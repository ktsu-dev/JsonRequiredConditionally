// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace ktsu.JsonRequiredConditionally;

using System.Text.Json;

/// <summary>
/// Collects the immediate property names of a JSON object.
/// </summary>
internal static class PresenceScanner
{
	/// <summary>
	/// Collects the names of the properties physically present on a JSON object.
	/// </summary>
	/// <param name="element">The element to inspect.</param>
	/// <param name="comparer">The comparer matching the serializer's case sensitivity.</param>
	/// <returns>The set of property names present on the object, empty for any other value kind.</returns>
	internal static HashSet<string> ScanPropertyNames(JsonElement element, StringComparer comparer)
	{
		HashSet<string> names = new(comparer);

		if (element.ValueKind != JsonValueKind.Object)
		{
			return names;
		}

		foreach (JsonProperty property in element.EnumerateObject())
		{
			names.Add(property.Name);
		}

		return names;
	}
}
