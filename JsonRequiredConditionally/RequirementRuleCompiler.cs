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
	internal static bool HasRules(Type type) => EligibilityCache.GetOrAdd(type, static t => IsEligible(t, []));

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

	/// <summary>
	/// Determines eligibility, following the reachable object graph to find a decorated member
	/// anywhere beneath <paramref name="type"/>.
	/// </summary>
	/// <param name="type">The type under consideration.</param>
	/// <param name="visiting">
	/// The types currently on the call stack, guarding against infinite recursion through cyclic
	/// type graphs (mutually- or self-referential types). A cycle back to an ancestor contributes
	/// no eligibility on its own; if a decorated type is reachable, it is reachable through some
	/// other, non-cyclic edge that this same traversal also visits.
	/// </param>
	/// <returns>True when <paramref name="type"/> should be routed through the converter.</returns>
	private static bool IsEligible(Type type, HashSet<Type> visiting)
	{
		if (type.IsPrimitive || type.IsEnum || type == typeof(string) || type == typeof(decimal))
		{
			return false;
		}

		// A collection type is never claimed for itself: System.Text.Json has its own converters for
		// these, and our converter's contract is "a single materialized object", not a sequence or
		// map. Reachability through a collection-typed member still applies -- see
		// EnumerateReachableMemberTypes, which unwraps the element/value type before recursing here.
		if (typeof(IEnumerable).IsAssignableFrom(type))
		{
			return false;
		}

		if (!visiting.Add(type))
		{
			return false;
		}

		try
		{
			if (HasDirectlyDecoratedMember(type))
			{
				return true;
			}

			foreach (Type reachable in EnumerateReachableMemberTypes(type))
			{
				if (IsEligible(reachable, visiting))
				{
					return true;
				}
			}

			return false;
		}
		finally
		{
			visiting.Remove(type);
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
	/// Enumerates the types reachable from a type's own candidate members, unwrapping collection
	/// and dictionary members to their element or value type rather than the collection type itself.
	/// </summary>
	/// <param name="type">The type to enumerate reachable member types for.</param>
	/// <returns>The type of each candidate member, or its element/value type when the member is a collection.</returns>
	private static IEnumerable<Type> EnumerateReachableMemberTypes(Type type)
	{
		foreach (MemberInfo member in EnumerateCandidateMembers(type))
		{
			// A member System.Text.Json itself will never populate cannot carry a reachable
			// requirement: nothing will ever validate against JSON that member was never built from.
			if (member.IsDefined(typeof(JsonIgnoreAttribute), inherit: true))
			{
				continue;
			}

			Type? memberType = member switch
			{
				PropertyInfo property when property.GetIndexParameters().Length == 0 => property.PropertyType,
				FieldInfo field => field.FieldType,
				_ => null,
			};

			if (memberType is null)
			{
				continue;
			}

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
}
