// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.JsonRequiredConditionally.Tests;

using System.Text.Json;
using System.Text.Json.Serialization;

/// <summary>
/// Covers which types the factory claims, and therefore which violations are reported with a path
/// rooted at the outermost container rather than at whatever inner type happened to be claimed.
/// </summary>
[TestClass]
public class EligibilityTests
{
	private static JsonSerializerOptions CreateOptions(bool includeFields = false) =>
		new()
		{
			IncludeFields = includeFields,
			Converters = { new JsonStringEnumConverter(), new JsonRequiredConditionallyConverterFactory() },
		};

	[TestMethod]
	public void DecoratedPlainFieldIsEnforcedWhenIncludeFieldsIsOn()
	{
		JsonRequiredConditionallyException exception = Assert.ThrowsExactly<JsonRequiredConditionallyException>(
			() => JsonSerializer.Deserialize<FieldDecoratedConfig>(
				"""{"Kind":"Advanced"}""", CreateOptions(includeFields: true)));

		CollectionAssert.AreEquivalent(new List<string> { "Tuning" }, exception.MissingProperties.ToList());
	}

	[TestMethod]
	public void DecoratedPlainFieldIsNotEnforcedWhenIncludeFieldsIsOff()
	{
		// System.Text.Json never populates the field in this configuration either, so there is
		// nothing to validate and no rule is compiled -- claiming the type costs only buffering.
		FieldDecoratedConfig? config = JsonSerializer.Deserialize<FieldDecoratedConfig>(
			"""{"Kind":"Advanced"}""", CreateOptions(includeFields: false));

		Assert.IsNotNull(config);
		Assert.IsNull(config.Tuning);
	}

	[TestMethod]
	public void DecoratedPlainFieldTypeIsClaimed()
	{
		JsonRequiredConditionallyConverterFactory factory = new();

		Assert.IsTrue(factory.CanConvert(typeof(FieldDecoratedConfig)));
	}

	[TestMethod]
	public void NotEmptyDecoratedPlainFieldIsEnforcedWhenIncludeFieldsIsOn()
	{
		JsonRequiredConditionallyException exception = Assert.ThrowsExactly<JsonRequiredConditionallyException>(
			() => JsonSerializer.Deserialize<NotEmptyFieldDecoratedConfig>(
				"""{}""", CreateOptions(includeFields: true)));

		CollectionAssert.AreEquivalent(new List<string> { "Name" }, exception.MissingProperties.ToList());
	}

	[TestMethod]
	public void NotEmptyDecoratedPlainFieldIsNotEnforcedWhenIncludeFieldsIsOff()
	{
		// System.Text.Json never populates the field in this configuration either, so there is
		// nothing to validate and no rule is compiled -- claiming the type costs only buffering.
		NotEmptyFieldDecoratedConfig? config = JsonSerializer.Deserialize<NotEmptyFieldDecoratedConfig>(
			"""{}""", CreateOptions(includeFields: false));

		Assert.IsNotNull(config);
		Assert.IsNull(config.Name);
	}

	[TestMethod]
	public void NotEmptyDecoratedPlainFieldTypeIsClaimed()
	{
		JsonRequiredConditionallyConverterFactory factory = new();

		Assert.IsTrue(factory.CanConvert(typeof(NotEmptyFieldDecoratedConfig)));
	}

	[TestMethod]
	public void NestedSequenceHolderKeepsTheFullPathPrefix()
	{
		JsonRequiredConditionallyException exception = Assert.ThrowsExactly<JsonRequiredConditionallyException>(
			() => JsonSerializer.Deserialize<GridConfig>(
				"""{"Grid":[[{"Kind":"Basic"},{"Kind":"Advanced"}]]}""", CreateOptions()));

		CollectionAssert.AreEquivalent(new List<string> { "Grid[0][1].Tuning" }, exception.MissingProperties.ToList());
	}

	[TestMethod]
	public void DictionaryOfSequencesHolderKeepsTheFullPathPrefix()
	{
		JsonRequiredConditionallyException exception = Assert.ThrowsExactly<JsonRequiredConditionallyException>(
			() => JsonSerializer.Deserialize<BucketConfig>(
				"""{"Buckets":{"a":[{"Kind":"Advanced"}]}}""", CreateOptions()));

		CollectionAssert.AreEquivalent(new List<string> { "Buckets.a[0].Tuning" }, exception.MissingProperties.ToList());
	}

	[TestMethod]
	public void NestedCollectionHoldersAreClaimed()
	{
		JsonRequiredConditionallyConverterFactory factory = new();

		Assert.IsTrue(factory.CanConvert(typeof(GridConfig)));
		Assert.IsTrue(factory.CanConvert(typeof(BucketConfig)));
	}

	[TestMethod]
	public void ValueTypeGetOnlyPropertyIsNotTreatedAsConstructorBound()
	{
		// System.Text.Json uses the implicit parameterless constructor for a value type without
		// [JsonConstructor], so Inner is never populated and the "Inner" JSON node is discarded
		// entirely. Validating Inner's never-populated default against that node is a false positive:
		// ZeroArmedStruct's rule is armed by Kind == Basic, which is exactly its CLR default.
		ConstructorlessValueHolder holder = JsonSerializer.Deserialize<ConstructorlessValueHolder>(
			"""{"Kind":"Basic","Inner":{"Kind":"Advanced"}}""", CreateOptions());

		Assert.AreEqual(Kind.Basic, holder.Inner.Kind);
		Assert.IsNull(holder.Inner.Name);
	}

	[TestMethod]
	public void TypeWithOnlyTheNotEmptyAttributeIsClaimed()
	{
		Assert.IsTrue(RequirementRuleCompiler.HasRules(typeof(NotEmptyStringConfig)));
	}

	[TestMethod]
	public void HolderReachingANotEmptyMemberIsClaimed()
	{
		Assert.IsTrue(RequirementRuleCompiler.HasRules(typeof(NotEmptyHolder)));
	}

	[TestMethod]
	public void HolderReachingANotEmptyMemberThroughACollectionIsClaimed()
	{
		Assert.IsTrue(RequirementRuleCompiler.HasRules(typeof(NotEmptySequenceHolder)));
		Assert.IsTrue(RequirementRuleCompiler.HasRules(typeof(NotEmptyDictionaryHolder)));
	}

	[TestMethod]
	public void HolderReachingANotEmptyMemberThroughNestedCollectionsIsClaimed()
	{
		Assert.IsTrue(RequirementRuleCompiler.HasRules(typeof(NotEmptyNestedSequenceHolder)));
	}

	[TestMethod]
	public void BareCollectionsAreStillNotClaimed()
	{
		// IsExcludedFromEligibility rejects anything assignable to IEnumerable, so a bare collection
		// is never claimed at its own top however decorated its element type is. The holder that owns
		// the collection is what gets claimed, which is what roots the reported path at the outermost
		// container. The new attribute must not change this.
		Assert.IsFalse(RequirementRuleCompiler.HasRules(typeof(List<NotEmptyStringConfig>)));
		Assert.IsFalse(RequirementRuleCompiler.HasRules(typeof(NotEmptyStringConfig[])));
	}

	[TestMethod]
	public void UndecoratedTypesAreStillNotClaimed()
	{
		Assert.IsFalse(RequirementRuleCompiler.HasRules(typeof(PlainConfig)));
	}
}
