// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace ktsu.JsonRequiredConditionally.Tests;

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
}
