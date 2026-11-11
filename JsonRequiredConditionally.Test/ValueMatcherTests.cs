// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.JsonRequiredConditionally.Tests;

using System.Diagnostics.CodeAnalysis;

[TestClass]
public class ValueMatcherTests
{
	private static readonly int[] Codes = [1, 2];
	private static readonly string[] Names = ["a"];

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
	public void FloatAndDoubleMatchAtFloatPrecisionInBothDirections()
	{
		Assert.IsTrue(ValueMatcher.Matches(0.1d, 0.1f));
		Assert.IsTrue(ValueMatcher.Matches(0.1f, 0.1d));
		Assert.IsTrue(ValueMatcher.Matches(0.5d, 0.5f));
		Assert.IsTrue(ValueMatcher.Matches(0.5f, 0.5d));
		Assert.IsFalse(ValueMatcher.Matches(0.2d, 0.1f));
		Assert.IsFalse(ValueMatcher.Matches(0.2f, 0.1d));
		Assert.IsFalse(ValueMatcher.Matches(1e300d, float.PositiveInfinity));
		Assert.IsFalse(ValueMatcher.Matches(float.PositiveInfinity, 1e300d));
		Assert.IsTrue(ValueMatcher.Matches(double.PositiveInfinity, float.PositiveInfinity));
		Assert.IsTrue(ValueMatcher.Matches(float.NaN, double.NaN));
	}

	[TestMethod]
	public void CanEverMatchAcceptsFloatAndDoublePairingsWithinFloatRange()
	{
		Assert.IsTrue(ValueMatcher.CanEverMatch(typeof(double), 0.1f));
		Assert.IsTrue(ValueMatcher.CanEverMatch(typeof(float), 0.1d));
		Assert.IsTrue(ValueMatcher.CanEverMatch(typeof(float?), 0.1d));
		Assert.IsTrue(ValueMatcher.CanEverMatch(typeof(float), double.PositiveInfinity));
		Assert.IsFalse(ValueMatcher.CanEverMatch(typeof(float), 1e300d));
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
	public void DoesNotParseAStringToMatchANumericOrBoolSibling()
	{
		Assert.IsFalse(ValueMatcher.Matches(1, "1"));
		Assert.IsFalse(ValueMatcher.Matches(1.5, "1.5"));
		Assert.IsFalse(ValueMatcher.Matches(true, "True"));
		Assert.IsFalse(ValueMatcher.CanEverMatch(typeof(int), "1"));
		Assert.IsFalse(ValueMatcher.CanEverMatch(typeof(int), "01"));
		Assert.IsFalse(ValueMatcher.CanEverMatch(typeof(bool), "true"));
		Assert.IsFalse(ValueMatcher.CanEverMatch(typeof(bool), "True"));
	}

	[TestMethod]
	public void DoesNotCoerceBetweenNumbersAndBoolOrChar()
	{
		Assert.IsFalse(ValueMatcher.Matches(true, 1));
		Assert.IsFalse(ValueMatcher.Matches(false, 0));
		Assert.IsFalse(ValueMatcher.Matches('A', 65));
		Assert.IsFalse(ValueMatcher.Matches(65, 'A'));
		Assert.IsFalse(ValueMatcher.CanEverMatch(typeof(bool), 1));
		Assert.IsFalse(ValueMatcher.CanEverMatch(typeof(char), 65));
		Assert.IsFalse(ValueMatcher.CanEverMatch(typeof(int), true));
		Assert.IsFalse(ValueMatcher.CanEverMatch(typeof(Kind), true));
	}

	[TestMethod]
	public void StillMatchesSameTypeBoolAndCharConstants()
	{
		Assert.IsTrue(ValueMatcher.Matches(true, true));
		Assert.IsTrue(ValueMatcher.Matches('A', 'A'));
		Assert.IsTrue(ValueMatcher.CanEverMatch(typeof(bool), true));
		Assert.IsTrue(ValueMatcher.CanEverMatch(typeof(bool?), false));
		Assert.IsTrue(ValueMatcher.CanEverMatch(typeof(char), 'A'));
	}

	[TestMethod]
	[DataRow("1")]
	[DataRow("999")]
	[DataRow(" Advanced ")]
	[DataRow("Advanced ")]
	[DataRow("Basic, Advanced")]
	[DataRow("Basic,Advanced")]
	public void EnumParseOnlyInputsAreNotMemberNames(string text)
	{
		// Enum.Parse accepts each of these, but none is a member's own name.
		Assert.IsFalse(ValueMatcher.CanEverMatch(typeof(Kind), text));
		Assert.IsFalse(ValueMatcher.CanEverMatch(typeof(Kind?), text));
		Assert.IsFalse(ValueMatcher.Matches(Kind.Advanced, text));
		Assert.IsFalse(ValueMatcher.Matches(text, Kind.Advanced));
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
		Assert.IsTrue(ValueMatcher.CanEverMatch(typeof(object), null));
		Assert.IsTrue(ValueMatcher.CanEverMatch(typeof(Guid?), null));
		Assert.IsTrue(ValueMatcher.CanEverMatch(typeof(int?), null));
		Assert.IsTrue(ValueMatcher.CanEverMatch(typeof(string), null));
		Assert.IsTrue(ValueMatcher.CanEverMatch(typeof(string), "text"));
	}

	[TestMethod]
	public void CanEverMatchRejectsGenuinelyUnconvertibleValues()
	{
		Assert.IsFalse(ValueMatcher.CanEverMatch(typeof(Guid), 1));
		Assert.IsFalse(ValueMatcher.CanEverMatch(typeof(Guid), null));
		Assert.IsFalse(ValueMatcher.CanEverMatch(typeof(int), null));
		Assert.IsFalse(ValueMatcher.CanEverMatch(typeof(Kind), null));
		Assert.IsFalse(ValueMatcher.CanEverMatch(typeof(Kind), "NoSuchMember"));
		Assert.IsFalse(ValueMatcher.CanEverMatch(typeof(string), 1));
		Assert.IsFalse(ValueMatcher.CanEverMatch(typeof(object), "anything"));
		Assert.IsFalse(ValueMatcher.CanEverMatch(typeof(object), 3));
		Assert.IsFalse(ValueMatcher.CanEverMatch(typeof(int[]), Codes));
		Assert.IsFalse(ValueMatcher.CanEverMatch(typeof(IEnumerable<int>), Codes));
		Assert.IsFalse(ValueMatcher.CanEverMatch(typeof(IReadOnlyList<int>), Codes));
		Assert.IsFalse(ValueMatcher.CanEverMatch(typeof(string[]), Names));
		Assert.IsFalse(ValueMatcher.CanEverMatch(typeof(System.Text.Json.JsonElement), "anything"));
		Assert.IsFalse(ValueMatcher.CanEverMatch(typeof(System.Text.Json.JsonElement?), "anything"));
		Assert.IsFalse(ValueMatcher.CanEverMatch(typeof(System.Text.Json.Nodes.JsonNode), "anything"));
		Assert.IsFalse(ValueMatcher.CanEverMatch(typeof(System.Text.Json.Nodes.JsonValue), 3));
	}
}
