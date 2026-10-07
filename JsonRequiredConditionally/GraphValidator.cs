// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.JsonRequiredConditionally;

using System.Collections;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
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

			// A property-level [JsonConverter] read this member's JSON in a shape only that converter
			// knows, so the member type's own rules cannot be matched against it. This is the same
			// boundary a type-level converter draws, which System.Text.Json enforces for us by
			// leaving Properties empty; a property-level one leaves the member type's contract intact,
			// so the walk has to stop here itself.
			if (property.CustomConverter is not null)
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

			// IList, not IEnumerable: pairing is positional, so it is only sound for a collection whose
			// enumeration order is its payload order. An IList's indexer is defined to be that order,
			// because System.Text.Json appends each element as it reads it. Sequences that reorder --
			// Stack<T> (LIFO), SortedSet<T> (comparer order), HashSet<T> (bucket order) -- are left
			// alone rather than mispaired; see DescendList.
			case JsonValueKind.Array when value is IList list:
				DescendList(element, list, plainOptions, userOptions, comparer, path, violations);
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
		Dictionary<object, (string Name, JsonElement Element)>? parsedKeys = null;

		// Enumerate the dictionary's own entries rather than indexing it: IDictionary's object-keyed
		// indexer returns null for a key of the wrong CLR type (e.g. int) instead of matching the
		// string key parsed from JSON, and throws outright for some read-only implementations
		// (e.g. ImmutableDictionary). DictionaryEntry enumeration works uniformly for both.
		foreach (DictionaryEntry entry in dictionary)
		{
			if (entry.Value is null)
			{
				continue;
			}

			if (entry.Key is string key)
			{
				if (TryGetProperty(element, key, comparer, caseInsensitive, out JsonElement child))
				{
					Descend(child, entry.Value, plainOptions, userOptions, comparer, Combine(path, key), violations);
				}

				continue;
			}

			// Any other key type is paired by parsing each JSON property name back into a key, rather
			// than by re-formatting the CLR key as text: System.Text.Json's key formats (ISO 8601 dates,
			// lower-case `true`, case-insensitive Guids, enum names) do not match what ToString gives,
			// and a mismatch silently skipped the entry, and every violation inside it.
			parsedKeys ??= ParseKeys(element, entry.Key.GetType(), plainOptions);

			if (parsedKeys.TryGetValue(entry.Key, out (string Name, JsonElement Element) match))
			{
				Descend(match.Element, entry.Value, plainOptions, userOptions, comparer, Combine(path, match.Name), violations);
			}
		}
	}

	/// <summary>
	/// Parses every property name of a JSON object into a dictionary key, through the same converter
	/// System.Text.Json used to read the keys when it materialized the dictionary.
	/// </summary>
	/// <param name="element">The JSON object the dictionary was materialized from.</param>
	/// <param name="keyType">The CLR type of the dictionary's keys.</param>
	/// <param name="options">The options the dictionary was materialized through.</param>
	/// <returns>Each parsed key, mapped to the property name as written and its value.</returns>
	/// <remarks>
	/// A later duplicate property name replaces an earlier one, as it does during deserialization. A
	/// name the converter cannot parse is left out; System.Text.Json would have failed on it already.
	/// A key type with no converter pairs nothing, the same as an unmatched key did before.
	/// </remarks>
	private static Dictionary<object, (string Name, JsonElement Element)> ParseKeys(JsonElement element, Type keyType, JsonSerializerOptions options)
	{
		Dictionary<object, (string Name, JsonElement Element)> keys = [];
		JsonConverter converter;

		try
		{
			converter = options.GetConverter(keyType);
		}
		catch (NotSupportedException)
		{
			return keys;
		}

		MethodInfo readKey = ReadKeyMethod.MakeGenericMethod(keyType);

		foreach (JsonProperty property in element.EnumerateObject())
		{
			object? key;

			try
			{
				key = readKey.Invoke(null, [converter, property.Name, options]);
			}
			catch (TargetInvocationException)
			{
				continue;
			}

			if (key is not null)
			{
				keys[key] = (property.Name, property.Value);
			}
		}

		return keys;
	}

	private static readonly MethodInfo ReadKeyMethod =
		typeof(GraphValidator).GetMethod(nameof(ReadKey), BindingFlags.NonPublic | BindingFlags.Static)!;

	/// <summary>
	/// Reads one property name as a dictionary key of type <typeparamref name="TKey"/>.
	/// </summary>
	/// <typeparam name="TKey">The dictionary's key type.</typeparam>
	/// <param name="converter">The converter System.Text.Json uses for <typeparamref name="TKey"/>.</param>
	/// <param name="name">The property name as written in the JSON.</param>
	/// <param name="options">The options the dictionary was materialized through.</param>
	/// <returns>The parsed key.</returns>
	private static object? ReadKey<TKey>(JsonConverter converter, string name, JsonSerializerOptions options)
	{
		// ReadAsPropertyName needs a reader positioned on a property name, so wrap the name in a
		// one-property object and advance to it.
		byte[] json = JsonSerializer.SerializeToUtf8Bytes(new Dictionary<string, int> { [name] = 0 });
		Utf8JsonReader reader = new(json);
		reader.Read();
		reader.Read();

		return ((JsonConverter<TKey>)converter).ReadAsPropertyName(ref reader, typeof(TKey), options);
	}

	/// <summary>
	/// Validates each element of a JSON array against the list entry materialized from it.
	/// </summary>
	/// <remarks>
	/// A collection that is not an <see cref="IList"/> is not descended into at all. Pairing the
	/// n-th JSON element with the n-th item a collection happens to yield is only correct when the
	/// two orders agree, and for a reordering collection they do not: a <see cref="Stack{T}"/>
	/// yields its items in reverse, so the rules compiled for one element would be evaluated against
	/// a different one. That misreports in both directions -- a valid payload rejected because an
	/// unrelated item failed to carry a conditionally required member, and an invalid one accepted
	/// because the item that should have failed was checked against another element's JSON. Not
	/// descending loses coverage for those collections, which is the same accepted boundary this
	/// walk already has for a type behind its own converter, and is strictly safer than descending
	/// with the wrong pairing.
	/// </remarks>
	private static void DescendList(
		JsonElement element,
		IList list,
		JsonSerializerOptions plainOptions,
		JsonSerializerOptions userOptions,
		StringComparer comparer,
		string path,
		ViolationCollector violations)
	{
		// Walk the JSON array's own enumerator rather than indexing the element by position:
		// JsonElement's array indexer falls back to a sequential scan for non-simple elements,
		// making per-index access O(n) and the whole loop O(n^2). The list side is indexed instead,
		// which is O(1) and carries the positional guarantee this pairing depends on.
		int index = 0;

		foreach (JsonElement itemElement in element.EnumerateArray())
		{
			if (index >= list.Count)
			{
				break;
			}

			object? item = list[index];

			if (item is not null)
			{
				Descend(itemElement, item, plainOptions, userOptions, comparer, $"{path}[{index}]", violations);
			}

			index++;
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
