// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.JsonRequiredConditionally.Tests;

using System.Diagnostics;

/// <summary>
/// Guards the eligibility memoization in <c>RequirementRuleCompiler.HasRules</c>. Without it,
/// deciding eligibility over a clique of mutually-referencing types enumerates every simple path:
/// the design spec measured 13 seconds for ten types, and the cost grows factorially beyond that.
/// Every other test would still pass if the memo were lost, so these time the densest graph there
/// is and fail on a slowdown instead of waiting for a consumer's deserialization to hang.
/// </summary>
[TestClass]
public class EligibilityMemoizationTests
{
	/// <summary>
	/// Generous enough for a cold contract model on a slow runner, and far below the factorial cost.
	/// </summary>
	private static readonly TimeSpan Bound = TimeSpan.FromSeconds(1);

	private static readonly Type[] UndecoratedClique =
	[
		typeof(UndecoratedClique0), typeof(UndecoratedClique1), typeof(UndecoratedClique2), typeof(UndecoratedClique3), typeof(UndecoratedClique4),
		typeof(UndecoratedClique5), typeof(UndecoratedClique6), typeof(UndecoratedClique7), typeof(UndecoratedClique8), typeof(UndecoratedClique9),
	];

	private static readonly Type[] DecoratedClique =
	[
		typeof(DecoratedClique0), typeof(DecoratedClique1), typeof(DecoratedClique2), typeof(DecoratedClique3), typeof(DecoratedClique4),
		typeof(DecoratedClique5), typeof(DecoratedClique6), typeof(DecoratedClique7), typeof(DecoratedClique8), typeof(DecoratedClique9),
	];

	private static void AssertClaimedQuickly(Type[] clique, bool expected)
	{
		JsonRequiredConditionallyConverterFactory factory = new();
		Stopwatch stopwatch = Stopwatch.StartNew();

		foreach (Type type in clique)
		{
			Assert.AreEqual(expected, factory.CanConvert(type), type.Name);
		}

		stopwatch.Stop();
		Assert.IsLessThan(Bound, stopwatch.Elapsed);
	}

	[TestMethod]
	public void UndecoratedCliqueIsUnclaimedQuickly() => AssertClaimedQuickly(UndecoratedClique, expected: false);

	[TestMethod]
	public void CliqueReachingOneDecoratedTypeIsClaimedQuickly() => AssertClaimedQuickly(DecoratedClique, expected: true);
}
