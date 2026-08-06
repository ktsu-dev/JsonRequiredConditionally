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
		JsonRequiredConditionallyException exception = Assert.ThrowsExactly<JsonRequiredConditionallyException>(
			() => JsonSerializer.Deserialize<CollectionConfig>(
				"""{"Items":[{"Kind":"Basic"},{"Kind":"Advanced"}],"Lookup":{}}""", CreateOptions()));

		CollectionAssert.AreEqual(new List<string> { "Items[1].Tuning" }, exception.MissingProperties.ToList());
	}

	[TestMethod]
	public void DictionaryValuesAreValidated()
	{
		JsonRequiredConditionallyException exception = Assert.ThrowsExactly<JsonRequiredConditionallyException>(
			() => JsonSerializer.Deserialize<CollectionConfig>(
				"""{"Items":[],"Lookup":{"a":{"Kind":"Advanced"}}}""", CreateOptions()));

		CollectionAssert.AreEqual(new List<string> { "Lookup.a.Tuning" }, exception.MissingProperties.ToList());
	}

	[TestMethod]
	public void IntKeyedDictionaryValuesAreValidated()
	{
		JsonRequiredConditionallyException exception = Assert.ThrowsExactly<JsonRequiredConditionallyException>(
			() => JsonSerializer.Deserialize<IntKeyedDictionaryConfig>(
				"""{"Items":{"1":{"Kind":"Advanced"}}}""", CreateOptions()));

		CollectionAssert.AreEqual(new List<string> { "Items.1.Tuning" }, exception.MissingProperties.ToList());
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
		JsonRequiredConditionallyException exception = Assert.ThrowsExactly<JsonRequiredConditionallyException>(
			() => JsonSerializer.Deserialize<BranchNode>(
				"""{"Kind":"Basic","Children":[{"Kind":"Basic","Children":[]},{"Kind":"Advanced","Children":[]}]}""",
				CreateOptions()));

		CollectionAssert.AreEqual(new List<string> { "Children[1].Tuning" }, exception.MissingProperties.ToList());
	}

	[TestMethod]
	public void ViolationsAtDifferentDepthsAggregateIntoOneException()
	{
		JsonRequiredConditionallyException exception = Assert.ThrowsExactly<JsonRequiredConditionallyException>(
			() => JsonSerializer.Deserialize<TreeNode>(
				"""{"Kind":"Advanced","Child":{"Kind":"Advanced"}}""", CreateOptions()));

		CollectionAssert.AreEqual(new List<string> { "Tuning", "Child.Tuning" }, exception.MissingProperties.ToList());
	}

	[TestMethod]
	public void ObjectTypedMemberWithIndexerPropertyDoesNotCrash()
	{
		PayloadConfig? config = JsonSerializer.Deserialize<PayloadConfig>(
			"""{"Kind":"Basic","Payload":{"a":1}}""", CreateOptions());

		Assert.IsNotNull(config);
	}

	[TestMethod]
	public void JsonIgnoredMemberIsNotDescendedEvenWhenNameCollidesWithRealJsonProperty()
	{
		IgnoredMemberConfig? config = JsonSerializer.Deserialize<IgnoredMemberConfig>(
			"""{"Kind":"Basic","Hidden":{"Kind":"Basic"}}""", CreateOptions());

		Assert.IsNotNull(config);
		Assert.AreEqual(Kind.Advanced, config.Hidden.Kind);
	}

	[TestMethod]
	public void CaseInsensitiveOptionsValidateNestedObjects()
	{
		JsonSerializerOptions options = new()
		{
			PropertyNameCaseInsensitive = true,
			Converters = { new JsonStringEnumConverter(), new JsonRequiredConditionallyConverterFactory() },
		};

		Assert.ThrowsExactly<JsonRequiredConditionallyException>(
			() => JsonSerializer.Deserialize<OuterConfig>(
				"""{"label":"x","child":{"kind":"Advanced"}}""", options));
	}
}
