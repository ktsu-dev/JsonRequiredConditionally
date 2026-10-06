// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.JsonRequiredConditionally.Tests;

using System.Text.Json;

/// <summary>
/// Covers System.Text.Json's own errors (not this library's violations) raised inside a decorated
/// type: registering the factory must not change where they say the bad input is.
/// </summary>
[TestClass]
public class ErrorLocationTests
{
	private static JsonException Throws<T>(string json, JsonSerializerOptions options) =>
		Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<T>(json, options));

	/// <summary>
	/// Asserts that, with the factory registered, the error names the element holding the bad
	/// input (<paramref name="expectedPath"/>, a prefix of the unregistered path) on the same line
	/// as without the factory, and that the relative error is kept as the inner exception.
	/// </summary>
	private static void AssertSameLocationAsUnregistered<T>(string json, string expectedPath, int maxDepth = 0)
	{
		JsonException unregistered = Throws<T>(json, new() { MaxDepth = maxDepth });
		JsonException registered = Throws<T>(json, new() { MaxDepth = maxDepth, Converters = { new JsonRequiredConditionallyConverterFactory() } });

		Assert.IsNotInstanceOfType<JsonRequiredConditionallyException>(registered);
		Assert.AreEqual(expectedPath, registered.Path);
		Assert.StartsWith(expectedPath, unregistered.Path!);
		Assert.AreEqual(unregistered.LineNumber, registered.LineNumber);
		Assert.IsInstanceOfType<JsonException>(registered.InnerException);
	}

	[TestMethod]
	public void TypeErrorInListElementKeepsItsIndexAndLine() =>
		AssertSameLocationAsUnregistered<List<ErrorLocationSibling>>(/*lang=json,strict*/ """
			[
			 {"Mode":2},
			 {"Mode":"oops"}
			]
			""", expectedPath: "$[1]");

	[TestMethod]
	public void TypeErrorInArrayElementKeepsItsIndexAndLine() =>
		AssertSameLocationAsUnregistered<ErrorLocationSibling[]>(/*lang=json,strict*/ """
			[
			 {"Mode":2},
			 {"Mode":"oops"}
			]
			""", expectedPath: "$[1]");

	[TestMethod]
	public void TypeErrorInDictionaryValueKeepsItsKeyAndLine() =>
		AssertSameLocationAsUnregistered<Dictionary<string, ErrorLocationOuter>>(/*lang=json,strict*/ """
			{"a":{"x":{"Mode":2}},
			 "b":{"x":{"Mode":"oops"}}}
			""", expectedPath: "$.b");

	[TestMethod]
	public void MaxDepthInsideNestedDecoratedTypeNamesItsElement() =>
		AssertSameLocationAsUnregistered<List<ErrorLocationOuter>>(/*lang=json,strict*/ """
			[{"x":{"Mode":1,"Tuning":"t"}}]
			""", expectedPath: "$[0]", maxDepth: 2);

	[TestMethod]
	public void TypeErrorInDecoratedRootKeepsItsPath()
	{
		JsonException exception = Throws<ErrorLocationOuter>(
			/*lang=json,strict*/ """{"x":{"Mode":"oops"}}""",
			new() { Converters = { new JsonRequiredConditionallyConverterFactory() } });

		Assert.AreEqual("$.x.Mode", exception.Path);
	}
}
