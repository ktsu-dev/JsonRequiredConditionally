// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.JsonRequiredConditionally.Tests;

using System.Text.Json;
using System.Text.Json.Serialization;

[TestClass]
public class NamingTests
{
	private static JsonSerializerOptions CreateOptions(
		JsonNamingPolicy? policy = null,
		bool caseInsensitive = false) =>
		new()
		{
			PropertyNamingPolicy = policy,
			PropertyNameCaseInsensitive = caseInsensitive,
			Converters =
			{
				new JsonStringEnumConverter(),
				new JsonRequiredConditionallyConverterFactory(),
			},
		};

	[TestMethod]
	public void CamelCasePolicyMatchesCamelCasedPayload()
	{
		JsonSerializerOptions options = CreateOptions(JsonNamingPolicy.CamelCase);

		SimpleConfig? config = JsonSerializer.Deserialize<SimpleConfig>(
			"""{"kind":"Advanced","tuning":"fast"}""", options);

		Assert.IsNotNull(config);
		Assert.AreEqual("fast", config.Tuning);
	}

	[TestMethod]
	public void CamelCasePolicyStillDetectsAbsence()
	{
		JsonSerializerOptions options = CreateOptions(JsonNamingPolicy.CamelCase);

		JsonRequiredConditionallyException exception = Assert.ThrowsExactly<JsonRequiredConditionallyException>(
			() => JsonSerializer.Deserialize<SimpleConfig>("""{"kind":"Advanced"}""", options));

		Assert.AreSequenceEqual(new List<string> { "tuning" }, [.. exception.MissingProperties]);
	}

	[TestMethod]
	public void JsonPropertyNameOverridesTheNamingPolicy()
	{
		JsonSerializerOptions options = CreateOptions(JsonNamingPolicy.CamelCase);

		RenamedConfig? config = JsonSerializer.Deserialize<RenamedConfig>(
			"""{"kind":"Advanced","tuning_value":"fast"}""", options);

		Assert.IsNotNull(config);
		Assert.AreEqual("fast", config.Tuning);
	}

	[TestMethod]
	public void JsonPropertyNameAbsenceIsReportedUnderTheJsonName()
	{
		JsonSerializerOptions options = CreateOptions(JsonNamingPolicy.CamelCase);

		JsonRequiredConditionallyException exception = Assert.ThrowsExactly<JsonRequiredConditionallyException>(
			() => JsonSerializer.Deserialize<RenamedConfig>("""{"kind":"Advanced"}""", options));

		Assert.AreSequenceEqual(new List<string> { "tuning_value" }, [.. exception.MissingProperties]);
	}

	[TestMethod]
	public void CaseInsensitiveMatchingAcceptsDifferentlyCasedPayload()
	{
		JsonSerializerOptions options = CreateOptions(caseInsensitive: true);

		SimpleConfig? config = JsonSerializer.Deserialize<SimpleConfig>(
			"""{"KIND":"Advanced","TUNING":"fast"}""", options);

		Assert.IsNotNull(config);
		Assert.AreEqual("fast", config.Tuning);
	}

	[TestMethod]
	public void CaseSensitiveMatchingTreatsMiscasedNameAsAbsent()
	{
		JsonSerializerOptions options = CreateOptions(caseInsensitive: false);

		Assert.ThrowsExactly<JsonRequiredConditionallyException>(
			() => JsonSerializer.Deserialize<SimpleConfig>("""{"Kind":"Advanced","TUNING":"fast"}""", options));
	}

	[TestMethod]
	public void NotEmptyRuleHonorsJsonPropertyName()
	{
		JsonSerializerOptions options = new() { Converters = { new JsonRequiredConditionallyConverterFactory() } };

		JsonRequiredConditionallyException exception = Assert.ThrowsExactly<JsonRequiredConditionallyException>(
			() => JsonSerializer.Deserialize<NotEmptyRenamedConfig>(/*lang=json,strict*/ """{"tuning_name":""}""", options));

		Assert.AreSequenceEqual(new List<string> { "tuning_name" }, [.. exception.EmptyProperties]);
	}

	[TestMethod]
	public void NotEmptyRuleHonorsACamelCaseNamingPolicy()
	{
		JsonSerializerOptions options = new()
		{
			PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
			Converters = { new JsonRequiredConditionallyConverterFactory() },
		};

		JsonRequiredConditionallyException exception = Assert.ThrowsExactly<JsonRequiredConditionallyException>(
			() => JsonSerializer.Deserialize<NotEmptyStringConfig>(/*lang=json,strict*/ """{"name":""}""", options));

		Assert.AreSequenceEqual(new List<string> { "name" }, [.. exception.EmptyProperties]);
	}

	[TestMethod]
	public void NotEmptyRuleHonorsCaseInsensitiveMatching()
	{
		JsonSerializerOptions options = new()
		{
			PropertyNameCaseInsensitive = true,
			Converters = { new JsonRequiredConditionallyConverterFactory() },
		};

		JsonRequiredConditionallyException exception = Assert.ThrowsExactly<JsonRequiredConditionallyException>(
			() => JsonSerializer.Deserialize<NotEmptyStringConfig>(/*lang=json,strict*/ """{"NAME":""}""", options));

		Assert.AreSequenceEqual(new List<string> { "Name" }, [.. exception.EmptyProperties]);
	}

	[TestMethod]
	public void CaseInsensitiveDuplicateIsValidatedAgainstTheLastOccurrenceWhenItIsInvalid()
	{
		JsonSerializerOptions options = CreateOptions(JsonNamingPolicy.CamelCase, caseInsensitive: true);

		JsonRequiredConditionallyException exception = Assert.ThrowsExactly<JsonRequiredConditionallyException>(
			() => JsonSerializer.Deserialize<OuterConfig>(
				/*lang=json*/ """{"child":{"kind":"Advanced","tuning":"x"},"Child":{"kind":"Advanced"}}""", options));

		Assert.AreSequenceEqual(new List<string> { "child.tuning" }, [.. exception.MissingProperties]);
	}

	[TestMethod]
	public void CaseInsensitiveDuplicateIsValidatedAgainstTheLastOccurrenceWhenItIsValid()
	{
		JsonSerializerOptions options = CreateOptions(JsonNamingPolicy.CamelCase, caseInsensitive: true);

		OuterConfig? config = JsonSerializer.Deserialize<OuterConfig>(
			/*lang=json*/ """{"child":{"kind":"Advanced"},"Child":{"kind":"Advanced","tuning":"x"}}""", options);

		Assert.IsNotNull(config?.Child);
		Assert.AreEqual("x", config.Child.Tuning);
	}

	[TestMethod]
	public void CaseInsensitiveDuplicateWithNeitherSpellingExactIsValidatedAgainstTheLastOccurrence()
	{
		JsonSerializerOptions options = CreateOptions(caseInsensitive: true);

		JsonRequiredConditionallyException exception = Assert.ThrowsExactly<JsonRequiredConditionallyException>(
			() => JsonSerializer.Deserialize<OuterConfig>(
				/*lang=json*/ """{"child":{"kind":"Advanced","tuning":"x"},"CHILD":{"kind":"Advanced"}}""", options));

		Assert.AreSequenceEqual(new List<string> { "Child.Tuning" }, [.. exception.MissingProperties]);
	}

	[TestMethod]
	public void CaseInsensitiveDuplicateNotEmptyMemberIsValidatedAgainstTheLastOccurrence()
	{
		JsonSerializerOptions options = CreateOptions(JsonNamingPolicy.CamelCase, caseInsensitive: true);

		JsonRequiredConditionallyException exception = Assert.ThrowsExactly<JsonRequiredConditionallyException>(
			() => JsonSerializer.Deserialize<NotEmptyStringConfig>(/*lang=json*/ """{"name":"x","Name":""}""", options));

		Assert.AreSequenceEqual(new List<string> { "name" }, [.. exception.EmptyProperties]);
	}

	[TestMethod]
	public void CaseInsensitiveOptionsStillPairDictionaryKeysExactly()
	{
		JsonSerializerOptions options = CreateOptions(caseInsensitive: true);

		JsonRequiredConditionallyException exception = Assert.ThrowsExactly<JsonRequiredConditionallyException>(
			() => JsonSerializer.Deserialize<CollectionConfig>(
				/*lang=json,strict*/ """{"Lookup":{"a":{"Kind":"Advanced"},"A":{"Kind":"Advanced","Tuning":"x"}}}""", options));

		Assert.AreSequenceEqual(new List<string> { "Lookup.a.Tuning" }, [.. exception.MissingProperties]);
	}
}
