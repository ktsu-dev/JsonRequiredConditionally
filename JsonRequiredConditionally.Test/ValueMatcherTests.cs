// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace ktsu.JsonRequiredConditionally.Tests;

[TestClass]
public class ValueMatcherTests
{
	public enum Kind
	{
		Basic = 0,
		Advanced = 1,
		Expert = 2,
	}

#pragma warning disable CA1008 // Enum values do not start with zero; test fixture does not require it
	public enum Other
	{
		Advanced = 1,
	}
#pragma warning restore CA1008

	[TestMethod]
	public void MatchesIdenticalEnumValues()
	{
		Assert.IsTrue(ValueMatcher.Matches(Kind.Advanced, Kind.Advanced));
	}

	[TestMethod]
	public void DoesNotMatchDifferentEnumValues()
	{
		Assert.IsFalse(ValueMatcher.Matches(Kind.Basic, Kind.Advanced));
	}

	[TestMethod]
	public void MatchesBoxedIntegerAgainstEnum()
	{
		Assert.IsTrue(ValueMatcher.Matches(Kind.Expert, 2));
		Assert.IsTrue(ValueMatcher.Matches(2, Kind.Expert));
	}

	[TestMethod]
	public void MatchesEnumsOfDifferentTypesWithEqualUnderlyingValues()
	{
		Assert.IsTrue(ValueMatcher.Matches(Kind.Advanced, Other.Advanced));
	}

	[TestMethod]
	public void MatchesNullToNull()
	{
		Assert.IsTrue(ValueMatcher.Matches(null, null));
	}

	[TestMethod]
	public void DoesNotMatchNullToValue()
	{
		Assert.IsFalse(ValueMatcher.Matches(null, Kind.Advanced));
		Assert.IsFalse(ValueMatcher.Matches(Kind.Advanced, null));
	}

	[TestMethod]
	public void MatchesEqualStringsOrdinally()
	{
		Assert.IsTrue(ValueMatcher.Matches("Advanced", "Advanced"));
		Assert.IsFalse(ValueMatcher.Matches("advanced", "Advanced"));
	}

	[TestMethod]
	public void MatchesEqualPrimitives()
	{
		Assert.IsTrue(ValueMatcher.Matches(42, 42));
		Assert.IsTrue(ValueMatcher.Matches(true, true));
		Assert.IsFalse(ValueMatcher.Matches(42, 43));
	}

	[TestMethod]
	public void DoesNotMatchUnconvertibleValueAgainstEnum()
	{
		Assert.IsFalse(ValueMatcher.Matches(Kind.Advanced, "Advanced"));
	}
}
