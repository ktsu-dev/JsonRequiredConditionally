// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.JsonRequiredConditionally.Tests;

using System.Reflection;
using System.Runtime.Versioning;
using System.Text.Json;

/// <summary>
/// Proves that the multi-targeted test matrix is testing what it claims to.
/// </summary>
/// <remarks>
/// The library's whole design rests on <c>JsonTypeInfo.Properties</c> and
/// <c>JsonPropertyInfo.Get</c>/<c>Set</c>/<c>AttributeProvider</c> behaving identically across the
/// System.Text.Json versions it ships against, and constructor, <c>init</c>-only and <c>required</c>
/// handling did change between them. That is only actually exercised if each leg of the matrix runs
/// on its own shared framework -- a leg compiled for net8.0 but rolled forward onto the .NET 10
/// runtime loads System.Text.Json 10 and tests nothing but compile compatibility.
/// <para>
/// This is not a theoretical risk here: ktsu.Sdk pins <c>RuntimeFrameworkVersion</c> to 10.0.0 for
/// every target framework, so the matrix was silently cosmetic until the test project overrode it.
/// These assertions fail loudly if that override is ever lost.
/// </para>
/// <para>
/// The per-leg assertions above cannot see a leg that is not there. Each compares this leg against
/// itself, so a matrix collapsed to a single <c>net10.0</c> leg satisfies both -- which is exactly
/// how the matrix silently lost <c>net9.0</c>, <c>net8.0</c> and <c>net7.0</c>. The coverage
/// assertions below close that blind spot by comparing build configuration rather than runtime
/// state: the matrix lists are declared once in <c>Directory.Build.props</c>, baked into this
/// assembly as <see cref="AssemblyMetadataAttribute"/>, and checked against each other. That works
/// from a single leg, because it never needs the other runtimes to be installed.
/// </para>
/// </remarks>
[TestClass]
public class RuntimeMatrixTests
{
	/// <summary>
	/// Gets the major version of the target framework this assembly was compiled for.
	/// </summary>
	private static int CompiledMajorVersion
	{
		get
		{
			string? name = AppContext.TargetFrameworkName;

			Assert.IsNotNull(name, "AppContext.TargetFrameworkName is unavailable, so the matrix cannot verify itself.");

			FrameworkName framework = new(name);

			return framework.Version.Major;
		}
	}

	[TestMethod]
	public void TestsRunOnTheSharedFrameworkTheyWereCompiledFor()
	{
		Assert.AreEqual(
			CompiledMajorVersion,
			Environment.Version.Major,
			$"This leg was compiled for .NET {CompiledMajorVersion} but is running on .NET {Environment.Version.Major}, so it is not exercising that framework's in-box System.Text.Json.");
	}

	[TestMethod]
	public void SystemTextJsonComesFromTheSharedFrameworkOfThisLeg()
	{
		Version? version = typeof(JsonSerializer).Assembly.GetName().Version;

		Assert.IsNotNull(version);
		Assert.AreEqual(
			CompiledMajorVersion,
			version.Major,
			$"This leg was compiled for .NET {CompiledMajorVersion} but loaded System.Text.Json {version}, so the matrix is not covering distinct serializer versions.");
	}

	/// <summary>
	/// Reads one of the matrix lists baked in from <c>Directory.Build.props</c>.
	/// </summary>
	/// <param name="key">The <see cref="AssemblyMetadataAttribute"/> key to read.</param>
	/// <returns>The target frameworks the list names, in declaration order.</returns>
	private static List<string> MatrixList(string key)
	{
		AssemblyMetadataAttribute[] matching = [.. typeof(RuntimeMatrixTests).Assembly
			.GetCustomAttributes<AssemblyMetadataAttribute>()
			.Where(attribute => attribute.Key == key)];

		Assert.HasCount(
			1,
			matching,
			$"Expected exactly one AssemblyMetadata entry for '{key}', found {matching.Length}. The test project must forward every matrix list from Directory.Build.props, or this guard cannot see the matrix it is checking.");

		return [.. (matching[0].Value ?? string.Empty)
			.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)];
	}

	[TestMethod]
	public void EveryRunnableLibraryTargetFrameworkIsCoveredOrRecordedAsUncovered()
	{
		List<string> runnable = MatrixList("LibraryRunnableTargetFrameworks");
		List<string> covered = MatrixList("TestTargetFrameworks");
		List<string> uncovered = MatrixList("UncoveredRunnableTargetFrameworks");

		Assert.IsNotEmpty(runnable, "LibraryRunnableTargetFrameworks is empty, so there is no matrix to check.");

		List<string> unaccountedFor = [.. runnable.Where(framework => !covered.Contains(framework) && !uncovered.Contains(framework))];

		Assert.IsEmpty(
			unaccountedFor,
			$"The library ships [{string.Join(", ", runnable)}] but [{string.Join(", ", unaccountedFor)}] has no test leg and is not recorded in UncoveredRunnableTargetFrameworks. Either add the leg to TestTargetFrameworks or record the gap, with its reason, in Directory.Build.props.");
	}

	[TestMethod]
	public void NoTargetFrameworkIsBothCoveredAndRecordedAsUncovered()
	{
		List<string> covered = MatrixList("TestTargetFrameworks");
		List<string> uncovered = MatrixList("UncoveredRunnableTargetFrameworks");

		List<string> contradictory = [.. covered.Where(uncovered.Contains)];

		Assert.IsEmpty(
			contradictory,
			$"[{string.Join(", ", contradictory)}] is listed as both a test leg and a recorded gap, so the two lists disagree about what the matrix covers.");
	}

	[TestMethod]
	public void EveryRecordedGapIsStillARunnableLibraryTargetFramework()
	{
		List<string> runnable = MatrixList("LibraryRunnableTargetFrameworks");
		List<string> uncovered = MatrixList("UncoveredRunnableTargetFrameworks");

		List<string> stale = [.. uncovered.Where(framework => !runnable.Contains(framework))];

		Assert.IsEmpty(
			stale,
			$"[{string.Join(", ", stale)}] is recorded as an uncovered gap but the library no longer targets it, so the exemption outlived the target it excused and should be deleted.");
	}

	[TestMethod]
	public void EveryTestLegTargetsAFrameworkTheLibraryShips()
	{
		List<string> runnable = MatrixList("LibraryRunnableTargetFrameworks");
		List<string> covered = MatrixList("TestTargetFrameworks");

		List<string> orphaned = [.. covered.Where(framework => !runnable.Contains(framework))];

		Assert.IsEmpty(
			orphaned,
			$"[{string.Join(", ", orphaned)}] is a test leg for a framework the library does not ship, so it is testing a configuration no consumer can be in.");
	}

	[TestMethod]
	public void ThisLegIsOneOfTheDeclaredTestLegs()
	{
		List<string> covered = MatrixList("TestTargetFrameworks");
		string thisLeg = $"net{CompiledMajorVersion}.0";

		Assert.Contains(
			thisLeg, covered,
			$"This leg reports itself as {thisLeg}, which is not in TestTargetFrameworks [{string.Join(", ", covered)}]. The declared matrix and the legs actually being built have diverged.");
	}

	[TestMethod]
	public void PackageBoundTargetFrameworksAreNotClaimedAsTestLegs()
	{
		List<string> packageBound = MatrixList("LibraryPackageBoundTargetFrameworks");
		List<string> covered = MatrixList("TestTargetFrameworks");

		Assert.IsNotEmpty(packageBound, "LibraryPackageBoundTargetFrameworks is empty, but the library ships netstandard assets.");

		List<string> impossible = [.. covered.Where(packageBound.Contains)];

		Assert.IsEmpty(
			impossible,
			$"[{string.Join(", ", impossible)}] is listed as a test leg, but a netstandard target has no shared framework to run on. Exercising those assets needs a net472 leg, tracked in ktsu-dev/JsonRequiredConditionally#14.");
	}
}
