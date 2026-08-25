// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.JsonRequiredConditionally.Tests;

using System.Text.Json;

[TestClass]
public class EmptinessInspectorTests
{
	private static bool IsEmpty(string json)
	{
		using JsonDocument document = JsonDocument.Parse(json);

		return EmptinessInspector.IsEmpty(document.RootElement);
	}

	[TestMethod]
	public void NullIsEmpty()
	{
		Assert.IsTrue(IsEmpty(/*lang=json,strict*/ "null"));
	}

	[TestMethod]
	public void ZeroLengthStringIsEmpty()
	{
		Assert.IsTrue(IsEmpty(/*lang=json,strict*/ "\"\""));
	}

	[TestMethod]
	public void WhitespaceStringIsNotEmpty()
	{
		Assert.IsFalse(IsEmpty(/*lang=json,strict*/ "\"   \""));
		Assert.IsFalse(IsEmpty(/*lang=json,strict*/ "\"\\t\\n\""));
	}

	[TestMethod]
	public void PopulatedStringIsNotEmpty()
	{
		Assert.IsFalse(IsEmpty(/*lang=json,strict*/ "\"x\""));
	}

	[TestMethod]
	public void EmptyArrayIsEmpty()
	{
		Assert.IsTrue(IsEmpty(/*lang=json,strict*/ "[]"));
	}

	[TestMethod]
	public void PopulatedArrayIsNotEmpty()
	{
		Assert.IsFalse(IsEmpty(/*lang=json,strict*/ "[1]"));
	}

	[TestMethod]
	public void ArrayOfOneEmptyStringIsNotEmpty()
	{
		Assert.IsFalse(IsEmpty(/*lang=json,strict*/ "[\"\"]"));
	}

	[TestMethod]
	public void EmptyObjectIsEmpty()
	{
		Assert.IsTrue(IsEmpty(/*lang=json,strict*/ "{}"));
	}

	[TestMethod]
	public void PopulatedObjectIsNotEmpty()
	{
		Assert.IsFalse(IsEmpty(/*lang=json,strict*/ """{"a":1}"""));
	}

	[TestMethod]
	public void ObjectWithOnlyNullValueIsNotEmpty()
	{
		Assert.IsFalse(IsEmpty(/*lang=json,strict*/ """{"a":null}"""));
	}

	[TestMethod]
	public void NumbersAndBooleansAreNeverEmpty()
	{
		Assert.IsFalse(IsEmpty(/*lang=json,strict*/ "0"));
		Assert.IsFalse(IsEmpty(/*lang=json,strict*/ "false"));
		Assert.IsFalse(IsEmpty(/*lang=json,strict*/ "true"));
	}

	[TestMethod]
	public void UndefinedIsNotEmpty()
	{
		Assert.IsFalse(EmptinessInspector.IsEmpty(default));
	}
}
