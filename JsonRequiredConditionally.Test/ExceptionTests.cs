// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.JsonRequiredConditionally.Tests;

using System.Text.Json;

[TestClass]
public class ExceptionTests
{
	[TestMethod]
	public void ExceptionDerivesFromJsonException()
	{
		JsonRequiredConditionallyException exception = new(["tuning"]);

		Assert.IsInstanceOfType<JsonException>(exception);
	}

	[TestMethod]
	public void ExceptionExposesMissingProperties()
	{
		JsonRequiredConditionallyException exception = new(["tuning", "host"]);

		CollectionAssert.AreEqual(new List<string> { "tuning", "host" }, exception.MissingProperties.ToList());
	}

	[TestMethod]
	public void ExceptionMessageNamesEveryMissingProperty()
	{
		JsonRequiredConditionallyException exception = new(["tuning", "host"]);

		StringAssert.Contains(exception.Message, "tuning");
		StringAssert.Contains(exception.Message, "host");
	}

	[TestMethod]
	public void DefaultConstructorProducesEmptyMissingProperties()
	{
		JsonRequiredConditionallyException exception = new();

		Assert.IsEmpty(exception.MissingProperties);
	}

	[TestMethod]
	public void MessageConstructorPreservesMessage()
	{
		JsonRequiredConditionallyException exception = new("custom message");

		Assert.AreEqual("custom message", exception.Message);
		Assert.IsEmpty(exception.MissingProperties);
	}

	[TestMethod]
	public void MissingPropertiesIsNotAliasedToTheCallerSuppliedList()
	{
		List<string> supplied = ["tuning"];
		JsonRequiredConditionallyException exception = new(supplied);

		supplied.Add("host");
		supplied[0] = "mutated";

		CollectionAssert.AreEqual(new List<string> { "tuning" }, exception.MissingProperties.ToList());
	}

	[TestMethod]
	public void InnerExceptionConstructorPreservesBoth()
	{
		InvalidOperationException inner = new("inner");
		JsonRequiredConditionallyException exception = new("outer", inner);

		Assert.AreEqual("outer", exception.Message);
		Assert.AreSame(inner, exception.InnerException);
	}

	[TestMethod]
	public void ExceptionExposesEmptyProperties()
	{
		JsonRequiredConditionallyException exception = new(["tuning"], ["items", "tags"]);

		CollectionAssert.AreEqual(new List<string> { "items", "tags" }, exception.EmptyProperties.ToList());
	}

	[TestMethod]
	public void SingleListConstructorLeavesEmptyPropertiesEmpty()
	{
		JsonRequiredConditionallyException exception = new(["tuning"]);

		Assert.IsEmpty(exception.EmptyProperties);
	}

	[TestMethod]
	public void DefaultConstructorProducesEmptyEmptyProperties()
	{
		JsonRequiredConditionallyException exception = new();

		Assert.IsEmpty(exception.EmptyProperties);
	}

	[TestMethod]
	public void MessageNamesBothCategoriesWhenBothArePopulated()
	{
		JsonRequiredConditionallyException exception = new(["tuning"], ["items"]);

		StringAssert.Contains(exception.Message, "tuning");
		StringAssert.Contains(exception.Message, "items");
	}

	[TestMethod]
	public void MessageForEmptyOnlyDoesNotClaimAnythingWasAbsent()
	{
		JsonRequiredConditionallyException exception = new([], ["items"]);

		StringAssert.Contains(exception.Message, "items");
		Assert.IsFalse(exception.Message.Contains("absent", StringComparison.Ordinal));
	}

	[TestMethod]
	public void TwoListConstructorWithNoEmptiesMatchesTheSingleListMessage()
	{
		JsonRequiredConditionallyException single = new(["tuning"]);
		JsonRequiredConditionallyException both = new(["tuning"], []);

		Assert.AreEqual(single.Message, both.Message);
	}

	[TestMethod]
	public void EmptyPropertiesAreCopiedNotAliased()
	{
		List<string> empties = ["items"];
		JsonRequiredConditionallyException exception = new([], empties);

		empties.Add("tags");

		Assert.HasCount(1, exception.EmptyProperties);
	}
}
