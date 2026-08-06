// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace ktsu.JsonRequiredConditionally.Tests;

using System.Text.Json;

[TestClass]
public class RequirementRuleCompilerTests
{
	[TestMethod]
	public void HasRulesDetectsDecoratedMembers()
	{
		Assert.IsTrue(RequirementRuleCompiler.HasRules(typeof(SimpleConfig)));
	}

	[TestMethod]
	public void HasRulesRejectsUndecoratedTypes()
	{
		Assert.IsFalse(RequirementRuleCompiler.HasRules(typeof(PlainConfig)));
	}

	[TestMethod]
	public void HasRulesRejectsPrimitivesStringsAndEnums()
	{
		Assert.IsFalse(RequirementRuleCompiler.HasRules(typeof(int)));
		Assert.IsFalse(RequirementRuleCompiler.HasRules(typeof(string)));
		Assert.IsFalse(RequirementRuleCompiler.HasRules(typeof(Kind)));
		Assert.IsFalse(RequirementRuleCompiler.HasRules(typeof(List<SimpleConfig>)));
	}

	[TestMethod]
	public void CompileUsesClrNameWhenNoNamingPolicy()
	{
		RequirementRule[] rules = RequirementRuleCompiler.Compile(typeof(SimpleConfig), new JsonSerializerOptions());

		Assert.HasCount(1, rules);
		Assert.AreEqual("Tuning", rules[0].JsonName);
		Assert.AreEqual("Tuning", rules[0].MemberName);
	}

	[TestMethod]
	public void CompileAppliesNamingPolicy()
	{
		JsonSerializerOptions options = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

		RequirementRule[] rules = RequirementRuleCompiler.Compile(typeof(SimpleConfig), options);

		Assert.AreEqual("tuning", rules[0].JsonName);
	}

	[TestMethod]
	public void CompilePrefersJsonPropertyNameOverNamingPolicy()
	{
		JsonSerializerOptions options = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

		RequirementRule[] rules = RequirementRuleCompiler.Compile(typeof(RenamedConfig), options);

		Assert.AreEqual("tuning_value", rules[0].JsonName);
	}

	[TestMethod]
	public void SameSiblingValuesCollapseToOneOredCondition()
	{
		RequirementRule[] rules = RequirementRuleCompiler.Compile(typeof(OrConfig), new JsonSerializerOptions());

		Assert.HasCount(1, rules[0].Conditions);
		Assert.IsTrue(rules[0].IsRequiredFor(new OrConfig { Kind = Kind.Advanced }));
		Assert.IsTrue(rules[0].IsRequiredFor(new OrConfig { Kind = Kind.Expert }));
		Assert.IsFalse(rules[0].IsRequiredFor(new OrConfig { Kind = Kind.Basic }));
	}

	[TestMethod]
	public void DifferentSiblingsProduceAndedConditions()
	{
		RequirementRule[] rules = RequirementRuleCompiler.Compile(typeof(AndConfig), new JsonSerializerOptions());

		Assert.HasCount(2, rules[0].Conditions);
		Assert.IsTrue(rules[0].IsRequiredFor(new AndConfig { Kind = Kind.Advanced, Mode = Mode.Remote }));
		Assert.IsFalse(rules[0].IsRequiredFor(new AndConfig { Kind = Kind.Advanced, Mode = Mode.Local }));
		Assert.IsFalse(rules[0].IsRequiredFor(new AndConfig { Kind = Kind.Basic, Mode = Mode.Remote }));
	}

	[TestMethod]
	public void UnresolvableSiblingNameThrowsInvalidOperationException()
	{
		InvalidOperationException exception = Assert.ThrowsExactly<InvalidOperationException>(
			() => RequirementRuleCompiler.Compile(typeof(BrokenConfig), new JsonSerializerOptions()));

		StringAssert.Contains(exception.Message, "NoSuchMember");
		StringAssert.Contains(exception.Message, nameof(BrokenConfig));
	}
}
