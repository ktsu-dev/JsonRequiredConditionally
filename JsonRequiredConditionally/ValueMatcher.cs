// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace ktsu.JsonRequiredConditionally;

using System.Globalization;

/// <summary>
/// Compares a sibling's runtime value against the constant supplied to an attribute.
/// </summary>
internal static class ValueMatcher
{
	/// <summary>
	/// Determines whether a sibling's actual value equals the expected attribute value.
	/// </summary>
	/// <param name="actual">The value read from the materialized instance.</param>
	/// <param name="expected">The constant supplied to the attribute.</param>
	/// <returns>True when the values are considered equal.</returns>
	internal static bool Matches(object? actual, object? expected)
	{
		if (actual is null || expected is null)
		{
			return actual is null && expected is null;
		}

		Type actualType = actual.GetType();
		Type expectedType = expected.GetType();

		if (actualType.IsEnum || expectedType.IsEnum)
		{
			return MatchesAsEnum(actual, expected, actualType.IsEnum ? actualType : expectedType);
		}

		if (actual is string actualText && expected is string expectedText)
		{
			return string.Equals(actualText, expectedText, StringComparison.Ordinal);
		}

		return actual.Equals(expected);
	}

	private static bool MatchesAsEnum(object actual, object expected, Type enumType)
	{
		Type underlying = Enum.GetUnderlyingType(enumType);

		try
		{
			object actualValue = Convert.ChangeType(actual, underlying, CultureInfo.InvariantCulture);
			object expectedValue = Convert.ChangeType(expected, underlying, CultureInfo.InvariantCulture);

			return actualValue.Equals(expectedValue);
		}
		catch (InvalidCastException)
		{
			return false;
		}
		catch (FormatException)
		{
			return false;
		}
		catch (OverflowException)
		{
			return false;
		}
	}
}
