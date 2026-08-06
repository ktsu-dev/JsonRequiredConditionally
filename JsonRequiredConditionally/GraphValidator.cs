// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace ktsu.JsonRequiredConditionally;

using System.Collections;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

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

		Walk(element, instance, options, comparer, string.Empty, missing);

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
		string path,
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
					missing.Add(Combine(path, rule.JsonName));
				}
			}
		}

		foreach (KeyValuePair<string, MemberInfo> candidate in SelectDescendantMembers(type, options, comparer))
		{
			object? value = ReadMember(candidate.Value, instance);

			if (value is null)
			{
				continue;
			}

			if (TryGetProperty(element, candidate.Key, comparer, options.PropertyNameCaseInsensitive, out JsonElement child))
			{
				Descend(child, value, options, comparer, Combine(path, candidate.Key), missing);
			}
		}
	}

	/// <summary>
	/// Selects, for each distinct JSON name, the single most-derived candidate member that carries
	/// it, skipping members STJ itself would never populate.
	/// </summary>
	/// <param name="type">The type to enumerate members of.</param>
	/// <param name="options">The options whose naming policy resolves JSON names.</param>
	/// <param name="comparer">The comparer matching the serializer's case sensitivity.</param>
	/// <returns>One member per distinct JSON name.</returns>
	private static Dictionary<string, MemberInfo> SelectDescendantMembers(
		Type type,
		JsonSerializerOptions options,
		StringComparer comparer)
	{
		Dictionary<string, MemberInfo> selected = new(comparer);

		foreach (MemberInfo member in RequirementRuleCompiler.EnumerateCandidateMembers(type))
		{
			if (member.IsDefined(typeof(JsonIgnoreAttribute), inherit: true))
			{
				continue;
			}

			string jsonName = RequirementRuleCompiler.ResolveJsonName(member, options);

			if (selected.TryGetValue(jsonName, out MemberInfo? existing) && !IsMoreDerived(member, existing))
			{
				continue;
			}

			selected[jsonName] = member;
		}

		return selected;
	}

	/// <summary>
	/// Determines whether a candidate member hides an already-selected member declared on a base type.
	/// </summary>
	/// <param name="candidate">The member under consideration.</param>
	/// <param name="existing">The previously selected member sharing the same JSON name.</param>
	/// <returns>True when <paramref name="candidate"/> is declared on a type more derived than <paramref name="existing"/>.</returns>
	private static bool IsMoreDerived(MemberInfo candidate, MemberInfo existing) =>
		existing.DeclaringType is not null &&
		candidate.DeclaringType is not null &&
		existing.DeclaringType != candidate.DeclaringType &&
		existing.DeclaringType.IsAssignableFrom(candidate.DeclaringType);

	private static void Descend(
		JsonElement element,
		object value,
		JsonSerializerOptions options,
		StringComparer comparer,
		string path,
		List<string> missing)
	{
		switch (element.ValueKind)
		{
			case JsonValueKind.Object when value is IDictionary dictionary:
				DescendDictionary(element, dictionary, options, comparer, path, missing);
				break;

			case JsonValueKind.Object:
				Walk(element, value, options, comparer, path, missing);
				break;

			case JsonValueKind.Array when value is IEnumerable sequence:
				DescendSequence(element, sequence, options, comparer, path, missing);
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
		string path,
		List<string> missing)
	{
		bool caseInsensitive = options.PropertyNameCaseInsensitive;

		// Enumerate the dictionary's own entries rather than indexing it: IDictionary's object-keyed
		// indexer returns null for a key of the wrong CLR type (e.g. int) instead of matching the
		// string key parsed from JSON, and throws outright for some read-only implementations
		// (e.g. ImmutableDictionary). DictionaryEntry enumeration works uniformly for both.
		foreach (DictionaryEntry entry in dictionary)
		{
			string? key = entry.Key?.ToString();

			if (key is null || entry.Value is null)
			{
				continue;
			}

			if (TryGetProperty(element, key, comparer, caseInsensitive, out JsonElement child))
			{
				Descend(child, entry.Value, options, comparer, Combine(path, key), missing);
			}
		}
	}

	private static void DescendSequence(
		JsonElement element,
		IEnumerable sequence,
		JsonSerializerOptions options,
		StringComparer comparer,
		string path,
		List<string> missing)
	{
		// Zip the JSON array's own enumerator against the sequence's, rather than indexing the
		// element by position: JsonElement's array indexer falls back to a sequential scan for
		// non-simple elements, making per-index access O(n) and the whole loop O(n^2).
		IEnumerator items = sequence.GetEnumerator();

		try
		{
			int index = 0;

			foreach (JsonElement itemElement in element.EnumerateArray())
			{
				if (!items.MoveNext())
				{
					break;
				}

				object? item = items.Current;

				if (item is not null)
				{
					Descend(itemElement, item, options, comparer, $"{path}[{index}]", missing);
				}

				index++;
			}
		}
		finally
		{
			(items as IDisposable)?.Dispose();
		}
	}

	private static object? ReadMember(MemberInfo member, object instance) => member switch
	{
		PropertyInfo property when property.CanRead && property.GetIndexParameters().Length == 0 => property.GetValue(instance),
		FieldInfo field => field.GetValue(instance),
		_ => null,
	};

	private static bool TryGetProperty(
		JsonElement element,
		string name,
		StringComparer comparer,
		bool caseInsensitive,
		out JsonElement value)
	{
		if (element.TryGetProperty(name, out value))
		{
			return true;
		}

		if (!caseInsensitive)
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

	private static string Combine(string prefix, string name) =>
		string.IsNullOrEmpty(prefix) ? name : prefix + "." + name;
}
