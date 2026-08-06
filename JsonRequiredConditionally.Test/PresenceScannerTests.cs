// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace ktsu.JsonRequiredConditionally.Tests;

using System.Text;
using System.Text.Json;

[TestClass]
public class PresenceScannerTests
{
	private static HashSet<string> Scan(string json, StringComparer? comparer = null)
	{
		byte[] bytes = Encoding.UTF8.GetBytes(json);
		Utf8JsonReader reader = new(bytes);
		reader.Read();

		return PresenceScanner.ScanPropertyNames(reader, comparer ?? StringComparer.Ordinal);
	}

	[TestMethod]
	public void CollectsTopLevelPropertyNames()
	{
		HashSet<string> names = Scan("""{"a":1,"b":"x","c":null}""");

		Assert.HasCount(3, names);
		Assert.IsTrue(names.Contains("a"));
		Assert.IsTrue(names.Contains("b"));
		Assert.IsTrue(names.Contains("c"));
	}

	[TestMethod]
	public void RecordsExplicitNullAsPresent()
	{
		HashSet<string> names = Scan("""{"a":null}""");

		Assert.IsTrue(names.Contains("a"));
	}

	[TestMethod]
	public void IgnoresNestedPropertyNames()
	{
		HashSet<string> names = Scan("""{"outer":{"inner":1},"sibling":2}""");

		Assert.HasCount(2, names);
		Assert.IsTrue(names.Contains("outer"));
		Assert.IsTrue(names.Contains("sibling"));
		Assert.IsFalse(names.Contains("inner"));
	}

	[TestMethod]
	public void IgnoresPropertyNamesInsideArrays()
	{
		HashSet<string> names = Scan("""{"items":[{"inner":1},{"inner":2}],"count":2}""");

		Assert.HasCount(2, names);
		Assert.IsFalse(names.Contains("inner"));
	}

	[TestMethod]
	public void ReturnsEmptyForEmptyObject()
	{
		Assert.IsEmpty(Scan("{}"));
	}

	[TestMethod]
	public void ReturnsEmptyWhenNotPositionedOnAnObject()
	{
		Assert.IsEmpty(Scan("""[1,2,3]"""));
	}

	[TestMethod]
	public void HonoursCaseInsensitiveComparer()
	{
		HashSet<string> names = Scan("""{"Tuning":1}""", StringComparer.OrdinalIgnoreCase);

		Assert.IsTrue(names.Contains("tuning"));
	}

	[TestMethod]
	public void DoesNotAdvanceTheCallersReader()
	{
		byte[] bytes = Encoding.UTF8.GetBytes("""{"a":1,"b":2}""");
		Utf8JsonReader reader = new(bytes);
		reader.Read();

		PresenceScanner.ScanPropertyNames(reader, StringComparer.Ordinal);

		Assert.AreEqual(JsonTokenType.StartObject, reader.TokenType);
	}
}
