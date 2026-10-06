// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.JsonRequiredConditionally.Tests;

using System.Text.Json;

/// <summary>
/// Covers an <c>override</c> of a decorated <c>virtual</c> property. Both attributes are
/// <c>Inherited = true</c>, so the override must be enforced exactly like the base declaration,
/// including when the override is the type's only decorated member.
/// </summary>
[TestClass]
public class OverrideTests
{
	private static JsonSerializerOptions CreateOptions() =>
		new() { Converters = { new JsonRequiredConditionallyConverterFactory() } };

	private static JsonRequiredConditionallyException Throws<T>(string json) =>
		Assert.ThrowsExactly<JsonRequiredConditionallyException>(
			() => JsonSerializer.Deserialize<T>(json, CreateOptions()));

	[TestMethod]
	public void NotEmptyOnBaseIsEnforced()
	{
		JsonRequiredConditionallyException exception = Throws<OverrideNotEmptyBase>(/*lang=json,strict*/ "{}");

		Assert.AreSequenceEqual(new List<string> { "Name" }, [.. exception.MissingProperties]);
	}

	[TestMethod]
	public void NotEmptyOnOnlyOverriddenMemberIsEnforced()
	{
		JsonRequiredConditionallyException exception = Throws<OverrideNotEmptyDerived>(/*lang=json,strict*/ "{}");

		Assert.AreSequenceEqual(new List<string> { "Name" }, [.. exception.MissingProperties]);
	}

	[TestMethod]
	public void NotEmptyOnOverriddenMemberIsEnforcedAlongsideAnotherDecoratedMember()
	{
		JsonRequiredConditionallyException exception = Throws<OverrideNotEmptyDerivedWithOther>(/*lang=json,strict*/ """{"Other":"x"}""");

		Assert.AreSequenceEqual(new List<string> { "Name" }, [.. exception.MissingProperties]);
	}

	[TestMethod]
	public void NotEmptyOnOverriddenMemberReportsEmpty()
	{
		JsonRequiredConditionallyException exception = Throws<OverrideNotEmptyDerived>(/*lang=json,strict*/ """{"Name":""}""");

		Assert.AreSequenceEqual(new List<string> { "Name" }, [.. exception.EmptyProperties]);
	}

	[TestMethod]
	public void SiblingRuleOnBaseIsEnforced()
	{
		JsonRequiredConditionallyException exception = Throws<OverrideSiblingBase>(/*lang=json,strict*/ """{"Mode":1}""");

		Assert.AreSequenceEqual(new List<string> { "Tuning" }, [.. exception.MissingProperties]);
	}

	[TestMethod]
	public void SiblingRuleOnOnlyOverriddenMemberIsEnforced()
	{
		JsonRequiredConditionallyException exception = Throws<OverrideSiblingDerived>(/*lang=json,strict*/ """{"Mode":1}""");

		Assert.AreSequenceEqual(new List<string> { "Tuning" }, [.. exception.MissingProperties]);
	}

	[TestMethod]
	public void SiblingRuleOnOverriddenMemberIsSatisfiedWhenPresent()
	{
		OverrideSiblingDerived? config = JsonSerializer.Deserialize<OverrideSiblingDerived>(
			/*lang=json,strict*/ """{"Mode":1,"Tuning":"fast"}""", CreateOptions());

		Assert.IsNotNull(config);
		Assert.AreEqual("fast", config.Tuning);
	}

	[TestMethod]
	public void SiblingRuleOnOverriddenMemberDoesNotFireWhenSiblingDiffers()
	{
		OverrideSiblingDerived? config = JsonSerializer.Deserialize<OverrideSiblingDerived>(
			/*lang=json,strict*/ """{"Mode":2}""", CreateOptions());

		Assert.IsNotNull(config);
		Assert.IsNull(config.Tuning);
	}
}
