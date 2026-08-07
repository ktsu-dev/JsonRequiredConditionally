// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.JsonRequiredConditionally.Tests;

using System.Text.Json;
using System.Text.Json.Serialization;

/// <summary>
/// Covers matching an attribute's constant against a sibling whose declared type differs from the
/// constant's own -- the case that used to make a rule quietly never fire.
/// </summary>
[TestClass]
public class SiblingMatchingTests
{
	private static JsonSerializerOptions CreateOptions() =>
		new() { Converters = { new JsonStringEnumConverter(), new JsonRequiredConditionallyConverterFactory() } };

	[TestMethod]
	public void IntegerAttributeArgumentMatchesLongSibling()
	{
		JsonRequiredConditionallyException exception = Assert.ThrowsExactly<JsonRequiredConditionallyException>(
			() => JsonSerializer.Deserialize<LongSiblingConfig>("""{"Count":1}""", CreateOptions()));

		CollectionAssert.AreEquivalent(new List<string> { "Detail" }, exception.MissingProperties.ToList());
	}

	[TestMethod]
	public void IntegerAttributeArgumentMatchesShortSibling()
	{
		Assert.ThrowsExactly<JsonRequiredConditionallyException>(
			() => JsonSerializer.Deserialize<ShortSiblingConfig>("""{"Count":1}""", CreateOptions()));
	}

	[TestMethod]
	public void IntegerAttributeArgumentMatchesByteSibling()
	{
		Assert.ThrowsExactly<JsonRequiredConditionallyException>(
			() => JsonSerializer.Deserialize<ByteSiblingConfig>("""{"Count":1}""", CreateOptions()));
	}

	[TestMethod]
	public void WidenedSiblingStillDistinguishesNonMatchingValues()
	{
		LongSiblingConfig? config = JsonSerializer.Deserialize<LongSiblingConfig>(
			"""{"Count":2}""", CreateOptions());

		Assert.IsNotNull(config);
		Assert.IsNull(config.Detail);
	}

	[TestMethod]
	public void EnumMemberNameAsStringMatchesEnumSibling()
	{
		Assert.ThrowsExactly<JsonRequiredConditionallyException>(
			() => JsonSerializer.Deserialize<EnumNameSiblingConfig>("""{"Kind":"Advanced"}""", CreateOptions()));
	}

	[TestMethod]
	public void EnumMemberNameAsStringStillDistinguishesNonMatchingValues()
	{
		EnumNameSiblingConfig? config = JsonSerializer.Deserialize<EnumNameSiblingConfig>(
			"""{"Kind":"Expert"}""", CreateOptions());

		Assert.IsNotNull(config);
		Assert.IsNull(config.Tuning);
	}

	[TestMethod]
	public void UnconvertibleAttributeValueThrowsOnFirstUse()
	{
		InvalidOperationException exception = Assert.ThrowsExactly<InvalidOperationException>(
			() => JsonSerializer.Deserialize<UnconvertibleSiblingConfig>(
				"""{"Id":"00000000-0000-0000-0000-000000000000"}""", CreateOptions()));

		StringAssert.Contains(exception.Message, nameof(UnconvertibleSiblingConfig));
		StringAssert.Contains(exception.Message, nameof(UnconvertibleSiblingConfig.Id));
		StringAssert.Contains(exception.Message, nameof(Guid));
	}
}
