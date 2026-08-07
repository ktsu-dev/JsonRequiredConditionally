// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.JsonRequiredConditionally.Tests;

using System.Text.Json;
using System.Text.Json.Serialization;

[TestClass]
public class NestingTests
{
	private static JsonSerializerOptions CreateOptions() =>
		new() { Converters = { new JsonStringEnumConverter(), new JsonRequiredConditionallyConverterFactory() } };

	private static JsonSerializerOptions CreatePlainOptions() =>
		new() { Converters = { new JsonStringEnumConverter() } };

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

		CollectionAssert.AreEquivalent(new List<string> { "Items[1].Tuning" }, exception.MissingProperties.ToList());
	}

	[TestMethod]
	public void DictionaryValuesAreValidated()
	{
		JsonRequiredConditionallyException exception = Assert.ThrowsExactly<JsonRequiredConditionallyException>(
			() => JsonSerializer.Deserialize<CollectionConfig>(
				"""{"Items":[],"Lookup":{"a":{"Kind":"Advanced"}}}""", CreateOptions()));

		CollectionAssert.AreEquivalent(new List<string> { "Lookup.a.Tuning" }, exception.MissingProperties.ToList());
	}

	[TestMethod]
	public void IntKeyedDictionaryValuesAreValidated()
	{
		JsonRequiredConditionallyException exception = Assert.ThrowsExactly<JsonRequiredConditionallyException>(
			() => JsonSerializer.Deserialize<IntKeyedDictionaryConfig>(
				"""{"Items":{"1":{"Kind":"Advanced"}}}""", CreateOptions()));

		CollectionAssert.AreEquivalent(new List<string> { "Items.1.Tuning" }, exception.MissingProperties.ToList());
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

		CollectionAssert.AreEquivalent(new List<string> { "Children[1].Tuning" }, exception.MissingProperties.ToList());
	}

	[TestMethod]
	public void ViolationsAtDifferentDepthsAggregateIntoOneException()
	{
		JsonRequiredConditionallyException exception = Assert.ThrowsExactly<JsonRequiredConditionallyException>(
			() => JsonSerializer.Deserialize<TreeNode>(
				"""{"Kind":"Advanced","Child":{"Kind":"Advanced"}}""", CreateOptions()));

		CollectionAssert.AreEquivalent(new List<string> { "Tuning", "Child.Tuning" }, exception.MissingProperties.ToList());
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
		// IgnoredMemberConfig carries its own directly-decorated Tuning, so it is genuinely claimed
		// by the factory -- otherwise this test would pass identically whether or not [JsonIgnore]
		// is honoured, since the converter would never run at all. Kind=Advanced with Tuning
		// present satisfies the *real* requirement; the only way this throws is if the walk still
		// descends into the ignored "Hidden" member and validates its default value (Kind=Advanced)
		// against the "Hidden" JSON node's own (Tuning-less) content.
		IgnoredMemberConfig? config = JsonSerializer.Deserialize<IgnoredMemberConfig>(
			"""{"Kind":"Advanced","Tuning":"x","Hidden":{"Kind":"Basic"}}""", CreateOptions());

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

	[TestMethod]
	public void JsonIncludedNonPublicMemberIsValidated()
	{
		const string json = """{"Public":{"Kind":"Basic"},"Secret":{"Kind":"Advanced"}}""";

		if (!SerializerCapabilities.SupportsJsonIncludeOnNonPublicProperties)
		{
			AssertLibraryDoesNotMaskTheContractRejection(
				() => JsonSerializer.Deserialize<IncludedNonPublicMemberConfig>(json, CreatePlainOptions()),
				() => JsonSerializer.Deserialize<IncludedNonPublicMemberConfig>(json, CreateOptions()));

			return;
		}

		Assert.ThrowsExactly<JsonRequiredConditionallyException>(
			() => JsonSerializer.Deserialize<IncludedNonPublicMemberConfig>(json, CreateOptions()));
	}

	[TestMethod]
	public void PublicFieldUnderDefaultOptionsIsNotValidated()
	{
		// Under default options (IncludeFields = false), System.Text.Json never writes into the
		// Child field; it keeps its initializer value (Kind=Advanced, Tuning=null). A JSON "Child"
		// node present in the payload is therefore irrelevant to it -- validating the field's
		// current value against that unrelated node would be a false positive.
		FieldHolderConfig? holder = JsonSerializer.Deserialize<FieldHolderConfig>(
			"""{"Kind":"Basic","Child":{"Kind":"Basic"}}""", CreateOptions());

		Assert.IsNotNull(holder);
		Assert.AreEqual(Kind.Advanced, holder.Child.Kind);
	}

	[TestMethod]
	public void GetOnlyPropertyIsNotValidated()
	{
		// A get-only property has no setter, so System.Text.Json never populates it either; it
		// keeps its initializer value. Same false-positive shape as the field case above.
		ReadOnlyPropertyConfig? holder = JsonSerializer.Deserialize<ReadOnlyPropertyConfig>(
			"""{"Kind":"Basic","ReadOnlyChild":{"Kind":"Basic"}}""", CreateOptions());

		Assert.IsNotNull(holder);
		Assert.AreEqual(Kind.Advanced, holder.ReadOnlyChild.Kind);
	}

	[TestMethod]
	public void HiddenObjectValuedMemberIsValidatedOnce()
	{
		JsonRequiredConditionallyException exception = Assert.ThrowsExactly<JsonRequiredConditionallyException>(
			() => JsonSerializer.Deserialize<HidingObjectConfig>(
				"""{"Payload":{"Kind":"Advanced"}}""", CreateOptions()));

		CollectionAssert.AreEquivalent(new List<string> { "Payload.Tuning" }, exception.MissingProperties.ToList());
	}

	[TestMethod]
	public void ConstructorBoundPropertyIsValidated()
	{
		// Child is populated via CtorHolder's constructor, not a setter (Set == null on its
		// JsonPropertyInfo). System.Text.Json genuinely builds it from the "Child" JSON, so its
		// Tuning requirement is real and must be caught, not silently skipped.
		Assert.ThrowsExactly<JsonRequiredConditionallyException>(
			() => JsonSerializer.Deserialize<CtorHolder>(
				"""{"Kind":"Basic","Child":{"Kind":"Advanced"}}""", CreateOptions()));
	}

	[TestMethod]
	public void ConstructorBoundPropertyAsOnlyPathToDecoratedTypeKeepsPathPrefix()
	{
		// CtorOnlyPathHolder has no directly-decorated member of its own; Child (constructor-bound)
		// is its only route to a decorated type. If constructor-bound properties were excluded from
		// eligibility's reachable-member enumeration, this container would never be claimed at all,
		// and the violation -- if caught independently by some other means -- would lose its path
		// prefix. It must be claimed, walked, and the prefix must survive.
		JsonRequiredConditionallyException exception = Assert.ThrowsExactly<JsonRequiredConditionallyException>(
			() => JsonSerializer.Deserialize<CtorOnlyPathHolder>(
				"""{"Child":{"Kind":"Advanced"}}""", CreateOptions()));

		CollectionAssert.AreEquivalent(new List<string> { "Child.Tuning" }, exception.MissingProperties.ToList());
	}

	[TestMethod]
	public void InternalJsonIncludedDecoratedMemberIsClaimedAndValidated()
	{
		// InternalDecoratedMemberConfig's only decorated member is internal, made visible to
		// System.Text.Json via [JsonInclude]. Rule compilation and eligibility must both see it via
		// the same JsonTypeInfo-based member model the walk uses, not raw Public|Instance
		// reflection, or this type would never be claimed and would deserialize with no validation.
		const string json = """{"Kind":"Advanced"}""";

		if (!SerializerCapabilities.SupportsJsonIncludeOnNonPublicProperties)
		{
			AssertLibraryDoesNotMaskTheContractRejection(
				() => JsonSerializer.Deserialize<InternalDecoratedMemberConfig>(json, CreatePlainOptions()),
				() => JsonSerializer.Deserialize<InternalDecoratedMemberConfig>(json, CreateOptions()));

			return;
		}

		Assert.ThrowsExactly<JsonRequiredConditionallyException>(
			() => JsonSerializer.Deserialize<InternalDecoratedMemberConfig>(json, CreateOptions()));
	}

	/// <summary>
	/// Asserts that registering this library changes nothing about a type System.Text.Json itself
	/// refuses to build a contract for.
	/// </summary>
	/// <param name="withoutFactory">Deserialization through options that do not carry the factory.</param>
	/// <param name="withFactory">The same deserialization through options that do.</param>
	/// <remarks>
	/// This is the assertion that carries the weight on System.Text.Json 7, where <c>[JsonInclude]</c>
	/// on a non-public property is rejected while the contract is built -- before any converter is
	/// consulted. The scenario the caller wanted to test does not exist on that framework, but the
	/// library still owes it not to swallow, wrap or alter the serializer's own error, and that is
	/// what is checked here.
	/// </remarks>
	private static void AssertLibraryDoesNotMaskTheContractRejection(Action withoutFactory, Action withFactory)
	{
		InvalidOperationException direct = Assert.ThrowsExactly<InvalidOperationException>(withoutFactory);
		InvalidOperationException claimed = Assert.ThrowsExactly<InvalidOperationException>(withFactory);

		Assert.AreEqual(direct.Message, claimed.Message);
		StringAssert.Contains(claimed.Message, "JsonIncludeAttribute");
	}

	[TestMethod]
	public void JsonConstructorPreferredOverConvenienceConstructorLeavesPropertyUntouched()
	{
		// A naive "does any public constructor have a same-named parameter" check would wrongly
		// treat Child as constructor-bound because of the convenience overload -- even though
		// System.Text.Json actually uses the [JsonConstructor]-marked parameterless constructor,
		// which never touches Child at all. Kind=Basic keeps the top-level Tuning requirement out
		// of the way, isolating the assertion to Child's own (false) requirement.
		JsonConstructorPreferredHolder? holder = JsonSerializer.Deserialize<JsonConstructorPreferredHolder>(
			"""{"Kind":"Basic","Child":{"Kind":"Basic"}}""", CreateOptions());

		Assert.IsNotNull(holder);
		Assert.AreEqual(Kind.Advanced, holder.Child.Kind);
	}

	[TestMethod]
	public void ParameterlessConstructorPreferredOverConvenienceConstructorLeavesPropertyUntouched()
	{
		// Same shape as above, but relying on System.Text.Json's default preference for a public
		// parameterless constructor rather than an explicit [JsonConstructor].
		ParameterlessPreferredHolder? holder = JsonSerializer.Deserialize<ParameterlessPreferredHolder>(
			"""{"Kind":"Basic","Child":{"Kind":"Basic"}}""", CreateOptions());

		Assert.IsNotNull(holder);
		Assert.AreEqual(Kind.Advanced, holder.Child.Kind);
	}
}
