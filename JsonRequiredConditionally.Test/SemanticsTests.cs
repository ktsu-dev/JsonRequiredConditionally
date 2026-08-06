// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

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
}
