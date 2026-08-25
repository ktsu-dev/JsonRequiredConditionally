// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.JsonRequiredConditionally.Tests;

using System.Text.Json;
using System.Text.Json.Serialization;

/// <summary>
/// Covers <see cref="JsonRequiredAndNotEmptyAttribute"/> end to end: every payload shape against
/// every member shape.
/// </summary>
[TestClass]
public class NotEmptyTests
{
	private static JsonSerializerOptions CreateOptions() =>
		new() { Converters = { new JsonStringEnumConverter(), new JsonRequiredConditionallyConverterFactory() } };

	private static JsonRequiredConditionallyException Throws<T>(string json) =>
		Assert.ThrowsExactly<JsonRequiredConditionallyException>(
			() => JsonSerializer.Deserialize<T>(json, CreateOptions()));

	[TestMethod]
	public void AbsentStringIsReportedAsMissing()
	{
		JsonRequiredConditionallyException exception = Throws<NotEmptyStringConfig>(/*lang=json,strict*/ "{}");

		CollectionAssert.AreEqual(new List<string> { "Name" }, exception.MissingProperties.ToList());
		Assert.IsEmpty(exception.EmptyProperties);
	}

	[TestMethod]
	public void NullStringIsReportedAsEmpty()
	{
		JsonRequiredConditionallyException exception = Throws<NotEmptyStringConfig>(/*lang=json,strict*/ """{"Name":null}""");

		Assert.IsEmpty(exception.MissingProperties);
		CollectionAssert.AreEqual(new List<string> { "Name" }, exception.EmptyProperties.ToList());
	}

	[TestMethod]
	public void ZeroLengthStringIsReportedAsEmpty()
	{
		JsonRequiredConditionallyException exception = Throws<NotEmptyStringConfig>(/*lang=json,strict*/ """{"Name":""}""");

		CollectionAssert.AreEqual(new List<string> { "Name" }, exception.EmptyProperties.ToList());
	}

	[TestMethod]
	public void WhitespaceStringIsAccepted()
	{
		NotEmptyStringConfig? config = JsonSerializer.Deserialize<NotEmptyStringConfig>(
			/*lang=json,strict*/ """{"Name":"   "}""", CreateOptions());

		Assert.IsNotNull(config);
		Assert.AreEqual("   ", config.Name);
	}

	[TestMethod]
	public void PopulatedStringIsAccepted()
	{
		NotEmptyStringConfig? config = JsonSerializer.Deserialize<NotEmptyStringConfig>(
			/*lang=json,strict*/ """{"Name":"x"}""", CreateOptions());

		Assert.IsNotNull(config);
		Assert.AreEqual("x", config.Name);
	}

	[TestMethod]
	public void EmptyListIsReportedAsEmpty()
	{
		JsonRequiredConditionallyException exception = Throws<NotEmptyListConfig>(/*lang=json,strict*/ """{"Items":[]}""");

		CollectionAssert.AreEqual(new List<string> { "Items" }, exception.EmptyProperties.ToList());
	}

	[TestMethod]
	public void PopulatedListIsAccepted()
	{
		NotEmptyListConfig? config = JsonSerializer.Deserialize<NotEmptyListConfig>(
			/*lang=json,strict*/ """{"Items":["a"]}""", CreateOptions());

		Assert.IsNotNull(config);
		Assert.HasCount(1, config.Items!);
	}

	[TestMethod]
	public void ListOfOneEmptyStringIsAccepted()
	{
		NotEmptyListConfig? config = JsonSerializer.Deserialize<NotEmptyListConfig>(
			/*lang=json,strict*/ """{"Items":[""]}""", CreateOptions());

		Assert.IsNotNull(config);
		Assert.HasCount(1, config.Items!);
	}

	[TestMethod]
	public void EmptySetIsReportedAsEmpty()
	{
		JsonRequiredConditionallyException exception = Throws<NotEmptySetConfig>(/*lang=json,strict*/ """{"Tags":[]}""");

		CollectionAssert.AreEqual(new List<string> { "Tags" }, exception.EmptyProperties.ToList());
	}

	[TestMethod]
	public void EmptyArrayMemberIsReportedAsEmpty()
	{
		JsonRequiredConditionallyException exception = Throws<NotEmptyArrayConfig>(/*lang=json,strict*/ """{"Values":[]}""");

		CollectionAssert.AreEqual(new List<string> { "Values" }, exception.EmptyProperties.ToList());
	}

	[TestMethod]
	public void EmptyDictionaryIsReportedAsEmpty()
	{
		JsonRequiredConditionallyException exception = Throws<NotEmptyDictionaryConfig>(/*lang=json,strict*/ """{"Lookup":{}}""");

		CollectionAssert.AreEqual(new List<string> { "Lookup" }, exception.EmptyProperties.ToList());
	}

	[TestMethod]
	public void PopulatedDictionaryIsAccepted()
	{
		NotEmptyDictionaryConfig? config = JsonSerializer.Deserialize<NotEmptyDictionaryConfig>(
			/*lang=json,strict*/ """{"Lookup":{"a":"b"}}""", CreateOptions());

		Assert.IsNotNull(config);
		Assert.HasCount(1, config.Lookup!);
	}

	[TestMethod]
	public void NullListIsReportedAsEmpty()
	{
		JsonRequiredConditionallyException exception = Throws<NotEmptyListConfig>(/*lang=json,strict*/ """{"Items":null}""");

		CollectionAssert.AreEqual(new List<string> { "Items" }, exception.EmptyProperties.ToList());
	}

	[TestMethod]
	public void NullDictionaryIsReportedAsEmpty()
	{
		JsonRequiredConditionallyException exception = Throws<NotEmptyDictionaryConfig>(/*lang=json,strict*/ """{"Lookup":null}""");

		CollectionAssert.AreEqual(new List<string> { "Lookup" }, exception.EmptyProperties.ToList());
	}

	[TestMethod]
	public void NullBehindACustomConverterIsReportedAsEmpty()
	{
		JsonRequiredConditionallyException exception = Throws<NotEmptyConvertedConfig>(/*lang=json,strict*/ """{"Label":null}""");

		CollectionAssert.AreEqual(new List<string> { "Label" }, exception.EmptyProperties.ToList());
	}

	[TestMethod]
	public void EmptyStringBehindACustomConverterIsReportedAsEmpty()
	{
		JsonRequiredConditionallyException exception = Throws<NotEmptyConvertedConfig>(/*lang=json,strict*/ """{"Label":""}""");

		CollectionAssert.AreEqual(new List<string> { "Label" }, exception.EmptyProperties.ToList());
	}

	[TestMethod]
	public void PopulatedStringBehindACustomConverterIsAccepted()
	{
		NotEmptyConvertedConfig? config = JsonSerializer.Deserialize<NotEmptyConvertedConfig>(
			/*lang=json,strict*/ """{"Label":"x"}""", CreateOptions());

		Assert.IsNotNull(config);
		Assert.AreEqual("x", config.Label!.Value);
	}

	[TestMethod]
	public void PresentNumberIsAlwaysAccepted()
	{
		NotEmptyIntConfig? config = JsonSerializer.Deserialize<NotEmptyIntConfig>(
			/*lang=json,strict*/ """{"Count":0}""", CreateOptions());

		Assert.IsNotNull(config);
		Assert.AreEqual(0, config.Count);
	}

	[TestMethod]
	public void AbsentNumberIsStillReportedAsMissing()
	{
		JsonRequiredConditionallyException exception = Throws<NotEmptyIntConfig>(/*lang=json,strict*/ "{}");

		CollectionAssert.AreEqual(new List<string> { "Count" }, exception.MissingProperties.ToList());
	}

	[TestMethod]
	public void NullNullableNumberIsReportedAsEmpty()
	{
		JsonRequiredConditionallyException exception = Throws<NotEmptyNullableIntConfig>(/*lang=json,strict*/ """{"Count":null}""");

		CollectionAssert.AreEqual(new List<string> { "Count" }, exception.EmptyProperties.ToList());
	}

	[TestMethod]
	public void PresentNullableNumberIsAccepted()
	{
		NotEmptyNullableIntConfig? config = JsonSerializer.Deserialize<NotEmptyNullableIntConfig>(
			/*lang=json,strict*/ """{"Count":0}""", CreateOptions());

		Assert.IsNotNull(config);
		Assert.AreEqual(0, config.Count);
	}
}
