// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

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
}
