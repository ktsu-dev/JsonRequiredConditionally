// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.JsonRequiredConditionally.Tests;

using System.Diagnostics.CodeAnalysis;

[TestClass]
public class ValueMatcherTests
{
	public enum Kind
	{
		Basic = 0,
		Advanced = 1,
		Expert = 2,
	}

	[SuppressMessage("Design", "CA1008:Enums should have zero value", Justification = "Test fixture intentionally has no zero-value member.")]
	public enum Other
	{
		Advanced = 1,
	}

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
	public void MatchesEnumMemberNameWrittenAsAString()
	{
		Assert.IsTrue(ValueMatcher.Matches(Kind.Advanced, "Advanced"));
		Assert.IsTrue(ValueMatcher.Matches("Advanced", Kind.Advanced));
	}

	[TestMethod]
	public void DoesNotMatchEnumMemberNameOfADifferentMember()
	{
		Assert.IsFalse(ValueMatcher.Matches(Kind.Basic, "Advanced"));
	}

	[TestMethod]
	public void DoesNotMatchEnumMemberNameThatIsNotAMemberAtAll()
	{
		Assert.IsFalse(ValueMatcher.Matches(Kind.Advanced, "NoSuchMember"));
	}

	[TestMethod]
	public void DoesNotMatchEnumMemberNameOfTheWrongCase()
	{
		Assert.IsFalse(ValueMatcher.Matches(Kind.Advanced, "advanced"));
	}

	[TestMethod]
	public void WidensNarrowerIntegerAttributeArgumentsToTheSiblingType()
	{
		Assert.IsTrue(ValueMatcher.Matches(1L, 1));
		Assert.IsTrue(ValueMatcher.Matches((short)1, 1));
		Assert.IsTrue(ValueMatcher.Matches((byte)1, 1));
		Assert.IsTrue(ValueMatcher.Matches(1u, 1));
		Assert.IsTrue(ValueMatcher.Matches((nint)1, 1));
		Assert.IsTrue(ValueMatcher.Matches((nuint)1, 1));
	}

	[TestMethod]
	public void WidenedComparisonStillDistinguishesDifferentValues()
	{
		Assert.IsFalse(ValueMatcher.Matches(2L, 1));
		Assert.IsFalse(ValueMatcher.Matches((nint)2, 1));
	}

	[TestMethod]
	public void DoesNotMatchAConstantThatOnlyRoundsOntoTheSiblingValue()
	{
		// Convert narrows by rounding, so each of these used to convert onto the sibling's own value
		// and compare equal to a number it does not equal. Banker's rounding takes 1.5 to 2, so the
		// .5 case matched just as the ordinary fractional one did.
		Assert.IsFalse(ValueMatcher.Matches(2, 2.4));
		Assert.IsFalse(ValueMatcher.Matches(2, 1.5));
		Assert.IsFalse(ValueMatcher.Matches(2, 2.4m));
		Assert.IsFalse(ValueMatcher.Matches(2L, 2.4));
		Assert.IsFalse(ValueMatcher.Matches((nint)2, 2.4));
	}

	[TestMethod]
	public void MatchesAConstantThatNarrowsExactly()
	{
		// The widening this matcher exists for is untouched: a whole constant written as a double
		// round-trips through the sibling's type unchanged.
		Assert.IsTrue(ValueMatcher.Matches(2, 2.0));
		Assert.IsTrue(ValueMatcher.Matches(2, 2.0m));
		Assert.IsTrue(ValueMatcher.Matches((nint)2, 2.0));
	}

	[TestMethod]
	public void CanEverMatchRejectsAConstantNoValueOfTheSiblingTypeCouldEqual()
	{
		Assert.IsFalse(ValueMatcher.CanEverMatch(typeof(int), 2.4));
		Assert.IsFalse(ValueMatcher.CanEverMatch(typeof(long), 1.5));
		Assert.IsTrue(ValueMatcher.CanEverMatch(typeof(int), 2.0));
	}

	[TestMethod]
	public void DoesNotStringifyANumberToMatchAStringSibling()
	{
		Assert.IsFalse(ValueMatcher.Matches("1", 1));
	}

	[TestMethod]
	public void CanEverMatchAcceptsWidenableAndEnumParseableValues()
	{
		Assert.IsTrue(ValueMatcher.CanEverMatch(typeof(long), 1));
		Assert.IsTrue(ValueMatcher.CanEverMatch(typeof(short), 1));
		Assert.IsTrue(ValueMatcher.CanEverMatch(typeof(nint), 1));
		Assert.IsTrue(ValueMatcher.CanEverMatch(typeof(Kind), "Advanced"));
		Assert.IsTrue(ValueMatcher.CanEverMatch(typeof(Kind), 1));
		Assert.IsTrue(ValueMatcher.CanEverMatch(typeof(Kind?), Kind.Advanced));
		Assert.IsTrue(ValueMatcher.CanEverMatch(typeof(object), "anything"));
		Assert.IsTrue(ValueMatcher.CanEverMatch(typeof(Guid), null));
		Assert.IsTrue(ValueMatcher.CanEverMatch(typeof(string), "text"));
	}

	[TestMethod]
	public void CanEverMatchRejectsGenuinelyUnconvertibleValues()
	{
		Assert.IsFalse(ValueMatcher.CanEverMatch(typeof(Guid), 1));
		Assert.IsFalse(ValueMatcher.CanEverMatch(typeof(Kind), "NoSuchMember"));
		Assert.IsFalse(ValueMatcher.CanEverMatch(typeof(string), 1));
	}
}
