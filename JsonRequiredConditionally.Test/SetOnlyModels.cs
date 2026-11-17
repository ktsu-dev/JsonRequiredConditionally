// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.JsonRequiredConditionally.Tests;

using System.Diagnostics.CodeAnalysis;

/// <summary>A claimed holder whose decorated child sits behind a set-only property (#123).</summary>
public sealed class SetOnlyChildHolder
{
	public Kind Kind { get; set; }

	[JsonRequiredIfSiblingIs(nameof(Kind), Kind.Advanced)]
	public string? Tuning { get; set; }

	[SuppressMessage("Design", "CA1044:Properties should not be write only", Justification = "The write-only property is the shape under test (#123).")]
	public SimpleConfig? Child { set => Stored = value; }

	public SimpleConfig? Stored { get; private set; }
}

/// <summary>The same set-only child on a holder with no decorated member of its own, so it is not claimed (#123).</summary>
public sealed class UnclaimedSetOnlyChildHolder
{
	[SuppressMessage("Design", "CA1044:Properties should not be write only", Justification = "The write-only property is the shape under test (#123).")]
	public SimpleConfig? Child { set => Stored = value; }

	public SimpleConfig? Stored { get; private set; }
}

/// <summary>A claimed holder whose decorated elements sit behind a set-only collection property (#123).</summary>
public sealed class SetOnlyListHolder
{
	public Kind Kind { get; set; }

	[JsonRequiredIfSiblingIs(nameof(Kind), Kind.Advanced)]
	public string? Tuning { get; set; }

	[SuppressMessage("Design", "CA1044:Properties should not be write only", Justification = "The write-only property is the shape under test (#123).")]
	[SuppressMessage("Design", "CA1002:Do not expose generic lists", Justification = "Test fixture round-trips through JSON deserialization.")]
	[SuppressMessage("Usage", "CA2227:Collection properties should be read only", Justification = "The set-only collection property is the shape under test (#123).")]
	public List<SimpleConfig>? Items { set => Received = value?.Count; }

	public int? Received { get; private set; }
}

/// <summary>A claimed holder reaching an unclaimed set-only holder one level down (#123).</summary>
public sealed class NestedSetOnlyChildHolder
{
	public Kind Kind { get; set; }

	[JsonRequiredIfSiblingIs(nameof(Kind), Kind.Advanced)]
	public string? Tuning { get; set; }

	public UnclaimedSetOnlyChildHolder? Inner { get; set; }
}

/// <summary>A claimed holder whose set-only property reaches no decorated member, which stays supported.</summary>
public sealed class SetOnlyPlainMemberHolder
{
	public Kind Kind { get; set; }

	[JsonRequiredIfSiblingIs(nameof(Kind), Kind.Advanced)]
	public string? Tuning { get; set; }

	[SuppressMessage("Design", "CA1044:Properties should not be write only", Justification = "The write-only property is the shape under test (#123).")]
	public string? Note { set => Stored = value; }

	public string? Stored { get; private set; }
}
