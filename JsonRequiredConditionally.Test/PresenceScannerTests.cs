// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.JsonRequiredConditionally.Tests;

using System.Text.Json;

[TestClass]
public class PresenceScannerTests
{
	private static HashSet<string> Scan(string json, StringComparer? comparer = null)
	{
		using JsonDocument document = JsonDocument.Parse(json);

		return PresenceScanner.ScanPropertyNames(document.RootElement, comparer ?? StringComparer.Ordinal);
	}

	[TestMethod]
	public void CollectsTopLevelPropertyNames()
	{
		HashSet<string> names = Scan(/*lang=json,strict*/ """{"a":1,"b":"x","c":null}""");

		Assert.HasCount(3, names);
		Assert.Contains("a", names);
		Assert.Contains("b", names);
		Assert.Contains("c", names);
	}

	[TestMethod]
	public void RecordsExplicitNullAsPresent()
	{
		HashSet<string> names = Scan(/*lang=json,strict*/ """{"a":null}""");

		Assert.Contains("a", names);
	}

	[TestMethod]
	public void IgnoresNestedPropertyNames()
	{
		HashSet<string> names = Scan(/*lang=json,strict*/ """{"outer":{"inner":1},"sibling":2}""");

		Assert.HasCount(2, names);
		Assert.Contains("outer", names);
		Assert.Contains("sibling", names);
		Assert.DoesNotContain("inner", names);
	}

	[TestMethod]
	public void IgnoresPropertyNamesInsideArrays()
	{
		HashSet<string> names = Scan(/*lang=json,strict*/ """{"items":[{"inner":1},{"inner":2}],"count":2}""");

		Assert.HasCount(2, names);
		Assert.DoesNotContain("inner", names);
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
		HashSet<string> names = Scan(/*lang=json,strict*/ """{"Tuning":1}""", StringComparer.OrdinalIgnoreCase);

		Assert.Contains("tuning", names);
	}
}
