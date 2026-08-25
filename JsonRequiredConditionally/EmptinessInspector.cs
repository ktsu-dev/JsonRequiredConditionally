// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.JsonRequiredConditionally;

using System.Text.Json;

/// <summary>
/// Answers whether a JSON value carries nothing.
/// </summary>
/// <remarks>
/// Emptiness is judged from the payload element rather than from the materialized CLR value. That is
/// what lets it answer correctly for a type behind its own <see cref="System.Text.Json.Serialization.JsonConverter"/>,
/// whose <see cref="System.Text.Json.Serialization.Metadata.JsonTypeInfo.Properties"/> System.Text.Json
/// leaves empty and whose CLR shape the library therefore cannot inspect. The cost is that a converter
/// mapping a non-empty JSON representation onto an empty collection is judged non-empty.
/// </remarks>
internal static class EmptinessInspector
{
	/// <summary>
	/// Determines whether a JSON value is empty.
	/// </summary>
	/// <param name="element">The value to inspect.</param>
	/// <returns>
	/// True for null, a zero-length string, a zero-element array, and an object with no properties.
	/// False for every other value, including numbers, booleans and
	/// <see cref="JsonValueKind.Undefined"/>.
	/// </returns>
	/// <remarks>
	/// A whitespace-only string is <em>not</em> empty. This follows the framework's own definition,
	/// under which a string is empty when its length is zero, and diverges deliberately from
	/// <c>System.ComponentModel.DataAnnotations.RequiredAttribute</c>, which treats whitespace as absent.
	/// Absence is not answered here: the caller distinguishes an absent property from a present but
	/// empty one, because the two are reported in different categories.
	/// </remarks>
	internal static bool IsEmpty(JsonElement element)
	{
		switch (element.ValueKind)
		{
			case JsonValueKind.Null:
				return true;

			case JsonValueKind.String:
				// ValueEquals compares against the raw UTF-8 payload, so no string is materialized
				// just to measure its length.
				return element.ValueEquals(string.Empty);

			case JsonValueKind.Array:
				return element.GetArrayLength() == 0;

			case JsonValueKind.Object:
			{
				JsonElement.ObjectEnumerator properties = element.EnumerateObject();

				return !properties.MoveNext();
			}

			default:
				return false;
		}
	}
}
