// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.JsonRequiredConditionally;

using System.Globalization;

/// <summary>
/// Compares a sibling's runtime value against the constant supplied to an attribute.
/// </summary>
/// <remarks>
/// An attribute argument is limited to the constant types the C# language allows, so it is routinely
/// a different type from the sibling it is compared against: <c>1</c> written against a
/// <c>long</c> sibling, or an enum member's name written as a string. Exact boxed equality would
/// leave those rules silently never firing, which is the dangerous direction -- hence the widening
/// here, and <see cref="CanEverMatch"/>, which lets rule compilation reject the pairings that could
/// never match no matter what the payload contains.
/// </remarks>
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

		if (actual is string actualText)
		{
			return expected is string expectedText && string.Equals(actualText, expectedText, StringComparison.Ordinal);
		}

		return actualType == expectedType
			? actual.Equals(expected)
			: TryWiden(expected, actualType, out object? widened) && actual.Equals(widened);
	}

	/// <summary>
	/// Determines whether an attribute value could ever equal a value of the sibling's declared type.
	/// </summary>
	/// <param name="siblingType">The sibling member's declared type.</param>
	/// <param name="expected">The constant supplied to the attribute.</param>
	/// <returns>False only when no value of <paramref name="siblingType"/> could ever match.</returns>
	/// <remarks>
	/// This mirrors <see cref="Matches"/> exactly one step removed: it answers the same question
	/// against the sibling's *declared* type rather than one particular runtime value, so rule
	/// compilation can fail loudly on a pairing that would otherwise be a rule that quietly never
	/// fires. A null expected value is always admissible -- it is an explicit choice by the author,
	/// and it matches a null sibling value.
	/// </remarks>
	internal static bool CanEverMatch(Type siblingType, object? expected)
	{
		if (expected is null)
		{
			return true;
		}

		Type target = Nullable.GetUnderlyingType(siblingType) ?? siblingType;

		if (target == typeof(object) || target.IsInstanceOfType(expected))
		{
			return true;
		}

		if (target.IsEnum)
		{
			return expected.GetType().IsEnum
				|| (expected is string name && TryParseEnum(target, name, out _))
				|| TryWiden(expected, Enum.GetUnderlyingType(target), out _);
		}

		return TryWiden(expected, target, out _);
	}

	private static bool MatchesAsEnum(object actual, object expected, Type enumType)
	{
		// An enum member's name is what the README's own example puts in the payload, so a consumer
		// writing that name in the attribute is the likely mistake rather than the exotic one.
		if (actual is string actualName)
		{
			return TryParseEnum(enumType, actualName, out object? parsedActual)
				&& MatchesAsEnum(parsedActual!, expected, enumType);
		}

		if (expected is string expectedName)
		{
			return TryParseEnum(enumType, expectedName, out object? parsedExpected)
				&& MatchesAsEnum(actual, parsedExpected!, enumType);
		}

		Type underlying = Enum.GetUnderlyingType(enumType);

		return TryWiden(actual, underlying, out object? actualValue)
			&& TryWiden(expected, underlying, out object? expectedValue)
			&& actualValue!.Equals(expectedValue);
	}

	private static bool TryParseEnum(Type enumType, string name, out object? value)
	{
		try
		{
			value = Enum.Parse(enumType, name, ignoreCase: false);
			return true;
		}
		catch (ArgumentException)
		{
			value = null;
			return false;
		}
		catch (OverflowException)
		{
			value = null;
			return false;
		}
	}

	/// <summary>
	/// Converts a value to a target type invariantly, reporting failure rather than throwing.
	/// </summary>
	/// <param name="value">The value to convert.</param>
	/// <param name="targetType">The type to convert it to.</param>
	/// <param name="widened">The converted value, or null when conversion is not possible.</param>
	/// <returns>True when the value was converted.</returns>
	/// <remarks>
	/// <see cref="IntPtr"/> and <see cref="UIntPtr"/> are handled explicitly because neither
	/// implements <see cref="IConvertible"/>, so <see cref="Convert.ChangeType(object, Type, IFormatProvider)"/>
	/// alone would reject an <c>nint</c> sibling compared against an ordinary integer constant.
	/// </remarks>
	private static bool TryWiden(object value, Type targetType, out object? widened)
	{
		if (targetType == typeof(string))
		{
			// A string sibling is compared ordinally against a string constant. Formatting a
			// non-string constant into a string to make it match would be a silent coercion no
			// author asked for, so it is treated as unconvertible instead.
			widened = null;
			return false;
		}

		try
		{
			// `checked` so that a constant outside the platform's pointer range surfaces as an
			// OverflowException -- caught below and reported as "cannot convert" -- rather than
			// silently wrapping into a value that happens to match.
			if (targetType == typeof(IntPtr))
			{
				widened = checked((IntPtr)Convert.ToInt64(value, CultureInfo.InvariantCulture));
				return true;
			}

			if (targetType == typeof(UIntPtr))
			{
				widened = checked((UIntPtr)Convert.ToUInt64(value, CultureInfo.InvariantCulture));
				return true;
			}

			widened = Convert.ChangeType(value, targetType, CultureInfo.InvariantCulture);
			return true;
		}
		catch (InvalidCastException)
		{
			widened = null;
			return false;
		}
		catch (FormatException)
		{
			widened = null;
			return false;
		}
		catch (OverflowException)
		{
			widened = null;
			return false;
		}
		catch (ArgumentException)
		{
			widened = null;
			return false;
		}
	}
}
