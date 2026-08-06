// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace ktsu.JsonRequiredConditionally;

using System.Collections;
using System.Reflection;
using System.Text.Json;

/// <summary>
/// Walks a materialized object graph alongside the JSON it came from, applying requirement rules at
/// every level.
/// </summary>
/// <remarks>
/// Recursion is driven by the JSON element tree, which is finite, so a cyclic object graph cannot
/// cause unbounded descent.
/// </remarks>
internal static class GraphValidator
{
	/// <summary>
	/// Validates an object and everything beneath it, throwing if any requirement is unmet.
	/// </summary>
	/// <param name="element">The JSON the object was materialized from.</param>
	/// <param name="instance">The materialized object.</param>
	/// <param name="options">The options whose naming policy and case sensitivity apply.</param>
	/// <exception cref="JsonRequiredConditionallyException">One or more requirements were unmet.</exception>
	internal static void Validate(JsonElement element, object instance, JsonSerializerOptions options)
	{
		StringComparer comparer = options.PropertyNameCaseInsensitive
			? StringComparer.OrdinalIgnoreCase
			: StringComparer.Ordinal;

		List<string> missing = [];

		Walk(element, instance, options, comparer, missing);

		if (missing.Count > 0)
		{
			throw new JsonRequiredConditionallyException(missing);
		}
	}

	private static void Walk(
		JsonElement element,
		object instance,
		JsonSerializerOptions options,
		StringComparer comparer,
		List<string> missing)
	{
		if (element.ValueKind != JsonValueKind.Object)
		{
			return;
		}

		Type type = instance.GetType();

		if (RequirementRuleCompiler.HasRules(type))
		{
			HashSet<string> present = PresenceScanner.ScanPropertyNames(element, comparer);

			foreach (RequirementRule rule in RequirementRuleCompiler.GetRules(type, options))
			{
				if (!present.Contains(rule.JsonName) && rule.IsRequiredFor(instance))
				{
					missing.Add(rule.JsonName);
				}
			}
		}

		foreach (MemberInfo member in RequirementRuleCompiler.EnumerateCandidateMembers(type))
		{
			object? value = ReadMember(member, instance);

			if (value is null)
			{
				continue;
			}

			if (TryGetProperty(element, RequirementRuleCompiler.ResolveJsonName(member, options), comparer, out JsonElement child))
			{
				Descend(child, value, options, comparer, missing);
			}
		}
	}

	private static void Descend(
		JsonElement element,
		object value,
		JsonSerializerOptions options,
		StringComparer comparer,
		List<string> missing)
	{
		switch (element.ValueKind)
		{
			case JsonValueKind.Object when value is IDictionary dictionary:
				DescendDictionary(element, dictionary, options, comparer, missing);
				break;

			case JsonValueKind.Object:
				Walk(element, value, options, comparer, missing);
				break;

			case JsonValueKind.Array when value is IEnumerable sequence:
				DescendSequence(element, sequence, options, comparer, missing);
				break;

			default:
				break;
		}
	}

	private static void DescendDictionary(
		JsonElement element,
		IDictionary dictionary,
		JsonSerializerOptions options,
		StringComparer comparer,
		List<string> missing)
	{
		foreach (JsonProperty property in element.EnumerateObject())
		{
			object? item = dictionary[property.Name];

			if (item is not null)
			{
				Descend(property.Value, item, options, comparer, missing);
			}
		}
	}

	private static void DescendSequence(
		JsonElement element,
		IEnumerable sequence,
		JsonSerializerOptions options,
		StringComparer comparer,
		List<string> missing)
	{
		int length = element.GetArrayLength();
		int index = 0;

		foreach (object? item in sequence)
		{
			if (index >= length)
			{
				break;
			}

			if (item is not null)
			{
				Descend(element[index], item, options, comparer, missing);
			}

			index++;
		}
	}

	private static object? ReadMember(MemberInfo member, object instance) => member switch
	{
		PropertyInfo property when property.CanRead => property.GetValue(instance),
		FieldInfo field => field.GetValue(instance),
		_ => null,
	};

	private static bool TryGetProperty(
		JsonElement element,
		string name,
		StringComparer comparer,
		out JsonElement value)
	{
		if (element.TryGetProperty(name, out value))
		{
			return true;
		}

		if (!ReferenceEquals(comparer, StringComparer.OrdinalIgnoreCase))
		{
			value = default;
			return false;
		}

		foreach (JsonProperty property in element.EnumerateObject())
		{
			if (comparer.Equals(property.Name, name))
			{
				value = property.Value;
				return true;
			}
		}

		value = default;
		return false;
	}
}
