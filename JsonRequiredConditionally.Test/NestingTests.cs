// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace ktsu.JsonRequiredConditionally.Tests;

using System.Text.Json;
using System.Text.Json.Serialization;

[TestClass]
public class NestingTests
{
	private static JsonSerializerOptions CreateOptions() =>
		new() { Converters = { new JsonStringEnumConverter(), new JsonRequiredConditionallyConverterFactory() } };

	[TestMethod]
	public void NestedObjectIsValidated()
	{
		Assert.ThrowsExactly<JsonRequiredConditionallyException>(
			() => JsonSerializer.Deserialize<OuterConfig>(
				"""{"Label":"x","Child":{"Kind":"Advanced"}}""", CreateOptions()));
	}

	[TestMethod]
	public void ValidNestedObjectDeserializes()
	{
		OuterConfig? outer = JsonSerializer.Deserialize<OuterConfig>(
			"""{"Label":"x","Child":{"Kind":"Advanced","Tuning":"fast"}}""", CreateOptions());

		Assert.IsNotNull(outer);
		Assert.AreEqual("fast", outer.Child!.Tuning);
	}

	[TestMethod]
	public void ListElementsAreValidated()
	{
		Assert.ThrowsExactly<JsonRequiredConditionallyException>(
			() => JsonSerializer.Deserialize<CollectionConfig>(
				"""{"Items":[{"Kind":"Basic"},{"Kind":"Advanced"}],"Lookup":{}}""", CreateOptions()));
	}

	[TestMethod]
	public void DictionaryValuesAreValidated()
	{
		Assert.ThrowsExactly<JsonRequiredConditionallyException>(
			() => JsonSerializer.Deserialize<CollectionConfig>(
				"""{"Items":[],"Lookup":{"a":{"Kind":"Advanced"}}}""", CreateOptions()));
	}

	[TestMethod]
	public void CyclicTypeGraphValidatesAtEveryLevel()
	{
		string json = """
			{"Kind":"Basic","Next":{"Mode":"Local","Next":{"Kind":"Advanced"}}}
			""";

		Assert.ThrowsExactly<JsonRequiredConditionallyException>(
			() => JsonSerializer.Deserialize<NodeA>(json, CreateOptions()));
	}

	[TestMethod]
	public void DeeplyNestedValidGraphDeserializes()
	{
		string json = """
			{"Kind":"Advanced","Tuning":"a","Next":{"Mode":"Remote","Endpoint":"b","Next":{"Kind":"Basic"}}}
			""";

		NodeA? node = JsonSerializer.Deserialize<NodeA>(json, CreateOptions());

		Assert.IsNotNull(node);
		Assert.AreEqual("b", node.Next!.Endpoint);
	}

	[TestMethod]
	public void InnerOptionsAreCachedPerRootAndExcludedType()
	{
		JsonSerializerOptions root = CreateOptions();
		JsonRequiredConditionallyConverterFactory factory = new();

		JsonSerializerOptions first = InnerOptionsCache.Get(root, typeof(SimpleConfig), factory);
		JsonSerializerOptions second = InnerOptionsCache.Get(root, typeof(SimpleConfig), factory);
		JsonSerializerOptions other = InnerOptionsCache.Get(root, typeof(OrConfig), factory);

		Assert.AreSame(first, second);
		Assert.AreNotSame(first, other);
	}
}
