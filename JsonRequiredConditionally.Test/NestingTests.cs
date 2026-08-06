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
	public void SelfReferentialChildIsValidated()
	{
		Assert.ThrowsExactly<JsonRequiredConditionallyException>(
			() => JsonSerializer.Deserialize<TreeNode>(
				"""{"Kind":"Basic","Child":{"Kind":"Advanced"}}""", CreateOptions()));
	}

	[TestMethod]
	public void SelfReferentialGrandchildIsValidated()
	{
		Assert.ThrowsExactly<JsonRequiredConditionallyException>(
			() => JsonSerializer.Deserialize<TreeNode>(
				"""{"Kind":"Basic","Child":{"Kind":"Basic","Child":{"Kind":"Advanced"}}}""", CreateOptions()));
	}

	[TestMethod]
	public void ValidSelfReferentialGraphDeserializes()
	{
		TreeNode? node = JsonSerializer.Deserialize<TreeNode>(
			"""{"Kind":"Advanced","Tuning":"a","Child":{"Kind":"Advanced","Tuning":"b"}}""", CreateOptions());

		Assert.IsNotNull(node);
		Assert.AreEqual("b", node.Child!.Tuning);
	}

	[TestMethod]
	public void SelfReferenceThroughCollectionIsValidated()
	{
		Assert.ThrowsExactly<JsonRequiredConditionallyException>(
			() => JsonSerializer.Deserialize<BranchNode>(
				"""{"Kind":"Basic","Children":[{"Kind":"Basic","Children":[]},{"Kind":"Advanced","Children":[]}]}""",
				CreateOptions()));
	}

	[TestMethod]
	public void ViolationsAtDifferentDepthsAggregateIntoOneException()
	{
		JsonRequiredConditionallyException exception = Assert.ThrowsExactly<JsonRequiredConditionallyException>(
			() => JsonSerializer.Deserialize<TreeNode>(
				"""{"Kind":"Advanced","Child":{"Kind":"Advanced"}}""", CreateOptions()));

		Assert.HasCount(2, exception.MissingProperties);
	}
}
