// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace ktsu.JsonRequiredConditionally.Tests;

using System.Text.Json;
using System.Text.Json.Serialization;

[TestClass]
public class ConverterTests
{
	internal static JsonSerializerOptions CreateOptions() =>
		new() { Converters = { new JsonStringEnumConverter(), new JsonRequiredConditionallyConverterFactory() } };

	[TestMethod]
	public void AbsentRequiredPropertyThrows()
	{
		JsonRequiredConditionallyException exception = Assert.ThrowsExactly<JsonRequiredConditionallyException>(
			() => JsonSerializer.Deserialize<SimpleConfig>("""{"Kind":"Advanced"}""", CreateOptions()));

		CollectionAssert.AreEqual(new List<string> { "Tuning" }, exception.MissingProperties.ToList());
	}

	[TestMethod]
	public void ExplicitNullSatisfiesTheRequirement()
	{
		SimpleConfig? config = JsonSerializer.Deserialize<SimpleConfig>(
			"""{"Kind":"Advanced","Tuning":null}""", CreateOptions());

		Assert.IsNotNull(config);
		Assert.IsNull(config.Tuning);
	}

	[TestMethod]
	public void PresentValueSatisfiesTheRequirement()
	{
		SimpleConfig? config = JsonSerializer.Deserialize<SimpleConfig>(
			"""{"Kind":"Advanced","Tuning":"fast"}""", CreateOptions());

		Assert.IsNotNull(config);
		Assert.AreEqual("fast", config.Tuning);
	}

	[TestMethod]
	public void NonMatchingSiblingLeavesPropertyOptional()
	{
		SimpleConfig? config = JsonSerializer.Deserialize<SimpleConfig>(
			"""{"Kind":"Expert"}""", CreateOptions());

		Assert.IsNotNull(config);
		Assert.IsNull(config.Tuning);
	}

	[TestMethod]
	public void OrSemanticsAcrossValuesOfOneSibling()
	{
		JsonSerializerOptions options = CreateOptions();

		Assert.ThrowsExactly<JsonRequiredConditionallyException>(
			() => JsonSerializer.Deserialize<OrConfig>("""{"Kind":"Advanced"}""", options));
		Assert.ThrowsExactly<JsonRequiredConditionallyException>(
			() => JsonSerializer.Deserialize<OrConfig>("""{"Kind":"Expert"}""", options));

		OrConfig? basic = JsonSerializer.Deserialize<OrConfig>("""{"Kind":"Basic"}""", options);
		Assert.IsNotNull(basic);
	}

	[TestMethod]
	public void AndSemanticsAcrossDifferentSiblings()
	{
		JsonSerializerOptions options = CreateOptions();

		Assert.ThrowsExactly<JsonRequiredConditionallyException>(
			() => JsonSerializer.Deserialize<AndConfig>("""{"Kind":"Advanced","Mode":"Remote"}""", options));

		AndConfig? local = JsonSerializer.Deserialize<AndConfig>("""{"Kind":"Advanced","Mode":"Local"}""", options);
		Assert.IsNotNull(local);
	}

	[TestMethod]
	public void UndecoratedTypeIsNotClaimedByTheFactory()
	{
		JsonRequiredConditionallyConverterFactory factory = new();

		Assert.IsFalse(factory.CanConvert(typeof(PlainConfig)));
		Assert.IsTrue(factory.CanConvert(typeof(SimpleConfig)));
	}

	[TestMethod]
	public void NullTokenDeserializesToNull()
	{
		SimpleConfig? config = JsonSerializer.Deserialize<SimpleConfig>("null", CreateOptions());

		Assert.IsNull(config);
	}

	[TestMethod]
	public void SerializationRoundTripsWithoutValidation()
	{
		SimpleConfig config = new() { Kind = Kind.Advanced, Tuning = null };

		string json = JsonSerializer.Serialize(config, CreateOptions());

		StringAssert.Contains(json, "Tuning");
	}
}
