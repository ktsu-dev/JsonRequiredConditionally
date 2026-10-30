// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.JsonRequiredConditionally.Tests;

using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// A list, array or dictionary at the root of the payload is claimed itself, so every failing
/// element is reported in one exception with its index or key.
/// </summary>
[TestClass]
public class RootCollectionTests
{
	private const string ThreeConfigs = """[{"Kind":"Advanced"},{"Kind":"Basic"},{"Kind":"Advanced"}]""";

	private static JsonSerializerOptions CreateOptions() =>
		new() { Converters = { new JsonStringEnumConverter(), new JsonRequiredConditionallyConverterFactory() } };

	[TestMethod]
	public void RootListReportsEveryFailingElementWithItsIndex()
	{
		JsonRequiredConditionallyException exception = Assert.ThrowsExactly<JsonRequiredConditionallyException>(
			() => JsonSerializer.Deserialize<List<SimpleConfig>>(ThreeConfigs, CreateOptions()));

		Assert.AreSequenceEqual(new List<string> { "[0].Tuning", "[2].Tuning" }, [.. exception.MissingProperties], SequenceOrder.InAnyOrder);
	}

	[TestMethod]
	public void RootArrayReportsEveryFailingElementWithItsIndex()
	{
		JsonRequiredConditionallyException exception = Assert.ThrowsExactly<JsonRequiredConditionallyException>(
			() => JsonSerializer.Deserialize<SimpleConfig[]>(ThreeConfigs, CreateOptions()));

		Assert.AreSequenceEqual(new List<string> { "[0].Tuning", "[2].Tuning" }, [.. exception.MissingProperties], SequenceOrder.InAnyOrder);
	}

	[TestMethod]
	public void RootDictionaryReportsEveryFailingValueWithItsKey()
	{
		JsonRequiredConditionallyException exception = Assert.ThrowsExactly<JsonRequiredConditionallyException>(
			() => JsonSerializer.Deserialize<Dictionary<string, SimpleConfig>>(
				"""{"a":{"Kind":"Advanced"},"b":{"Kind":"Advanced"}}""", CreateOptions()));

		Assert.AreSequenceEqual(new List<string> { "a.Tuning", "b.Tuning" }, [.. exception.MissingProperties], SequenceOrder.InAnyOrder);
	}

	[TestMethod]
	public void RootNestedListReportsBothIndexes()
	{
		JsonRequiredConditionallyException exception = Assert.ThrowsExactly<JsonRequiredConditionallyException>(
			() => JsonSerializer.Deserialize<List<List<SimpleConfig>>>(
				"""[[{"Kind":"Basic"}],[{"Kind":"Basic"},{"Kind":"Advanced"}]]""", CreateOptions()));

		Assert.AreSequenceEqual(new List<string> { "[1][1].Tuning" }, [.. exception.MissingProperties], SequenceOrder.InAnyOrder);
	}

	[TestMethod]
	public void ValidRootListDeserializes()
	{
		List<SimpleConfig>? value = JsonSerializer.Deserialize<List<SimpleConfig>>(
			"""[{"Kind":"Advanced","Tuning":"fast"},{"Kind":"Basic"}]""", CreateOptions());

		Assert.IsNotNull(value);
		Assert.HasCount(2, value);
		Assert.AreEqual("fast", value[0].Tuning);
	}

	[TestMethod]
	public void RootListRoundTrips()
	{
		List<SimpleConfig> original = [new() { Kind = Kind.Advanced, Tuning = "fast" }];

		string json = JsonSerializer.Serialize(original, CreateOptions());
		List<SimpleConfig>? value = JsonSerializer.Deserialize<List<SimpleConfig>>(json, CreateOptions());

		Assert.IsNotNull(value);
		Assert.AreEqual("fast", value[0].Tuning);
	}

	[TestMethod]
	public void ListsArraysAndDictionariesOfDecoratedTypesAreClaimed()
	{
		Assert.IsTrue(RequirementRuleCompiler.HasRules(typeof(List<SimpleConfig>)));
		Assert.IsTrue(RequirementRuleCompiler.HasRules(typeof(SimpleConfig[])));
		Assert.IsTrue(RequirementRuleCompiler.HasRules(typeof(Dictionary<string, SimpleConfig>)));
		Assert.IsTrue(RequirementRuleCompiler.HasRules(typeof(Dictionary<string, List<SimpleConfig>>)));
	}

	[TestMethod]
	public void CollectionsThatCannotBePairedAreNotClaimed()
	{
		// Sets and stacks do not keep payload order, so the walk does not descend into them. Claiming
		// one would materialize its elements factory-free and leave them unvalidated, so each element
		// stays claimed on its own instead.
		Assert.IsFalse(RequirementRuleCompiler.HasRules(typeof(HashSet<SimpleConfig>)));
		Assert.IsFalse(RequirementRuleCompiler.HasRules(typeof(Stack<SimpleConfig>)));
		Assert.IsFalse(RequirementRuleCompiler.HasRules(typeof(List<HashSet<SimpleConfig>>)));
		Assert.IsFalse(RequirementRuleCompiler.HasRules(typeof(IEnumerable<SimpleConfig>)));
	}

	[TestMethod]
	public void CollectionsOfUndecoratedTypesAreNotClaimed()
	{
		Assert.IsFalse(RequirementRuleCompiler.HasRules(typeof(List<PlainConfig>)));
		Assert.IsFalse(RequirementRuleCompiler.HasRules(typeof(Dictionary<string, int>)));
		Assert.IsFalse(RequirementRuleCompiler.HasRules(typeof(List<SelfListing>)));
	}

	[TestMethod]
	public void RootSetStillValidatesEachElementOnItsOwn()
	{
		Assert.ThrowsExactly<JsonRequiredConditionallyException>(
			() => JsonSerializer.Deserialize<HashSet<SimpleConfig>>(ThreeConfigs, CreateOptions()));
	}

	/// <summary>A list that holds itself, to check the collection search ends.</summary>
	public sealed class SelfListing : List<SelfListing>;
}
