// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

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

		CollectionAssert.AreEqual(new List<string> { "tuning" }, exception.MissingProperties.ToList());
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

		CollectionAssert.AreEqual(new List<string> { "tuning_value" }, exception.MissingProperties.ToList());
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
}
