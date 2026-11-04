// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.JsonRequiredConditionally.Tests;

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;

/// <summary>A decorated type reached through a pair.</summary>
public sealed class NonEmptyName
{
	[JsonRequiredAndNotEmpty]
	public string? Name { get; set; }
}

/// <summary>Reaches a decorated type only through a list of pairs, with no decorated member of its own.</summary>
public sealed class PairListHolder
{
	[SuppressMessage("Design", "CA1002:Do not expose generic lists", Justification = "Test fixture round-trips through JSON deserialization, which requires a settable collection property.")]
	[SuppressMessage("Usage", "CA2227:Collection properties should be read only", Justification = "Test fixture round-trips through JSON deserialization, which requires a settable collection property.")]
	public List<KeyValuePair<string, NonEmptyName>>? P { get; set; }
}

/// <summary>A claimed holder with a list of pairs.</summary>
public sealed class ClaimedPairListHolder
{
	[JsonRequiredAndNotEmpty]
	public string? X { get; set; }

	[SuppressMessage("Design", "CA1002:Do not expose generic lists", Justification = "Test fixture round-trips through JSON deserialization, which requires a settable collection property.")]
	[SuppressMessage("Usage", "CA2227:Collection properties should be read only", Justification = "Test fixture round-trips through JSON deserialization, which requires a settable collection property.")]
	public List<KeyValuePair<string, NonEmptyName>>? P { get; set; }
}

/// <summary>A claimed holder with a single pair.</summary>
public sealed class ClaimedPairHolder
{
	[JsonRequiredAndNotEmpty]
	public string? X { get; set; }

	public KeyValuePair<string, NonEmptyName> P { get; set; }
}

/// <summary>A claimed holder whose pair holds a sibling-conditioned type.</summary>
public sealed class ClaimedSiblingPairHolder
{
	[JsonRequiredAndNotEmpty]
	public string? X { get; set; }

	public KeyValuePair<string, SimpleConfig> P { get; set; }
}

/// <summary>A holder whose pair holds nothing decorated.</summary>
public sealed class UndecoratedPairHolder
{
	public KeyValuePair<string, string>? P { get; set; }
}

[TestClass]
public class KeyValuePairTests
{
	private static JsonSerializerOptions CreateOptions() =>
		new() { Converters = { new JsonStringEnumConverter(), new JsonRequiredConditionallyConverterFactory() } };

	[TestMethod]
	public void UnclaimedHolderReportsThePairPath()
	{
		JsonRequiredConditionallyException exception = Assert.ThrowsExactly<JsonRequiredConditionallyException>(
			() => JsonSerializer.Deserialize<PairListHolder>(
				"""{"P":[{"Key":"k","Value":{"Name":""}}]}""", CreateOptions()));

		Assert.AreSequenceEqual(new List<string> { "P[0].Value.Name" }, [.. exception.EmptyProperties]);
	}

	[TestMethod]
	public void ClaimedHolderValidatesAListOfPairs()
	{
		JsonRequiredConditionallyException exception = Assert.ThrowsExactly<JsonRequiredConditionallyException>(
			() => JsonSerializer.Deserialize<ClaimedPairListHolder>(
				"""{"X":"x","P":[{"Key":"k","Value":{"Name":""}}]}""", CreateOptions()));

		Assert.AreSequenceEqual(new List<string> { "P[0].Value.Name" }, [.. exception.EmptyProperties]);
	}

	[TestMethod]
	public void ClaimedHolderValidatesASinglePair()
	{
		JsonRequiredConditionallyException exception = Assert.ThrowsExactly<JsonRequiredConditionallyException>(
			() => JsonSerializer.Deserialize<ClaimedPairHolder>(
				"""{"X":"x","P":{"Key":"k","Value":{"Name":""}}}""", CreateOptions()));

		Assert.AreSequenceEqual(new List<string> { "P.Value.Name" }, [.. exception.EmptyProperties]);
	}

	[TestMethod]
	public void SiblingRuleInsideAPairIsReported()
	{
		JsonRequiredConditionallyException exception = Assert.ThrowsExactly<JsonRequiredConditionallyException>(
			() => JsonSerializer.Deserialize<ClaimedSiblingPairHolder>(
				"""{"X":"x","P":{"Key":"k","Value":{"Kind":"Advanced"}}}""", CreateOptions()));

		Assert.AreSequenceEqual(new List<string> { "P.Value.Tuning" }, [.. exception.MissingProperties]);
	}

	[TestMethod]
	public void PairNamesFollowTheNamingPolicy()
	{
		JsonSerializerOptions options = CreateOptions();
		options.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;

		JsonRequiredConditionallyException exception = Assert.ThrowsExactly<JsonRequiredConditionallyException>(
			() => JsonSerializer.Deserialize<ClaimedPairHolder>(
				"""{"X":"x","p":{"key":"k","value":{"name":""}}}""", options));

		Assert.AreSequenceEqual(new List<string> { "p.value.name" }, [.. exception.EmptyProperties]);
	}

	[TestMethod]
	public void ValidPairDeserializes()
	{
		ClaimedPairHolder? holder = JsonSerializer.Deserialize<ClaimedPairHolder>(
			"""{"X":"x","P":{"Key":"k","Value":{"Name":"n"}}}""", CreateOptions());

		Assert.AreEqual("n", holder?.P.Value.Name);
	}

	[TestMethod]
	public void PairWithoutRulesIsNotClaimed()
	{
		JsonRequiredConditionallyConverterFactory factory = new();

		Assert.IsFalse(factory.CanConvert(typeof(KeyValuePair<string, string>)));
		Assert.IsFalse(factory.CanConvert(typeof(UndecoratedPairHolder)));
	}
}
