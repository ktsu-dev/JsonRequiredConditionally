// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.JsonRequiredConditionally.Tests;

using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

/// <summary>
/// Covers an application that turns off System.Text.Json's default reflection and supplies its own
/// <see cref="DefaultJsonTypeInfoResolver"/>, which this library must keep working under.
/// </summary>
/// <remarks>
/// The switch is process-wide and read once, so it cannot be flipped inside this test host without
/// affecting every other test. The test instead relaunches this assembly, filtered to this class,
/// under a copy of its runtimeconfig with the switch off, and the child process makes the
/// assertions. The parent only checks that the child ran and passed.
/// </remarks>
[TestClass]
public class ReflectionDisabledTests
{
	private const string ChildMarker = "KTSU_JSONREQUIREDCONDITIONALLY_REFLECTION_DISABLED_CHILD";

	private const string ReflectionSwitch = "System.Text.Json.JsonSerializer.IsReflectionEnabledByDefault";

	public TestContext TestContext { get; set; } = null!;

	private static bool IsChild => Environment.GetEnvironmentVariable(ChildMarker) == "1";

	private static JsonSerializerOptions CreateOptions() =>
		new()
		{
			TypeInfoResolver = new DefaultJsonTypeInfoResolver(),
			Converters = { new JsonStringEnumConverter(), new JsonRequiredConditionallyConverterFactory() },
		};

	[TestMethod]
	public void NonStringKeyedDictionaryValuesAreValidatedWithReflectionDisabledByDefault()
	{
		if (!IsChild)
		{
			RunInChildProcess(nameof(NonStringKeyedDictionaryValuesAreValidatedWithReflectionDisabledByDefault));
			return;
		}

		Assert.IsFalse(JsonSerializer.IsReflectionEnabledByDefault, "The child process should run with default reflection disabled.");

		AssertKeyedViolation<int>("1");
		AssertKeyedViolation<Guid>("0F8FAD5B-D9CB-469F-A165-70867728950E");
		AssertKeyedViolation<DateTime>("2024-01-01T00:00:00");
		AssertKeyedViolation<Kind>("Advanced");
	}

	private static void AssertKeyedViolation<TKey>(string key)
		where TKey : notnull
	{
		JsonRequiredConditionallyException exception = Assert.ThrowsExactly<JsonRequiredConditionallyException>(
			() => JsonSerializer.Deserialize<KeyedDictionaryConfig<TKey>>(
				"{\"Map\":{\"" + key + "\":{\"Kind\":\"Advanced\"}}}", CreateOptions()));

		Assert.AreSequenceEqual(new List<string> { $"Map.{key}.Tuning" }, [.. exception.MissingProperties], Microsoft.VisualStudio.TestTools.UnitTesting.SequenceOrder.InAnyOrder);
	}

	private void RunInChildProcess(string testName)
	{
		string assemblyPath = typeof(ReflectionDisabledTests).Assembly.Location;
		string directory = Path.GetDirectoryName(assemblyPath)!;
		string baseName = Path.GetFileNameWithoutExtension(assemblyPath);
		string runtimeConfigPath = Path.Combine(directory, baseName + ".runtimeconfig.json");
		string depsPath = Path.Combine(directory, baseName + ".deps.json");

		JsonNode runtimeConfig = JsonNode.Parse(File.ReadAllText(runtimeConfigPath))!;
		JsonObject runtimeOptions = runtimeConfig["runtimeOptions"]!.AsObject();

		if (runtimeOptions["configProperties"] is not JsonObject configProperties)
		{
			configProperties = [];
			runtimeOptions["configProperties"] = configProperties;
		}

		configProperties[ReflectionSwitch] = false;

		string childConfigPath = Path.Combine(Path.GetTempPath(), $"{baseName}.{Guid.NewGuid():N}.runtimeconfig.json");
		File.WriteAllText(childConfigPath, runtimeConfig.ToJsonString());

		try
		{
			// The shared framework lives at <dotnet root>/shared/Microsoft.NETCore.App/<version>/, so
			// the host that launched this leg is three levels up from it.
			string dotnetRoot = Path.GetFullPath(Path.Combine(RuntimeEnvironment.GetRuntimeDirectory(), "..", "..", ".."));
			string dotnet = Path.Combine(dotnetRoot, RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "dotnet.exe" : "dotnet");

			ProcessStartInfo startInfo = new(dotnet)
			{
				RedirectStandardOutput = true,
				RedirectStandardError = true,
				UseShellExecute = false,
			};

			foreach (string argument in new[]
			{
				"exec", "--runtimeconfig", childConfigPath, "--depsfile", depsPath, assemblyPath,
				"--filter", $"FullyQualifiedName~{nameof(ReflectionDisabledTests)}.{testName}",
			})
			{
				startInfo.ArgumentList.Add(argument);
			}

			startInfo.Environment[ChildMarker] = "1";

			using Process process = Process.Start(startInfo)!;
			Task<string> standardError = process.StandardError.ReadToEndAsync(TestContext.CancellationToken);
			string output = process.StandardOutput.ReadToEnd();
			process.WaitForExit();

			// Exit code 0 means at least one test ran and every one passed; Microsoft.Testing.Platform
			// reports a run in which the filter matched nothing as 8, so this cannot pass vacuously.
			Assert.AreEqual(0, process.ExitCode, $"The child test run failed.{Environment.NewLine}{output}{Environment.NewLine}{standardError.Result}");
		}
		finally
		{
			File.Delete(childConfigPath);
		}
	}
}
