// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.JsonRequiredConditionally.Tests;

// Two ten-type cliques, in which every type has a property of every other type. They exist only so
// EligibilityMemoizationTests can time eligibility over the densest type graph there is: without
// memoization the reachability walk enumerates every simple path, which is factorial in the size
// of the clique. Each clique is used by one test only, because eligibility is cached per type for
// the life of the process.

/// <summary>One of ten mutually-referencing types, none of them decorated.</summary>
public sealed class UndecoratedClique0
{
	public UndecoratedClique1? Node1 { get; set; }

	public UndecoratedClique2? Node2 { get; set; }

	public UndecoratedClique3? Node3 { get; set; }

	public UndecoratedClique4? Node4 { get; set; }

	public UndecoratedClique5? Node5 { get; set; }

	public UndecoratedClique6? Node6 { get; set; }

	public UndecoratedClique7? Node7 { get; set; }

	public UndecoratedClique8? Node8 { get; set; }

	public UndecoratedClique9? Node9 { get; set; }
}

/// <summary>One of ten mutually-referencing types, none of them decorated.</summary>
public sealed class UndecoratedClique1
{
	public UndecoratedClique0? Node0 { get; set; }

	public UndecoratedClique2? Node2 { get; set; }

	public UndecoratedClique3? Node3 { get; set; }

	public UndecoratedClique4? Node4 { get; set; }

	public UndecoratedClique5? Node5 { get; set; }

	public UndecoratedClique6? Node6 { get; set; }

	public UndecoratedClique7? Node7 { get; set; }

	public UndecoratedClique8? Node8 { get; set; }

	public UndecoratedClique9? Node9 { get; set; }
}

/// <summary>One of ten mutually-referencing types, none of them decorated.</summary>
public sealed class UndecoratedClique2
{
	public UndecoratedClique0? Node0 { get; set; }

	public UndecoratedClique1? Node1 { get; set; }

	public UndecoratedClique3? Node3 { get; set; }

	public UndecoratedClique4? Node4 { get; set; }

	public UndecoratedClique5? Node5 { get; set; }

	public UndecoratedClique6? Node6 { get; set; }

	public UndecoratedClique7? Node7 { get; set; }

	public UndecoratedClique8? Node8 { get; set; }

	public UndecoratedClique9? Node9 { get; set; }
}

/// <summary>One of ten mutually-referencing types, none of them decorated.</summary>
public sealed class UndecoratedClique3
{
	public UndecoratedClique0? Node0 { get; set; }

	public UndecoratedClique1? Node1 { get; set; }

	public UndecoratedClique2? Node2 { get; set; }

	public UndecoratedClique4? Node4 { get; set; }

	public UndecoratedClique5? Node5 { get; set; }

	public UndecoratedClique6? Node6 { get; set; }

	public UndecoratedClique7? Node7 { get; set; }

	public UndecoratedClique8? Node8 { get; set; }

	public UndecoratedClique9? Node9 { get; set; }
}

/// <summary>One of ten mutually-referencing types, none of them decorated.</summary>
public sealed class UndecoratedClique4
{
	public UndecoratedClique0? Node0 { get; set; }

	public UndecoratedClique1? Node1 { get; set; }

	public UndecoratedClique2? Node2 { get; set; }

	public UndecoratedClique3? Node3 { get; set; }

	public UndecoratedClique5? Node5 { get; set; }

	public UndecoratedClique6? Node6 { get; set; }

	public UndecoratedClique7? Node7 { get; set; }

	public UndecoratedClique8? Node8 { get; set; }

	public UndecoratedClique9? Node9 { get; set; }
}

/// <summary>One of ten mutually-referencing types, none of them decorated.</summary>
public sealed class UndecoratedClique5
{
	public UndecoratedClique0? Node0 { get; set; }

	public UndecoratedClique1? Node1 { get; set; }

	public UndecoratedClique2? Node2 { get; set; }

	public UndecoratedClique3? Node3 { get; set; }

	public UndecoratedClique4? Node4 { get; set; }

	public UndecoratedClique6? Node6 { get; set; }

	public UndecoratedClique7? Node7 { get; set; }

	public UndecoratedClique8? Node8 { get; set; }

	public UndecoratedClique9? Node9 { get; set; }
}

/// <summary>One of ten mutually-referencing types, none of them decorated.</summary>
public sealed class UndecoratedClique6
{
	public UndecoratedClique0? Node0 { get; set; }

	public UndecoratedClique1? Node1 { get; set; }

	public UndecoratedClique2? Node2 { get; set; }

	public UndecoratedClique3? Node3 { get; set; }

	public UndecoratedClique4? Node4 { get; set; }

	public UndecoratedClique5? Node5 { get; set; }

	public UndecoratedClique7? Node7 { get; set; }

	public UndecoratedClique8? Node8 { get; set; }

	public UndecoratedClique9? Node9 { get; set; }
}

/// <summary>One of ten mutually-referencing types, none of them decorated.</summary>
public sealed class UndecoratedClique7
{
	public UndecoratedClique0? Node0 { get; set; }

	public UndecoratedClique1? Node1 { get; set; }

	public UndecoratedClique2? Node2 { get; set; }

	public UndecoratedClique3? Node3 { get; set; }

	public UndecoratedClique4? Node4 { get; set; }

	public UndecoratedClique5? Node5 { get; set; }

	public UndecoratedClique6? Node6 { get; set; }

	public UndecoratedClique8? Node8 { get; set; }

	public UndecoratedClique9? Node9 { get; set; }
}

/// <summary>One of ten mutually-referencing types, none of them decorated.</summary>
public sealed class UndecoratedClique8
{
	public UndecoratedClique0? Node0 { get; set; }

	public UndecoratedClique1? Node1 { get; set; }

	public UndecoratedClique2? Node2 { get; set; }

	public UndecoratedClique3? Node3 { get; set; }

	public UndecoratedClique4? Node4 { get; set; }

	public UndecoratedClique5? Node5 { get; set; }

	public UndecoratedClique6? Node6 { get; set; }

	public UndecoratedClique7? Node7 { get; set; }

	public UndecoratedClique9? Node9 { get; set; }
}

/// <summary>One of ten mutually-referencing types, none of them decorated.</summary>
public sealed class UndecoratedClique9
{
	public UndecoratedClique0? Node0 { get; set; }

	public UndecoratedClique1? Node1 { get; set; }

	public UndecoratedClique2? Node2 { get; set; }

	public UndecoratedClique3? Node3 { get; set; }

	public UndecoratedClique4? Node4 { get; set; }

	public UndecoratedClique5? Node5 { get; set; }

	public UndecoratedClique6? Node6 { get; set; }

	public UndecoratedClique7? Node7 { get; set; }

	public UndecoratedClique8? Node8 { get; set; }
}

/// <summary>One of ten mutually-referencing types, of which only the last is decorated.</summary>
public sealed class DecoratedClique0
{
	public DecoratedClique1? Node1 { get; set; }

	public DecoratedClique2? Node2 { get; set; }

	public DecoratedClique3? Node3 { get; set; }

	public DecoratedClique4? Node4 { get; set; }

	public DecoratedClique5? Node5 { get; set; }

	public DecoratedClique6? Node6 { get; set; }

	public DecoratedClique7? Node7 { get; set; }

	public DecoratedClique8? Node8 { get; set; }

	public DecoratedClique9? Node9 { get; set; }
}

/// <summary>One of ten mutually-referencing types, of which only the last is decorated.</summary>
public sealed class DecoratedClique1
{
	public DecoratedClique0? Node0 { get; set; }

	public DecoratedClique2? Node2 { get; set; }

	public DecoratedClique3? Node3 { get; set; }

	public DecoratedClique4? Node4 { get; set; }

	public DecoratedClique5? Node5 { get; set; }

	public DecoratedClique6? Node6 { get; set; }

	public DecoratedClique7? Node7 { get; set; }

	public DecoratedClique8? Node8 { get; set; }

	public DecoratedClique9? Node9 { get; set; }
}

/// <summary>One of ten mutually-referencing types, of which only the last is decorated.</summary>
public sealed class DecoratedClique2
{
	public DecoratedClique0? Node0 { get; set; }

	public DecoratedClique1? Node1 { get; set; }

	public DecoratedClique3? Node3 { get; set; }

	public DecoratedClique4? Node4 { get; set; }

	public DecoratedClique5? Node5 { get; set; }

	public DecoratedClique6? Node6 { get; set; }

	public DecoratedClique7? Node7 { get; set; }

	public DecoratedClique8? Node8 { get; set; }

	public DecoratedClique9? Node9 { get; set; }
}

/// <summary>One of ten mutually-referencing types, of which only the last is decorated.</summary>
public sealed class DecoratedClique3
{
	public DecoratedClique0? Node0 { get; set; }

	public DecoratedClique1? Node1 { get; set; }

	public DecoratedClique2? Node2 { get; set; }

	public DecoratedClique4? Node4 { get; set; }

	public DecoratedClique5? Node5 { get; set; }

	public DecoratedClique6? Node6 { get; set; }

	public DecoratedClique7? Node7 { get; set; }

	public DecoratedClique8? Node8 { get; set; }

	public DecoratedClique9? Node9 { get; set; }
}

/// <summary>One of ten mutually-referencing types, of which only the last is decorated.</summary>
public sealed class DecoratedClique4
{
	public DecoratedClique0? Node0 { get; set; }

	public DecoratedClique1? Node1 { get; set; }

	public DecoratedClique2? Node2 { get; set; }

	public DecoratedClique3? Node3 { get; set; }

	public DecoratedClique5? Node5 { get; set; }

	public DecoratedClique6? Node6 { get; set; }

	public DecoratedClique7? Node7 { get; set; }

	public DecoratedClique8? Node8 { get; set; }

	public DecoratedClique9? Node9 { get; set; }
}

/// <summary>One of ten mutually-referencing types, of which only the last is decorated.</summary>
public sealed class DecoratedClique5
{
	public DecoratedClique0? Node0 { get; set; }

	public DecoratedClique1? Node1 { get; set; }

	public DecoratedClique2? Node2 { get; set; }

	public DecoratedClique3? Node3 { get; set; }

	public DecoratedClique4? Node4 { get; set; }

	public DecoratedClique6? Node6 { get; set; }

	public DecoratedClique7? Node7 { get; set; }

	public DecoratedClique8? Node8 { get; set; }

	public DecoratedClique9? Node9 { get; set; }
}

/// <summary>One of ten mutually-referencing types, of which only the last is decorated.</summary>
public sealed class DecoratedClique6
{
	public DecoratedClique0? Node0 { get; set; }

	public DecoratedClique1? Node1 { get; set; }

	public DecoratedClique2? Node2 { get; set; }

	public DecoratedClique3? Node3 { get; set; }

	public DecoratedClique4? Node4 { get; set; }

	public DecoratedClique5? Node5 { get; set; }

	public DecoratedClique7? Node7 { get; set; }

	public DecoratedClique8? Node8 { get; set; }

	public DecoratedClique9? Node9 { get; set; }
}

/// <summary>One of ten mutually-referencing types, of which only the last is decorated.</summary>
public sealed class DecoratedClique7
{
	public DecoratedClique0? Node0 { get; set; }

	public DecoratedClique1? Node1 { get; set; }

	public DecoratedClique2? Node2 { get; set; }

	public DecoratedClique3? Node3 { get; set; }

	public DecoratedClique4? Node4 { get; set; }

	public DecoratedClique5? Node5 { get; set; }

	public DecoratedClique6? Node6 { get; set; }

	public DecoratedClique8? Node8 { get; set; }

	public DecoratedClique9? Node9 { get; set; }
}

/// <summary>One of ten mutually-referencing types, of which only the last is decorated.</summary>
public sealed class DecoratedClique8
{
	public DecoratedClique0? Node0 { get; set; }

	public DecoratedClique1? Node1 { get; set; }

	public DecoratedClique2? Node2 { get; set; }

	public DecoratedClique3? Node3 { get; set; }

	public DecoratedClique4? Node4 { get; set; }

	public DecoratedClique5? Node5 { get; set; }

	public DecoratedClique6? Node6 { get; set; }

	public DecoratedClique7? Node7 { get; set; }

	public DecoratedClique9? Node9 { get; set; }
}

/// <summary>The only decorated type of its clique.</summary>
public sealed class DecoratedClique9
{
	public Kind Kind { get; set; }

	[JsonRequiredIfSiblingIs(nameof(Kind), Kind.Advanced)]
	public string? Tuning { get; set; }

	public DecoratedClique0? Node0 { get; set; }

	public DecoratedClique1? Node1 { get; set; }

	public DecoratedClique2? Node2 { get; set; }

	public DecoratedClique3? Node3 { get; set; }

	public DecoratedClique4? Node4 { get; set; }

	public DecoratedClique5? Node5 { get; set; }

	public DecoratedClique6? Node6 { get; set; }

	public DecoratedClique7? Node7 { get; set; }

	public DecoratedClique8? Node8 { get; set; }
}
