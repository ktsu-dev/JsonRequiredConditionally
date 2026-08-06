// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace ktsu.JsonRequiredConditionally;

using System.Collections;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

/// <summary>
/// Builds the requirement rules for a type by reflecting over its decorated members.
/// </summary>
internal static class RequirementRuleCompiler
{
	private const BindingFlags MemberFlags = BindingFlags.Public | BindingFlags.Instance;
	private const BindingFlags SiblingFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

	private static readonly ConcurrentDictionary<Type, bool> EligibilityCache = new();

	[SuppressMessage("Style", "IDE0028:Collection initialization can be simplified", Justification = "A collection expression does not compile for ConditionalWeakTable on netstandard2.0.")]
	private static readonly ConditionalWeakTable<JsonSerializerOptions, ConcurrentDictionary<Type, RequirementRule[]>> RuleCache = new();

	/// <summary>
	/// A reflection-based options instance used only to ask System.Text.Json's own contract model
	/// which members of a type it would actually serialize, for the purpose of deciding whether a
	/// type transitively reaches a decorated member. This is deliberately not derived from any
	/// caller's options -- <see cref="System.Text.Json.Serialization.JsonConverterFactory"/>'s
	/// <c>CanConvert</c> is only ever given a <see cref="Type"/>, so no caller options exist yet to
	/// consult at this point; see the remarks on <see cref="HasRules"/>.
	/// </summary>
	private static readonly JsonSerializerOptions StructuralProbeOptions = new() { TypeInfoResolver = new DefaultJsonTypeInfoResolver() };

	/// <summary>
	/// Determines whether a type carries at least one decorated member, or transitively reaches a
	/// type that does through its own members' object graph, and is itself shaped like an object.
	/// </summary>
	/// <param name="type">The candidate type.</param>
	/// <returns>True when the type should be routed through the converter.</returns>
	/// <remarks>
	/// Transitivity matters: a container with no decorated member of its own (e.g. one holding a
	/// <c>List&lt;T&gt;</c> of a decorated <c>T</c>) must still be claimed at its own top, or its
	/// decorated descendants get independently re-resolved by System.Text.Json using the caller's
	/// original options instead of being reached by this type's own graph walk — each losing all
	/// path context above itself.
	/// </remarks>
	/// <remarks>
	/// <see cref="System.Text.Json.Serialization.JsonConverterFactory"/>'s <c>CanConvert</c> receives
	/// only a <see cref="Type"/>, never the caller's <see cref="JsonSerializerOptions"/>, so this
	/// reachability check cannot honour caller-specific settings such as <c>IncludeFields</c> or a
	/// custom naming policy. It uses <see cref="StructuralProbeOptions"/> -- a fixed,
	/// reflection-based, default-configured instance -- as the best available approximation of
	/// "what would System.Text.Json actually walk into". This is an inherent limitation of the
	/// claiming API, not something any implementation strategy here could fully avoid.
	/// </remarks>
	internal static bool HasRules(Type type)
	{
		if (EligibilityCache.TryGetValue(type, out bool cached))
		{
			return cached;
		}

		if (IsExcludedFromEligibility(type))
		{
			EligibilityCache.TryAdd(type, false);
			return false;
		}

		HashSet<Type> visited = [];
		List<Type> reachable = [];

		if (CollectReachable(type, visited, reachable))
		{
			// A decorated type was found somewhere in the reachable graph. `type` is definitely
			// eligible; other, not-yet-fully-explored nodes on the way are left uncached here and
			// resolved independently (cheaply, since they benefit from whatever got cached during
			// this pass) whenever they are queried directly.
			EligibilityCache.TryAdd(type, true);
			return true;
		}

		// The entire reachable component was explored -- each type visited at most once, via
		// `visited` -- with no decoration found anywhere. Reachability is transitive: whatever any
		// type in `reachable` can itself reach is a subset of what `type` reaches, so if nothing in
		// the whole component is decorated, none of them are eligible either. Caching the whole
		// component in one pass here is what keeps a densely-connected type graph (e.g. many
		// mutually-referencing undecorated types) from being independently rediscovered, node by
		// node, on every later call -- without it, resolving eligibility for a type graph shaped
		// like a clique degrades to enumerating simple paths, which is factorial in the number of
		// types.
		foreach (Type visitedType in reachable)
		{
			EligibilityCache.TryAdd(visitedType, false);
		}

		return false;
	}

	/// <summary>
	/// Collects every type reachable from <paramref name="type"/>, visiting each at most once, and
	/// reports whether a directly-decorated type was found anywhere in the reachable graph.
	/// </summary>
	/// <param name="type">The type to explore from.</param>
	/// <param name="visited">
	/// Types already visited in this traversal. Doubles as cycle protection: a type is added before
	/// its own members are explored, so a cyclic reference back to it is a no-op rather than
	/// infinite recursion.
	/// </param>
	/// <param name="reachable">Accumulates every non-excluded type visited, in visitation order.</param>
	/// <returns>True as soon as a directly-decorated type is found anywhere in the reachable graph.</returns>
	private static bool CollectReachable(Type type, HashSet<Type> visited, List<Type> reachable)
	{
		if (IsExcludedFromEligibility(type) || !visited.Add(type))
		{
			return false;
		}

		if (EligibilityCache.TryGetValue(type, out bool cached))
		{
			// Already resolved by an earlier call: trust it rather than re-exploring. A cached
			// `false` specifically means this type's entire reachable set was already proven, in
			// full, to be decoration-free.
			return cached;
		}

		reachable.Add(type);

		if (HasDirectlyDecoratedMember(type))
		{
			return true;
		}

		foreach (Type memberType in EnumerateReachableMemberTypes(type))
		{
			if (CollectReachable(memberType, visited, reachable))
			{
				return true;
			}
		}

		return false;
	}

	private static bool IsExcludedFromEligibility(Type type) =>
		type.IsPrimitive || type.IsEnum || type == typeof(string) || type == typeof(decimal) || typeof(IEnumerable).IsAssignableFrom(type);

	/// <summary>
	/// Compiles the requirement rules for a type against a specific set of serializer options.
	/// </summary>
	/// <param name="type">The type to compile rules for.</param>
	/// <param name="options">The options whose naming policy resolves JSON names.</param>
	/// <returns>One rule per decorated member.</returns>
	/// <exception cref="InvalidOperationException">A sibling name does not resolve to a readable member.</exception>
	internal static RequirementRule[] Compile(Type type, JsonSerializerOptions options)
	{
		List<RequirementRule> rules = [];

		foreach (MemberInfo member in EnumerateCandidateMembers(type))
		{
			JsonRequiredIfSiblingIsAttribute[] attributes =
				[.. member.GetCustomAttributes<JsonRequiredIfSiblingIsAttribute>(inherit: true)];

			if (attributes.Length == 0)
			{
				continue;
			}

			SiblingCondition[] conditions = BuildConditions(type, attributes);

			rules.Add(new RequirementRule(ResolveJsonName(member, options), member.Name, conditions));
		}

		return [.. rules];
	}

	/// <summary>
	/// Gets the cached requirement rules for a type under a given set of options.
	/// </summary>
	/// <param name="type">The type to get rules for.</param>
	/// <param name="options">The options whose naming policy resolves JSON names.</param>
	/// <returns>One rule per decorated member.</returns>
	internal static RequirementRule[] GetRules(Type type, JsonSerializerOptions options)
	{
		ConcurrentDictionary<Type, RequirementRule[]> perType =
			RuleCache.GetValue(options, static _ => new ConcurrentDictionary<Type, RequirementRule[]>());

		return perType.GetOrAdd(type, t => Compile(t, options));
	}

	private static SiblingCondition[] BuildConditions(Type type, JsonRequiredIfSiblingIsAttribute[] attributes)
	{
		List<SiblingCondition> conditions = [];

		foreach (IGrouping<string, JsonRequiredIfSiblingIsAttribute> group in
			attributes.GroupBy(attribute => attribute.SiblingName, StringComparer.Ordinal))
		{
			Func<object, object?> accessor = CreateAccessor(type, group.Key);
			object?[] values = [.. group.Select(attribute => attribute.Value)];

			conditions.Add(new SiblingCondition(group.Key, accessor, values));
		}

		return [.. conditions];
	}

	/// <summary>
	/// Enumerates the public instance properties and fields of a type that are candidates for
	/// carrying a <see cref="JsonRequiredIfSiblingIsAttribute"/> or being a sibling.
	/// </summary>
	/// <param name="type">The type to enumerate.</param>
	/// <returns>The type's public instance properties, then its public instance fields.</returns>
	internal static IEnumerable<MemberInfo> EnumerateCandidateMembers(Type type)
	{
		foreach (PropertyInfo property in type.GetProperties(MemberFlags))
		{
			yield return property;
		}

		foreach (FieldInfo field in type.GetFields(MemberFlags))
		{
			yield return field;
		}
	}

	/// <summary>
	/// Resolves the JSON name a member serializes under, honouring an explicit
	/// <see cref="JsonPropertyNameAttribute"/> before falling back to the options' naming policy.
	/// </summary>
	/// <param name="member">The member to resolve a name for.</param>
	/// <param name="options">The options whose naming policy applies absent an explicit name.</param>
	/// <returns>The JSON name for the member.</returns>
	internal static string ResolveJsonName(MemberInfo member, JsonSerializerOptions options)
	{
		JsonPropertyNameAttribute? nameAttribute = member.GetCustomAttribute<JsonPropertyNameAttribute>(inherit: true);

		return nameAttribute is not null
			? nameAttribute.Name
			: options.PropertyNamingPolicy?.ConvertName(member.Name) ?? member.Name;
	}

	private static Func<object, object?> CreateAccessor(Type type, string siblingName)
	{
		PropertyInfo? property = FindProperty(type, siblingName);
		if (property is not null && property.CanRead)
		{
			return instance => property.GetValue(instance);
		}

		FieldInfo? field = FindField(type, siblingName);
		if (field is not null)
		{
			return instance => field.GetValue(instance);
		}

		throw new InvalidOperationException(
			$"[{nameof(JsonRequiredIfSiblingIsAttribute)}] on type '{type.Name}' names sibling '{siblingName}', which is not a readable property or field of that type.");
	}

	private static PropertyInfo? FindProperty(Type type, string siblingName)
	{
		try
		{
			return type.GetProperty(siblingName, SiblingFlags);
		}
		catch (AmbiguousMatchException)
		{
			// A derived type hides a same-named base member; prefer the most-derived one.
			return type.GetProperty(siblingName, SiblingFlags | BindingFlags.DeclaredOnly);
		}
	}

	private static FieldInfo? FindField(Type type, string siblingName)
	{
		try
		{
			return type.GetField(siblingName, SiblingFlags);
		}
		catch (AmbiguousMatchException)
		{
			// A derived type hides a same-named base member; prefer the most-derived one.
			return type.GetField(siblingName, SiblingFlags | BindingFlags.DeclaredOnly);
		}
	}

	private static bool HasDirectlyDecoratedMember(Type type)
	{
		foreach (MemberInfo member in EnumerateCandidateMembers(type))
		{
			if (member.IsDefined(typeof(JsonRequiredIfSiblingIsAttribute), inherit: true))
			{
				return true;
			}
		}

		return false;
	}

	/// <summary>
	/// Enumerates the types reachable from a type's own members, asking System.Text.Json's own
	/// contract model (via <see cref="StructuralProbeOptions"/>) which members it would actually
	/// serialize rather than re-deriving that from raw reflection. Collection and dictionary
	/// members are unwrapped to their element or value type rather than yielding the collection
	/// type itself.
	/// </summary>
	/// <param name="type">The type to enumerate reachable member types for.</param>
	/// <returns>The type of each member System.Text.Json would populate, or its element/value type when the member is a collection.</returns>
	/// <remarks>
	/// This must use the same member model <see cref="GraphValidator"/>'s walk uses (property/field
	/// inclusion, <c>[JsonIgnore]</c>, <c>[JsonInclude]</c>, hiding, <c>IncludeFields</c>), or
	/// eligibility and descent would disagree about what is reachable -- eligibility could claim a
	/// type the walk then never actually finds anything to descend into, or vice versa.
	/// </remarks>
	private static IEnumerable<Type> EnumerateReachableMemberTypes(Type type)
	{
		JsonTypeInfo? typeInfo = TryGetTypeInfo(StructuralProbeOptions, type);

		if (typeInfo is null)
		{
			yield break;
		}

		foreach (JsonPropertyInfo property in typeInfo.Properties)
		{
			// A member System.Text.Json could never populate during deserialization (get-only with
			// no setter, or otherwise not settable) cannot carry a reachable requirement either:
			// nothing will ever validate against JSON such a member was never built from.
			if (property.Get is null || property.Set is null)
			{
				continue;
			}

			Type memberType = property.PropertyType;

			if (memberType != typeof(string) && typeof(IEnumerable).IsAssignableFrom(memberType))
			{
				foreach (Type elementType in EnumerateElementTypes(memberType))
				{
					yield return elementType;
				}
			}
			else
			{
				yield return memberType;
			}
		}
	}

	/// <summary>
	/// Determines the element type of a sequence, or the value type of a dictionary, from its
	/// generic collection interfaces.
	/// </summary>
	/// <param name="type">The collection type to inspect.</param>
	/// <returns>Zero or one type: the dictionary value type if the collection is a dictionary, otherwise the sequence element type.</returns>
	private static IEnumerable<Type> EnumerateElementTypes(Type type)
	{
		foreach (Type candidateInterface in type.GetInterfaces())
		{
			if (candidateInterface.IsGenericType && candidateInterface.GetGenericTypeDefinition() == typeof(IDictionary<,>))
			{
				yield return candidateInterface.GetGenericArguments()[1];
				yield break;
			}
		}

		foreach (Type candidateInterface in type.GetInterfaces())
		{
			if (candidateInterface.IsGenericType && candidateInterface.GetGenericTypeDefinition() == typeof(IEnumerable<>))
			{
				yield return candidateInterface.GetGenericArguments()[0];
				yield break;
			}
		}
	}

	/// <summary>
	/// Gets the System.Text.Json contract metadata for a type under a given set of options, or null
	/// if the resolver cannot supply one.
	/// </summary>
	/// <param name="options">The options whose contract resolver describes the type.</param>
	/// <param name="type">The type to get metadata for.</param>
	/// <returns>The type's contract metadata, or null when the type is not supported by the resolver.</returns>
	/// <remarks>
	/// Observed empirically: an options instance whose resolver has never been consulted throws
	/// <see cref="NotSupportedException"/> here, not <see cref="InvalidOperationException"/> as
	/// might be assumed from the exception's usual role elsewhere in System.Text.Json. Both callers
	/// of this method use an options instance with <see cref="JsonSerializerOptions.TypeInfoResolver"/>
	/// explicitly set, which avoids that specific case, but both exceptions are caught here as a
	/// defensive guard against any type the resolver genuinely cannot describe.
	/// </remarks>
	internal static JsonTypeInfo? TryGetTypeInfo(JsonSerializerOptions options, Type type)
	{
		try
		{
			return options.GetTypeInfo(type);
		}
		catch (InvalidOperationException)
		{
			return null;
		}
		catch (NotSupportedException)
		{
			return null;
		}
	}
}
