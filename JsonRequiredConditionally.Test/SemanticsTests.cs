// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.JsonRequiredConditionally.Tests;

using System.Text.Json;
using System.Text.Json.Serialization;

[TestClass]
public class SemanticsTests
{
	private static JsonSerializerOptions CreateOptions() =>
		new()
		{
			Converters =
			{
				new JsonStringEnumConverter(),
				new JsonRequiredConditionallyConverterFactory(),
			},
		};

	[TestMethod]
	public void RecordWithMissingRequiredPropertyThrows()
	{
		Assert.ThrowsExactly<JsonRequiredConditionallyException>(
			() => JsonSerializer.Deserialize<RecordConfig>("""{"Kind":"Advanced"}""", CreateOptions()));
	}

	[TestMethod]
	public void RecordWithPresentPropertyDeserializes()
	{
		RecordConfig? config = JsonSerializer.Deserialize<RecordConfig>(
			"""{"Kind":"Advanced","Tuning":"fast"}""", CreateOptions());

		Assert.IsNotNull(config);
		Assert.AreEqual("fast", config.Tuning);
	}

	[TestMethod]
	public void EveryViolationIsReportedInOneException()
	{
		JsonRequiredConditionallyException exception = Assert.ThrowsExactly<JsonRequiredConditionallyException>(
			() => JsonSerializer.Deserialize<MultiViolationConfig>("""{"Kind":"Advanced"}""", CreateOptions()));

		Assert.HasCount(2, exception.MissingProperties);
		CollectionAssert.Contains(exception.MissingProperties.ToList(), "Tuning");
		CollectionAssert.Contains(exception.MissingProperties.ToList(), "Host");
	}

	[TestMethod]
	public void AbsentSiblingReadsAsClrDefaultAndMatchesZeroValuedEnum()
	{
		Assert.ThrowsExactly<JsonRequiredConditionallyException>(
			() => JsonSerializer.Deserialize<ZeroValueConfig>("{}", CreateOptions()));
	}

	[TestMethod]
	public void NullSiblingMatchesNullCondition()
	{
		Assert.ThrowsExactly<JsonRequiredConditionallyException>(
			() => JsonSerializer.Deserialize<NullSiblingConfig>("""{"Kind":null}""", CreateOptions()));

		NullSiblingConfig? config = JsonSerializer.Deserialize<NullSiblingConfig>(
			"""{"Kind":"set"}""", CreateOptions());

		Assert.IsNotNull(config);
	}

	[TestMethod]
	public void UnresolvableSiblingSurfacesOnFirstUse()
	{
		Assert.ThrowsExactly<InvalidOperationException>(
			() => JsonSerializer.Deserialize<BrokenConfig>("""{"Kind":"Advanced"}""", CreateOptions()));
	}

	[TestMethod]
	public void PresenceAloneNoLongerSatisfiesANotEmptyMember()
	{
		JsonRequiredConditionallyException exception = Assert.ThrowsExactly<JsonRequiredConditionallyException>(
			() => JsonSerializer.Deserialize<NotEmptyStringConfig>(/*lang=json,strict*/ """{"Name":null}""", CreateOptions()));

		CollectionAssert.AreEqual(new List<string> { "Name" }, exception.EmptyProperties.ToList());
	}

	[TestMethod]
	public void NotEmptyDoesNothingWhenTheFactoryIsNotRegistered()
	{
		NotEmptyStringConfig? config = JsonSerializer.Deserialize<NotEmptyStringConfig>(
			/*lang=json,strict*/ "{}", JsonSerializerOptions.Default);

		Assert.IsNotNull(config);
		Assert.IsNull(config.Name);
	}

	[TestMethod]
	public void WriteIsNotValidated()
	{
		NotEmptyStringConfig config = new() { Name = string.Empty };

		string json = JsonSerializer.Serialize(config, CreateOptions());

		StringAssert.Contains(json, "Name");
	}
}
