// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.JsonRequiredConditionally;

using System.Collections.Concurrent;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;

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

		if (TryMatchAsSingle(actual, expected, out bool matchesAsSingle))
		{
			return matchesAsSingle;
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
	/// fires. A null expected value is admissible for any sibling that can hold null -- a reference
	/// type or a <see cref="Nullable{T}"/> -- and it matches a null sibling value. A non-nullable
	/// value-type sibling (<c>int</c>, an enum, <see cref="Guid"/>, ...) is never null, so a null
	/// constant against one is rejected.
	/// <para>
	/// A sibling declared as <see cref="object"/>, <see cref="JsonElement"/> or <see cref="JsonNode"/>
	/// is rejected for every non-null constant (see <see cref="IsOpaqueSiblingType"/>).
	/// </para>
	/// <para>
	/// An array constant is rejected against every sibling type. <see cref="Matches"/> compares by
	/// <see cref="object.Equals(object)"/>, which for an array is reference equality, so a
	/// deserialized array -- or the <see cref="List{T}"/> behind an interface-typed sibling -- never
	/// equals the attribute's array, even though the sibling's declared type can hold it.
	/// </para>
	/// </remarks>
	internal static bool CanEverMatch(Type siblingType, object? expected)
	{
		if (expected is null)
		{
			return !siblingType.IsValueType || Nullable.GetUnderlyingType(siblingType) is not null;
		}

		if (expected is Array)
		{
			return false;
		}

		Type target = Nullable.GetUnderlyingType(siblingType) ?? siblingType;

		if (IsOpaqueSiblingType(target))
		{
			return false;
		}

		if (target.IsInstanceOfType(expected))
		{
			return true;
		}

		if (target.IsEnum)
		{
			// A constant of the sibling's own enum was accepted above, so an enum here is a different one. It
			// goes through TryWiden like any integer, so a member outside the sibling's underlying range, such
			// as -1 against a byte-backed enum, is refused rather than compiling into a rule that never fires (#119).
			return (expected is string name && TryParseEnum(target, name, out _))
				|| TryWiden(expected, Enum.GetUnderlyingType(target), out _);
		}

		if (target == typeof(double) && expected is float)
		{
			return true;
		}

		if (target == typeof(float) && expected is double constant)
		{
			return TryNarrowToSingle(constant, out _);
		}

		return TryWiden(expected, target, out _);
	}

	/// <summary>
	/// Compares a <see cref="float"/> against a <see cref="double"/> at <see cref="float"/> precision,
	/// whichever of the two is the sibling and whichever the constant.
	/// </summary>
	/// <param name="actual">The sibling's runtime value.</param>
	/// <param name="expected">The constant supplied to the attribute.</param>
	/// <param name="matches">Whether the values are equal at <see cref="float"/> precision.</param>
	/// <returns>True when the pairing is one <see cref="float"/> and one <see cref="double"/>.</returns>
	/// <remarks>
	/// Exact widening gets this pairing wrong in both directions (#111). <c>0.1f</c> widens to the
	/// double <c>0.10000000149011612</c>, which no payload <c>0.1</c> deserializes to, so a rule
	/// written against a double sibling compiled and never fired. <c>0.1</c> narrows to <c>0.1f</c>
	/// without round-tripping, so a rule against a float sibling was refused, although a payload
	/// <c>0.1</c> deserializes to exactly <c>0.1f</c>. The author of either rule wrote the same
	/// number, and <see cref="float"/> is the precision both sides share, so that is where they meet.
	/// <para>
	/// The comparison is <see cref="float.CompareTo(float)"/> rather than <c>==</c>, so that NaN matches
	/// NaN as it does through <see cref="object.Equals(object)"/> for every other pairing.
	/// </para>
	/// </remarks>
	private static bool TryMatchAsSingle(object actual, object expected, out bool matches)
	{
		if (actual is double actualDouble && expected is float expectedSingle)
		{
			matches = TryNarrowToSingle(actualDouble, out float narrowed) && narrowed.CompareTo(expectedSingle) == 0;
			return true;
		}

		if (actual is float actualSingle && expected is double expectedDouble)
		{
			matches = TryNarrowToSingle(expectedDouble, out float narrowed) && actualSingle.CompareTo(narrowed) == 0;
			return true;
		}

		matches = false;
		return false;
	}

	/// <summary>
	/// Rounds a <see cref="double"/> to the nearest <see cref="float"/>, refusing one outside its range.
	/// </summary>
	/// <param name="value">The value to round.</param>
	/// <param name="narrowed">The nearest <see cref="float"/>.</param>
	/// <returns>False when a finite <paramref name="value"/> overflows to an infinity.</returns>
	private static bool TryNarrowToSingle(double value, out float narrowed)
	{
		narrowed = (float)value;
		return !float.IsInfinity(narrowed) || double.IsInfinity(value);
	}

	/// <summary>
	/// Determines whether a sibling's declared type leaves its value as an uninterpreted JSON tree.
	/// </summary>
	/// <param name="siblingType">The sibling member's declared type, with any <see cref="Nullable{T}"/> removed.</param>
	/// <returns>True for <see cref="object"/>, <see cref="JsonElement"/> and any <see cref="JsonNode"/> type.</returns>
	/// <remarks>
	/// System.Text.Json materializes an <see cref="object"/> member as a boxed <see cref="JsonElement"/>,
	/// and a <see cref="JsonElement"/> or <see cref="JsonNode"/> member as itself. None of them ever
	/// equals an attribute constant, so a rule against one compiles and then never fires.
	/// </remarks>
	internal static bool IsOpaqueSiblingType(Type siblingType) =>
		siblingType == typeof(object)
		|| siblingType == typeof(JsonElement)
		|| typeof(JsonNode).IsAssignableFrom(siblingType);

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

	/// <summary>
	/// Resolves a string to an enum member only when it is exactly one of the enum's declared names.
	/// </summary>
	/// <remarks>
	/// <see cref="Enum.Parse(Type, string, bool)"/> on its own also accepts numeric strings
	/// (<c>"1"</c>, or an undefined <c>"999"</c>), surrounding whitespace (<c>" Advanced "</c>) and
	/// comma-separated combinations (<c>"Basic, Advanced"</c>), even on an enum that is not
	/// <see cref="FlagsAttribute"/>. None of those is a member's own name, and reading a number out of
	/// a string is the coercion <see cref="TryWiden"/> refuses, so they are rejected here and
	/// <see cref="CanEverMatch"/> reports the rule as one that can never match.
	/// </remarks>
	private static bool TryParseEnum(Type enumType, string name, out object? value)
	{
		if (Array.IndexOf(Enum.GetNames(enumType), name) < 0)
		{
			value = null;
			return false;
		}

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
	/// <para>
	/// Every conversion is required to preserve the value; see <see cref="PreservesValue"/>.
	/// </para>
	/// </remarks>
	private static bool TryWiden(object value, Type targetType, out object? widened) =>
		IsWideNumber(targetType)
			? TryWidenToWideNumber(value, targetType, out widened)
			: TryWidenPrimitive(value, targetType, out widened);

	/// <summary>
	/// Converts a value between the numeric primitives <see cref="IsNumeric"/> accepts.
	/// </summary>
	/// <param name="value">The value to convert.</param>
	/// <param name="targetType">The type to convert it to.</param>
	/// <param name="widened">The converted value, or null when conversion is not possible.</param>
	/// <returns>True when the value was converted.</returns>
	private static bool TryWidenPrimitive(object value, Type targetType, out object? widened)
	{
		Type valueType = value.GetType();

		if (!IsNumeric(targetType) || !IsNumeric(valueType.IsEnum ? Enum.GetUnderlyingType(valueType) : valueType))
		{
			// Only a number widens to another number. Formatting a number into a string sibling,
			// parsing a string constant into a number or bool sibling, or reading 0 as false or 65
			// as 'A' would each be a silent coercion no author asked for -- and Convert.ChangeType's
			// version of it depends on formatting ("True" parses, "true" round-trips differently,
			// "01" does not round-trip at all). Treating them as unconvertible lets CanEverMatch
			// reject the pairing when the rule compiles.
			widened = null;
			return false;
		}

		try
		{
			// `checked` so that a constant outside the platform's pointer range surfaces as an
			// OverflowException -- caught below and reported as "cannot convert" -- rather than
			// silently wrapping into a value that happens to match. Neither pointer type is
			// IConvertible, so each round-trips through its integer form rather than itself.
			if (targetType == typeof(IntPtr))
			{
				IntPtr pointer = checked((IntPtr)Convert.ToInt64(value, CultureInfo.InvariantCulture));

				widened = PreservesValue(value, (long)pointer) ? pointer : null;
				return widened is not null;
			}

			if (targetType == typeof(UIntPtr))
			{
				UIntPtr pointer = checked((UIntPtr)Convert.ToUInt64(value, CultureInfo.InvariantCulture));

				widened = PreservesValue(value, (ulong)pointer) ? pointer : null;
				return widened is not null;
			}

			object converted = Convert.ChangeType(value, targetType, CultureInfo.InvariantCulture);

			widened = PreservesValue(value, converted) ? converted : null;
			return widened is not null;
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

	/// <summary>
	/// Determines whether a type is <c>System.Int128</c>, <c>System.UInt128</c> or <c>System.Half</c>.
	/// </summary>
	/// <param name="type">The type to test.</param>
	/// <returns>True for the three numeric types System.Text.Json reads natively that are not <see cref="IConvertible"/>.</returns>
	/// <remarks>
	/// They are recognized by name because netstandard2.0 has none of them, and this library uses no
	/// conditional compilation. A C# attribute cannot hold a constant of any of them, so a rule against
	/// one is always written with an ordinary <c>int</c>, <c>long</c> or <c>double</c> literal (#124).
	/// </remarks>
	private static bool IsWideNumber(Type type) =>
		type.Assembly == typeof(object).Assembly
		&& type.FullName is "System.Int128" or "System.UInt128" or "System.Half";

	/// <summary>
	/// Converts a numeric constant to <c>Int128</c>, <c>UInt128</c> or <c>Half</c> through the target
	/// type's own conversion operators, reporting failure rather than throwing.
	/// </summary>
	/// <param name="value">The constant to convert.</param>
	/// <param name="targetType">One of the types <see cref="IsWideNumber"/> accepts.</param>
	/// <param name="widened">The converted value, or null when no value of the target type equals it.</param>
	/// <returns>True when the value was converted.</returns>
	/// <remarks>
	/// None of the three implements <see cref="IConvertible"/>, so <see cref="Convert.ChangeType(object, Type, IFormatProvider)"/>
	/// cannot reach them. An integer constant, and a floating-point one against <c>Int128</c> or
	/// <c>UInt128</c>, must convert exactly, as <see cref="PreservesValue"/> requires elsewhere: a
	/// negative constant against <c>UInt128</c> or <c>2.5</c> against <c>Int128</c> is refused. A
	/// <see cref="float"/> or <see cref="double"/> constant against <c>Half</c> is instead rounded to the
	/// nearest <c>Half</c>, the precision a payload carrying the same number deserializes to, as
	/// <see cref="TryMatchAsSingle"/> does for <see cref="float"/> against <see cref="double"/> (#111).
	/// It is refused only when it overflows to an infinity.
	/// </remarks>
	private static bool TryWidenToWideNumber(object value, Type targetType, out object? widened)
	{
		widened = null;

		if (value.GetType().IsEnum)
		{
			value = Convert.ChangeType(value, Enum.GetUnderlyingType(value.GetType()), CultureInfo.InvariantCulture);
		}

		if (!IsNumeric(value.GetType()) || value is IntPtr or UIntPtr)
		{
			return false;
		}

		try
		{
			widened = targetType.FullName == "System.Half"
				? WidenToHalf(value, targetType)
				: WidenToInteger128(value, targetType);
			return widened is not null;
		}
		catch (Exception exception) when (exception is TargetInvocationException or OverflowException or InvalidCastException or NotSupportedException)
		{
			widened = null;
			return false;
		}
	}

	/// <summary>
	/// Converts a numeric primitive to <c>Half</c>, through <see cref="double"/>.
	/// </summary>
	/// <param name="value">A numeric primitive other than a pointer.</param>
	/// <param name="halfType">The <c>System.Half</c> type.</param>
	/// <returns>The <c>Half</c>, or null when no <c>Half</c> equals the constant.</returns>
	/// <remarks>
	/// A <see cref="float"/> or <see cref="double"/> constant is rounded to the nearest <c>Half</c> and
	/// refused only when it overflows to an infinity. Any other constant must come back unchanged when
	/// the <c>Half</c> is converted to the constant's own type: that refuses 2049, which rounds to 2048,
	/// and an infinity, which no integer or decimal can hold.
	/// </remarks>
	private static object? WidenToHalf(object value, Type halfType)
	{
		double number = Convert.ToDouble(value, CultureInfo.InvariantCulture);
		object half = ApplyConversionOperator(typeof(double), halfType, number);
		double back = (double)ApplyConversionOperator(halfType, typeof(double), half);

		if (value is float or double)
		{
			return double.IsInfinity(back) && !double.IsInfinity(number) ? null : half;
		}

		return PreservesValue(value, back) ? half : null;
	}

	/// <summary>
	/// Converts a numeric primitive to <c>Int128</c> or <c>UInt128</c>.
	/// </summary>
	/// <param name="value">A numeric primitive other than a pointer.</param>
	/// <param name="targetType">The <c>System.Int128</c> or <c>System.UInt128</c> type.</param>
	/// <returns>The converted value, or null when no value of the target type equals the constant.</returns>
	/// <remarks>
	/// A floating-point or decimal constant goes through the target's own operator from that type and
	/// must convert back to itself, which refuses a fraction, a NaN and anything out of range. An integer
	/// goes through <see cref="long"/> or <see cref="ulong"/>, refusing a negative one for <c>UInt128</c>.
	/// </remarks>
	private static object? WidenToInteger128(object value, Type targetType)
	{
		Type valueType = value.GetType();

		if (value is float or double or decimal)
		{
			object converted = ApplyConversionOperator(valueType, targetType, value);

			return ApplyConversionOperator(targetType, valueType, converted).Equals(value) ? converted : null;
		}

		if (value is ulong large)
		{
			return ApplyConversionOperator(typeof(ulong), targetType, large);
		}

		long integer = Convert.ToInt64(value, CultureInfo.InvariantCulture);

		if (targetType.FullName != "System.UInt128")
		{
			return ApplyConversionOperator(typeof(long), targetType, integer);
		}

		return integer < 0 ? null : ApplyConversionOperator(typeof(ulong), targetType, (ulong)integer);
	}

	private static readonly ConcurrentDictionary<(Type From, Type To), MethodInfo?> ConversionOperators = new();

	/// <summary>
	/// Applies the conversion operator from <paramref name="from"/> to <paramref name="to"/> that one of
	/// the <see cref="IsWideNumber"/> types declares.
	/// </summary>
	/// <exception cref="NotSupportedException">Neither type declares such an operator.</exception>
	private static object ApplyConversionOperator(Type from, Type to, object value)
	{
		MethodInfo? method = ConversionOperators.GetOrAdd((from, to), static key =>
			FindConversionOperator(key.From, key.From, key.To) ?? FindConversionOperator(key.To, key.From, key.To));

		return method is null
			? throw new NotSupportedException($"No conversion from {from} to {to}.")
			: method.Invoke(null, [value])!;
	}

	private static MethodInfo? FindConversionOperator(Type declaringType, Type from, Type to)
	{
		IEnumerable<MethodInfo> operators = declaringType.GetMethods(BindingFlags.Public | BindingFlags.Static)
			.Where(method => method.Name is "op_Explicit" or "op_Implicit" && method.ReturnType == to);

		foreach (MethodInfo method in operators)
		{
			ParameterInfo[] parameters = method.GetParameters();

			if (parameters.Length == 1 && parameters[0].ParameterType == from)
			{
				return method;
			}
		}

		return null;
	}

	/// <summary>
	/// Determines whether a type is one of the numeric primitives <see cref="TryWiden"/> converts between.
	/// </summary>
	/// <param name="type">The type to test.</param>
	/// <returns>True for the integral and floating-point primitives, <see cref="decimal"/>, <see cref="IntPtr"/> and <see cref="UIntPtr"/>.</returns>
	private static bool IsNumeric(Type type) =>
		type == typeof(sbyte)
		|| type == typeof(byte)
		|| type == typeof(short)
		|| type == typeof(ushort)
		|| type == typeof(int)
		|| type == typeof(uint)
		|| type == typeof(long)
		|| type == typeof(ulong)
		|| type == typeof(float)
		|| type == typeof(double)
		|| type == typeof(decimal)
		|| type == typeof(IntPtr)
		|| type == typeof(UIntPtr);

	/// <summary>
	/// Determines whether a conversion kept the value it started from.
	/// </summary>
	/// <param name="original">The value that was converted.</param>
	/// <param name="converted">The result of converting it.</param>
	/// <returns>True when converting back yields the original value.</returns>
	/// <remarks>
	/// <see cref="Convert"/> narrows by rounding rather than by refusing, so a fractional constant
	/// compared against an integral sibling would otherwise become a whole number and match a
	/// sibling value it does not equal -- every <c>int</c> sibling holding 2 would satisfy a rule
	/// written against 2.4. Requiring the original value back rejects that, and any other narrowing
	/// that silently loses information, while leaving an exact widening admissible: 1 against a
	/// <c>long</c> sibling and 2.0 against an <c>int</c> one both round-trip unchanged.
	/// </remarks>
	private static bool PreservesValue(object original, object converted)
	{
		Type originalType = original.GetType();

		if (originalType.IsEnum)
		{
			// Convert.ChangeType cannot target an enum type, and an enum value is exactly its
			// underlying integer, so the round-trip is compared there instead.
			originalType = Enum.GetUnderlyingType(originalType);
			original = Convert.ChangeType(original, originalType, CultureInfo.InvariantCulture);
		}

		return Convert.ChangeType(converted, originalType, CultureInfo.InvariantCulture).Equals(original);
	}
}
