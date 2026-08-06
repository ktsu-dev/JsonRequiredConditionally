// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace ktsu.JsonRequiredConditionally.Tests;

using System.Text.Json.Serialization;

public enum Kind
{
	Basic = 0,
	Advanced = 1,
	Expert = 2,
}

public enum Mode
{
	Local = 0,
	Remote = 1,
}

/// <summary>A single sibling condition on one enum value.</summary>
public sealed class SimpleConfig
{
	public Kind Kind { get; set; }

	[JsonRequiredIfSiblingIs(nameof(Kind), Kind.Advanced)]
	public string? Tuning { get; set; }
}

/// <summary>Two values of the same sibling: OR.</summary>
public sealed class OrConfig
{
	public Kind Kind { get; set; }

	[JsonRequiredIfSiblingIs(nameof(Kind), Kind.Advanced)]
	[JsonRequiredIfSiblingIs(nameof(Kind), Kind.Expert)]
	public string? Tuning { get; set; }
}

/// <summary>Two different siblings: AND.</summary>
public sealed class AndConfig
{
	public Kind Kind { get; set; }

	public Mode Mode { get; set; }

	[JsonRequiredIfSiblingIs(nameof(Kind), Kind.Advanced)]
	[JsonRequiredIfSiblingIs(nameof(Mode), Mode.Remote)]
	public string? Tuning { get; set; }
}

/// <summary>Explicit JSON name overriding any naming policy.</summary>
public sealed class RenamedConfig
{
	public Kind Kind { get; set; }

	[JsonPropertyName("tuning_value")]
	[JsonRequiredIfSiblingIs(nameof(Kind), Kind.Advanced)]
	public string? Tuning { get; set; }
}

/// <summary>No attributes at all; must bypass the converter entirely.</summary>
public sealed class PlainConfig
{
	public Kind Kind { get; set; }

	public string? Tuning { get; set; }
}

/// <summary>Names a sibling that does not exist.</summary>
public sealed class BrokenConfig
{
	public Kind Kind { get; set; }

	[JsonRequiredIfSiblingIs("NoSuchMember", Kind.Advanced)]
	public string? Tuning { get; set; }
}
