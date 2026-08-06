// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace ktsu.JsonRequiredConditionally.Tests;

using System.Diagnostics.CodeAnalysis;
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

/// <summary>Base type whose sibling a derived type hides with a differently-typed member.</summary>
public class HidingBaseConfig
{
	public object? Kind { get; set; }
}

/// <summary>
/// Hides the base sibling with `new`. The hiding member has a different type than the hidden one,
/// which is what makes <see cref="Type.GetProperty(string, System.Reflection.BindingFlags)"/> report
/// an ambiguous match instead of silently resolving to the most-derived member.
/// </summary>
public sealed class HidingConfig : HidingBaseConfig
{
	public new Kind Kind { get; set; }

	[JsonRequiredIfSiblingIs(nameof(Kind), Kind.Advanced)]
	public string? Tuning { get; set; }
}

/// <summary>Holds a decorated child, to prove nested validation runs.</summary>
public sealed class OuterConfig
{
	public string? Label { get; set; }

	public SimpleConfig? Child { get; set; }
}

/// <summary>Holds decorated children in collections.</summary>
public sealed class CollectionConfig
{
	[SuppressMessage("Design", "CA1002:Do not expose generic lists", Justification = "Test fixture round-trips through JSON deserialization, which requires a settable collection property.")]
	[SuppressMessage("Usage", "CA2227:Collection properties should be read only", Justification = "Test fixture round-trips through JSON deserialization, which requires a settable collection property.")]
	public List<SimpleConfig> Items { get; set; } = [];

	[SuppressMessage("Usage", "CA2227:Collection properties should be read only", Justification = "Test fixture round-trips through JSON deserialization, which requires a settable collection property.")]
	public Dictionary<string, SimpleConfig> Lookup { get; set; } = [];
}

/// <summary>Mutually recursive with <see cref="NodeB"/>.</summary>
public sealed class NodeA
{
	public Kind Kind { get; set; }

	[JsonRequiredIfSiblingIs(nameof(Kind), Kind.Advanced)]
	public string? Tuning { get; set; }

	public NodeB? Next { get; set; }
}

/// <summary>Mutually recursive with <see cref="NodeA"/>.</summary>
public sealed class NodeB
{
	public Mode Mode { get; set; }

	[JsonRequiredIfSiblingIs(nameof(Mode), Mode.Remote)]
	public string? Endpoint { get; set; }

	public NodeA? Next { get; set; }
}

/// <summary>Directly self-referential — the case the previous design silently skipped.</summary>
public sealed class TreeNode
{
	public Kind Kind { get; set; }

	[JsonRequiredIfSiblingIs(nameof(Kind), Kind.Advanced)]
	public string? Tuning { get; set; }

	public TreeNode? Child { get; set; }
}

/// <summary>Self-referential through a collection.</summary>
public sealed class BranchNode
{
	public Kind Kind { get; set; }

	[JsonRequiredIfSiblingIs(nameof(Kind), Kind.Advanced)]
	public string? Tuning { get; set; }

	[SuppressMessage("Usage", "CA2227:Collection properties should be read only", Justification = "Test fixture requires a settable collection for deserialization.")]
	[SuppressMessage("Design", "CA1002:Do not expose generic lists", Justification = "Test fixture mirrors the shape a consumer would write.")]
	public List<BranchNode> Children { get; set; } = [];
}

/// <summary>Holds an object-typed member that materializes as a boxed <see cref="System.Text.Json.JsonElement"/>.</summary>
public sealed class PayloadConfig
{
	public Kind Kind { get; set; }

	[JsonRequiredIfSiblingIs(nameof(Kind), Kind.Advanced)]
	public string? Tuning { get; set; }

	public object? Payload { get; set; }
}

/// <summary>A member excluded from serialization whose name collides with an unrelated JSON property.</summary>
public sealed class IgnoredMemberConfig
{
	public Kind Kind { get; set; }

	[JsonIgnore]
	public SimpleConfig Hidden { get; set; } = new() { Kind = Kind.Advanced };
}

/// <summary>Holds a decorated child behind a non-string-keyed dictionary.</summary>
public sealed class IntKeyedDictionaryConfig
{
	[SuppressMessage("Usage", "CA2227:Collection properties should be read only", Justification = "Test fixture round-trips through JSON deserialization, which requires a settable collection property.")]
	public Dictionary<int, SimpleConfig> Items { get; set; } = [];
}
