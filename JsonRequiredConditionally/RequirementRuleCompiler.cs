// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace ktsu.JsonRequiredConditionally;

using System.Collections;
using System.Collections.Concurrent;
using System.Linq;
using System.Reflection;
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

	/// <summary>
	/// Determines whether a type carries at least one decorated member and is shaped like an object.
	/// </summary>
	/// <param name="type">The candidate type.</param>
	/// <returns>True when the type should be routed through the converter.</returns>
	internal static bool HasRules(Type type) => EligibilityCache.GetOrAdd(type, IsEligible);

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

	private static IEnumerable<MemberInfo> EnumerateCandidateMembers(Type type)
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

	private static string ResolveJsonName(MemberInfo member, JsonSerializerOptions options)
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

	private static bool IsEligible(Type type)
	{
		if (type.IsPrimitive || type.IsEnum || type == typeof(string) || type == typeof(decimal))
		{
			return false;
		}

		if (typeof(IEnumerable).IsAssignableFrom(type))
		{
			return false;
		}

		foreach (MemberInfo member in EnumerateCandidateMembers(type))
		{
			if (member.IsDefined(typeof(JsonRequiredIfSiblingIsAttribute), inherit: true))
			{
				return true;
			}
		}

		return false;
	}
}
