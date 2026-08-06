// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace ktsu.JsonRequiredConditionally;

using System.Text.Json;

/// <summary>
/// Collects the immediate property names of a JSON object without materializing it.
/// </summary>
internal static class PresenceScanner
{
	/// <summary>
	/// Reads forward over a copy of the reader, collecting the current object's property names.
	/// </summary>
	/// <param name="reader">A copy of the caller's reader, parked on <see cref="JsonTokenType.StartObject"/>.</param>
	/// <param name="comparer">The comparer matching the serializer's case sensitivity.</param>
	/// <returns>The set of property names physically present on the object.</returns>
	internal static HashSet<string> ScanPropertyNames(Utf8JsonReader reader, StringComparer comparer)
	{
		HashSet<string> names = new(comparer);

		if (reader.TokenType != JsonTokenType.StartObject)
		{
			return names;
		}

		while (reader.Read())
		{
			if (reader.TokenType == JsonTokenType.EndObject)
			{
				break;
			}

			if (reader.TokenType != JsonTokenType.PropertyName)
			{
				continue;
			}

			names.Add(reader.GetString()!);

			reader.Read();
			reader.Skip();
		}

		return names;
	}
}
