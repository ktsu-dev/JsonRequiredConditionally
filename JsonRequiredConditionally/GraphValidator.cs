// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.JsonRequiredConditionally;

using System.Collections;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

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
	/// <param name="plainOptions">
	/// The factory-free options the object was actually materialized through. Its own
	/// <see cref="JsonTypeInfo"/> model -- not a reflection re-implementation of it -- drives which
	/// members the walk descends into, so it agrees exactly with what System.Text.Json itself
	/// populated.
	/// </param>
	/// <param name="userOptions">The caller's own options, whose naming policy and case sensitivity apply to rule evaluation.</param>
	/// <exception cref="JsonRequiredConditionallyException">
	/// Violations are collected into two categories as the walk descends: properties absent from
	/// the payload, and properties present but empty. Both categories accumulate across the whole
	/// graph before this single exception is thrown at the end, carrying every unmet requirement of
	/// either kind rather than stopping at the first.
	/// </exception>
	internal static void Validate(JsonElement element, object instance, JsonSerializerOptions plainOptions, JsonSerializerOptions userOptions)
	{
		StringComparer comparer = userOptions.PropertyNameCaseInsensitive
			? StringComparer.OrdinalIgnoreCase
			: StringComparer.Ordinal;

		ViolationCollector violations = new();

		Walk(element, instance, plainOptions, userOptions, comparer, string.Empty, violations);

		if (violations.Any)
		{
			throw new JsonRequiredConditionallyException(violations.Missing, violations.Empty);
		}
	}

	private static void Walk(
		JsonElement element,
		object instance,
		JsonSerializerOptions plainOptions,
		JsonSerializerOptions userOptions,
		StringComparer comparer,
		string path,
		ViolationCollector violations)
	{
		if (element.ValueKind != JsonValueKind.Object)
		{
			return;
		}

		Type type = instance.GetType();

		if (RequirementRuleCompiler.HasRules(type))
		{
			HashSet<string> present = PresenceScanner.ScanPropertyNames(element, comparer);

			// plainOptions, not userOptions: System.Text.Json leaves JsonTypeInfo.Properties empty
			// for a type carrying its own converter, and a claimed type's JsonTypeInfo under the
			// user's own options is exactly that -- this library's converter. plainOptions is
			// factory-free, so GetRules (via Compile) sees the real member model underneath.
			foreach (RequirementRule rule in RequirementRuleCompiler.GetRules(type, plainOptions))
			{
				if (!present.Contains(rule.JsonName) && rule.IsRequiredFor(instance))
				{
					violations.Missing.Add(Combine(path, rule.JsonName));
				}
			}

			foreach (NonEmptyRule rule in RequirementRuleCompiler.GetNonEmptyRules(type, plainOptions))
			{
				string fullPath = Combine(path, rule.JsonName);

				if (!TryGetProperty(element, rule.JsonName, comparer, userOptions.PropertyNameCaseInsensitive, out JsonElement child))
				{
					// The loop above may already have reported this exact path, when the same member
					// carries both attributes and its sibling condition was satisfied. Violation lists
					// hold only actual failures and are therefore short, so a linear scan costs less
					// than building a set would.
					if (!violations.Missing.Contains(fullPath))
					{
						violations.Missing.Add(fullPath);
					}
				}
				else if (EmptinessInspector.IsEmpty(child))
				{
					violations.Empty.Add(fullPath);
				}
			}
		}

		// A type carrying its own custom converter has an empty Properties list here -- System.Text.Json
		// does not describe what such a converter does internally. The walk simply cannot see beneath
		// it; this is a known, accepted boundary of validating through the materialized graph rather
		// than the token stream, not a false positive or a crash.
		JsonTypeInfo? typeInfo = RequirementRuleCompiler.TryGetTypeInfo(plainOptions, type);

		if (typeInfo is null)
		{
			return;
		}

		foreach (JsonPropertyInfo property in typeInfo.Properties)
		{
			// A member System.Text.Json could never have populated during deserialization must not
			// be validated against the JSON either: its current value is whatever its initializer
			// set, unrelated to this payload.
			if (!RequirementRuleCompiler.IsPopulatedByDeserialization(typeInfo, property))
			{
				continue;
			}

			// IsPopulatedByDeserialization already confirmed Get is non-null; the compiler cannot
			// see that across the method call.
			object? value = property.Get!(instance);

			if (value is null)
			{
				continue;
			}

			if (TryGetProperty(element, property.Name, comparer, userOptions.PropertyNameCaseInsensitive, out JsonElement child))
			{
				Descend(child, value, plainOptions, userOptions, comparer, Combine(path, property.Name), violations);
			}
		}
	}

	private static void Descend(
		JsonElement element,
		object value,
		JsonSerializerOptions plainOptions,
		JsonSerializerOptions userOptions,
		StringComparer comparer,
		string path,
		ViolationCollector violations)
	{
		switch (element.ValueKind)
		{
			case JsonValueKind.Object when value is IDictionary dictionary:
				DescendDictionary(element, dictionary, plainOptions, userOptions, comparer, path, violations);
				break;

			case JsonValueKind.Object:
				Walk(element, value, plainOptions, userOptions, comparer, path, violations);
				break;

			case JsonValueKind.Array when value is IEnumerable sequence:
				DescendSequence(element, sequence, plainOptions, userOptions, comparer, path, violations);
				break;

			default:
				break;
		}
	}

	private static void DescendDictionary(
		JsonElement element,
		IDictionary dictionary,
		JsonSerializerOptions plainOptions,
		JsonSerializerOptions userOptions,
		StringComparer comparer,
		string path,
		ViolationCollector violations)
	{
		bool caseInsensitive = userOptions.PropertyNameCaseInsensitive;

		// Enumerate the dictionary's own entries rather than indexing it: IDictionary's object-keyed
		// indexer returns null for a key of the wrong CLR type (e.g. int) instead of matching the
		// string key parsed from JSON, and throws outright for some read-only implementations
		// (e.g. ImmutableDictionary). DictionaryEntry enumeration works uniformly for both.
		foreach (DictionaryEntry entry in dictionary)
		{
			string? key = FormatKey(entry.Key);

			if (key is null || entry.Value is null)
			{
				continue;
			}

			if (TryGetProperty(element, key, comparer, caseInsensitive, out JsonElement child))
			{
				Descend(child, entry.Value, plainOptions, userOptions, comparer, Combine(path, key), violations);
			}
		}
	}

	/// <summary>
	/// Formats a dictionary key the same way System.Text.Json writes it: invariantly, not under the
	/// current culture. A negative <c>int</c> key under a culture whose negative sign is not ASCII
	/// hyphen-minus would otherwise never match the JSON property name System.Text.Json produced.
	/// </summary>
	/// <param name="key">The dictionary entry's key.</param>
	/// <returns>The key formatted invariantly, or null if the key itself is null.</returns>
	private static string? FormatKey(object? key) => key switch
	{
		null => null,
		IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
		_ => key.ToString(),
	};

	private static void DescendSequence(
		JsonElement element,
		IEnumerable sequence,
		JsonSerializerOptions plainOptions,
		JsonSerializerOptions userOptions,
		StringComparer comparer,
		string path,
		ViolationCollector violations)
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
					Descend(itemElement, item, plainOptions, userOptions, comparer, $"{path}[{index}]", violations);
				}

				index++;
			}
		}
		finally
		{
			(items as IDisposable)?.Dispose();
		}
	}

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

/// <summary>
/// Accumulates the violations found during one walk, in their two categories.
/// </summary>
/// <remarks>
/// A single parameter carrying both lists, rather than one parameter per category: the walk methods
/// already take seven parameters each, and the two categories are always produced and consumed
/// together.
/// </remarks>
internal sealed class ViolationCollector
{
	/// <summary>
	/// Gets the paths of properties that were required but absent from the payload.
	/// </summary>
	internal List<string> Missing { get; } = [];

	/// <summary>
	/// Gets the paths of properties that were present but carried an empty value.
	/// </summary>
	internal List<string> Empty { get; } = [];

	/// <summary>
	/// Gets a value indicating whether any violation was collected.
	/// </summary>
	internal bool Any => Missing.Count > 0 || Empty.Count > 0;
}
