// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace ktsu.JsonRequiredConditionally.Tests;

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

/// <summary>
/// Detects System.Text.Json capabilities that differ across the versions this test project runs
/// against, by asking the serializer itself rather than by branching on a target framework.
/// </summary>
/// <remarks>
/// The test project is multi-targeted so each in-box System.Text.Json version is genuinely
/// exercised, and a few scenarios simply cannot be expressed on the older ones. Probing the
/// capability keeps those tests asserting something true on every framework instead of being
/// disabled on the ones where the scenario does not exist.
/// </remarks>
internal static class SerializerCapabilities
{
	private static readonly JsonSerializerOptions ProbeOptions =
		new() { TypeInfoResolver = new DefaultJsonTypeInfoResolver() };

	/// <summary>
	/// Gets a value indicating whether this framework's System.Text.Json accepts
	/// <c>[JsonInclude]</c> on a non-public property.
	/// </summary>
	/// <remarks>
	/// System.Text.Json 7 rejects it outright, throwing
	/// <c>InvalidOperationException: The non-public property 'X' on type 'Y' is annotated with
	/// 'JsonIncludeAttribute' which is invalid</c> while building the type's contract -- before any
	/// converter is consulted, and identically whether or not this library is registered.
	/// System.Text.Json 8 and later allow it.
	/// </remarks>
	internal static bool SupportsJsonIncludeOnNonPublicProperties { get; } = Probe();

	/// <remarks>
	/// Probed by actually deserializing rather than by asking for the type's
	/// <see cref="JsonTypeInfo"/>: System.Text.Json 7 builds the metadata happily and only rejects
	/// the annotation when the contract is configured, on first use.
	/// </remarks>
	private static bool Probe()
	{
		try
		{
			JsonSerializer.Deserialize<NonPublicIncludeProbe>("""{"Value":"x"}""", ProbeOptions);
			return true;
		}
		catch (InvalidOperationException)
		{
			return false;
		}
	}

	[SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes", Justification = "Instantiated only by System.Text.Json's contract resolver, reflectively, while probing.")]
	private sealed class NonPublicIncludeProbe
	{
		[JsonInclude]
		internal string? Value { get; set; }
	}
}
