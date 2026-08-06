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
	/// <para>
	/// <see cref="System.Text.Json.Serialization.JsonConverterFactory"/>'s <c>CanConvert</c> receives
	/// only a <see cref="Type"/>, never the caller's <see cref="JsonSerializerOptions"/>, so this
	/// reachability check cannot honour caller-specific settings such as <c>IncludeFields</c> or a
	/// custom naming policy. It uses <see cref="StructuralProbeOptions"/> -- a fixed,
	/// reflection-based, default-configured instance -- as the best available approximation of
	/// "what would System.Text.Json actually walk into". This is an inherent limitation of the
	/// claiming API, not something any implementation strategy here could fully avoid.
	/// </para>
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
	/// <param name="options">
	/// The options to resolve the type's member model through. Must be factory-free (or otherwise
	/// not carrying this library's converter for <paramref name="type"/>) -- System.Text.Json leaves
	/// <see cref="JsonTypeInfo.Properties"/> empty for a type with its own converter, which would
	/// make every claimed type appear to have no members at all.
	/// </param>
	/// <returns>One rule per decorated member.</returns>
	/// <exception cref="InvalidOperationException">A sibling name does not resolve to a readable member.</exception>
	internal static RequirementRule[] Compile(Type type, JsonSerializerOptions options)
	{
		JsonTypeInfo? typeInfo = TryGetTypeInfo(options, type);

		if (typeInfo is null)
		{
			return [];
		}

		List<RequirementRule> rules = [];

		foreach (JsonPropertyInfo property in typeInfo.Properties)
		{
			if (property.AttributeProvider is not MemberInfo member)
			{
				continue;
			}

			JsonRequiredIfSiblingIsAttribute[] attributes =
				[.. member.GetCustomAttributes<JsonRequiredIfSiblingIsAttribute>(inherit: true)];

			if (attributes.Length == 0)
			{
				continue;
			}

			SiblingCondition[] conditions = BuildConditions(type, attributes);

			// property.Name is System.Text.Json's own resolved JSON name: an explicit
			// [JsonPropertyName] already wins over the naming policy, so there is no need to
			// re-derive that here the way ResolveJsonName used to.
			rules.Add(new RequirementRule(property.Name, member.Name, conditions));
		}

		return [.. rules];
	}

	/// <summary>
	/// Gets the cached requirement rules for a type under a given set of options.
	/// </summary>
	/// <param name="type">The type to get rules for.</param>
	/// <param name="options">
	/// The options to resolve the type's member model through -- forwarded directly to
	/// <see cref="Compile"/>, this is the only production route into it. Must be factory-free (or
	/// otherwise not carrying this library's converter for <paramref name="type"/>): System.Text.Json
	/// leaves <see cref="JsonTypeInfo.Properties"/> empty for a type with its own converter, so
	/// passing factory-carrying options here silently yields no rules at all, for every type that
	/// converter claims.
	/// </param>
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

	/// <summary>
	/// Determines whether a type has a member directly decorated with
	/// <see cref="JsonRequiredIfSiblingIsAttribute"/>, using System.Text.Json's own contract model
	/// (via <see cref="StructuralProbeOptions"/>) to find the member set rather than re-deriving it
	/// from raw reflection -- otherwise a decorated member System.Text.Json would not itself
	/// populate (e.g. a non-public property without <c>[JsonInclude]</c>) would be found here but
	/// never actually validated, or vice versa.
	/// </summary>
	/// <param name="type">The type to check.</param>
	/// <returns>True when a member System.Text.Json would populate carries the attribute.</returns>
	private static bool HasDirectlyDecoratedMember(Type type)
	{
		JsonTypeInfo? typeInfo = TryGetTypeInfo(StructuralProbeOptions, type);

		if (typeInfo is null)
		{
			return false;
		}

		foreach (JsonPropertyInfo property in typeInfo.Properties)
		{
			if (property.AttributeProvider is MemberInfo member &&
				member.IsDefined(typeof(JsonRequiredIfSiblingIsAttribute), inherit: true))
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
			if (!IsPopulatedByDeserialization(property))
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

	[SuppressMessage("Style", "IDE0028:Collection initialization can be simplified", Justification = "A collection expression does not compile for ConditionalWeakTable on netstandard2.0.")]
	private static readonly ConditionalWeakTable<JsonSerializerOptions, JsonSerializerOptions> ResolverEnsuredOptionsCache = new();

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
	/// might be assumed from the exception's usual role elsewhere in System.Text.Json. Rather than
	/// require every caller to pre-configure a resolver -- <c>RequirementRuleCompilerTests</c>
	/// legitimately calls <see cref="Compile"/> with a bare, never-configured
	/// <see cref="JsonSerializerOptions"/>, and mutating a caller's own options object in place is
	/// unsafe (the <see cref="JsonSerializerOptions.TypeInfoResolver"/> setter throws if the
	/// instance has already been locked by prior use elsewhere) -- this method transparently
	/// substitutes a cached, resolver-equipped clone for any options instance that does not already
	/// have one, preserving every other setting (naming policy, <c>IncludeFields</c>, etc.) from the
	/// original. Both exceptions are still caught defensively for any type shape the resolver
	/// genuinely cannot describe even once configured.
	/// </remarks>
	internal static JsonTypeInfo? TryGetTypeInfo(JsonSerializerOptions options, Type type)
	{
		JsonSerializerOptions effective = options.TypeInfoResolver is not null
			? options
			: ResolverEnsuredOptionsCache.GetValue(options, static o => new JsonSerializerOptions(o) { TypeInfoResolver = new DefaultJsonTypeInfoResolver() });

		try
		{
			return effective.GetTypeInfo(type);
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

	/// <summary>
	/// Determines whether System.Text.Json would actually populate a member during deserialization
	/// -- i.e. whether validating it against the incoming JSON means anything at all.
	/// </summary>
	/// <param name="property">The property to check.</param>
	/// <returns>True when the property is directly settable, or bound to a deserialization constructor parameter.</returns>
	/// <remarks>
	/// A property with a setter is always populated (this covers records and <c>init</c>-only
	/// properties too, both of which report a non-null <see cref="JsonPropertyInfo.Set"/>). A
	/// get-only property is populated only when a parameter of the constructor System.Text.Json
	/// would actually select (see <see cref="SelectDeserializationConstructor"/>) binds to it,
	/// detected here by an ordinal-insensitive name match against that one constructor's parameters
	/// -- not every public constructor the type happens to declare, which would treat a get-only
	/// property as populated merely because some unrelated convenience overload happens to have a
	/// same-named parameter. This is an approximation of System.Text.Json's own
	/// constructor-parameter binding, not a faithful reproduction of it -- the precise answer,
	/// <c>JsonPropertyInfo.AssociatedParameter</c>, is only available on net9.0+, and this library
	/// also targets net7.0, net8.0, netstandard2.0 and netstandard2.1.
	/// </remarks>
	internal static bool IsPopulatedByDeserialization(JsonPropertyInfo property)
	{
		if (property.Get is null)
		{
			return false;
		}

		if (property.Set is not null)
		{
			return true;
		}

		return IsConstructorBound(property);
	}

	private static bool IsConstructorBound(JsonPropertyInfo property)
	{
		if (property.AttributeProvider is not MemberInfo member || member.DeclaringType is null)
		{
			return false;
		}

		ConstructorInfo? constructor = SelectDeserializationConstructor(member.DeclaringType);

		if (constructor is null)
		{
			return false;
		}

		foreach (ParameterInfo parameter in constructor.GetParameters())
		{
			if (string.Equals(parameter.Name, member.Name, StringComparison.OrdinalIgnoreCase))
			{
				return true;
			}
		}

		return false;
	}

	/// <summary>
	/// Approximates System.Text.Json's own deserialization constructor selection, in the same
	/// precedence it uses: an explicit <c>[JsonConstructor]</c> wins outright; otherwise a public
	/// parameterless constructor is preferred (and binds no properties at all, since it takes no
	/// parameters); otherwise a single public parameterized constructor is used; otherwise the
	/// choice is genuinely ambiguous between multiple candidate constructors and no constructor is
	/// treated as authoritative for binding purposes.
	/// </summary>
	/// <param name="type">The type to select a deserialization constructor for.</param>
	/// <returns>The constructor System.Text.Json would select, or null when the selection is ambiguous.</returns>
	private static ConstructorInfo? SelectDeserializationConstructor(Type type)
	{
		ConstructorInfo[] publicConstructors = type.GetConstructors();

		ConstructorInfo[] jsonConstructors =
			[.. publicConstructors.Where(constructor => constructor.IsDefined(typeof(JsonConstructorAttribute), inherit: true))];

		if (jsonConstructors.Length == 1)
		{
			return jsonConstructors[0];
		}

		ConstructorInfo? parameterless = Array.Find(publicConstructors, constructor => constructor.GetParameters().Length == 0);

		if (parameterless is not null)
		{
			return parameterless;
		}

		ConstructorInfo[] parameterized = [.. publicConstructors.Where(constructor => constructor.GetParameters().Length > 0)];

		return parameterized.Length == 1 ? parameterized[0] : null;
	}
}
