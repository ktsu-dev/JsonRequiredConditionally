# ktsu.JsonRequiredConditionally Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Ship `ktsu.JsonRequiredConditionally`, a NuGet library providing `[JsonRequiredIfSiblingIs(nameof(Sibling), value)]` enforced during `System.Text.Json` deserialization.

**Architecture:** A `JsonConverterFactory` claims only types carrying the attribute. Its converter copies the `Utf8JsonReader` (a struct) to scan which property names are physically present, materializes the object through a cached clone of the options with itself excluded for that one type, then evaluates compiled rules against the instance and throws a single aggregated exception listing every missing property.

**Tech Stack:** C# on ktsu.Sdk, `System.Text.Json`, `Polyfill` (for `Ensure.NotNull`), MSTest.Sdk via Microsoft.Testing.Platform.

**Spec:** `docs/superpowers/specs/2026-08-06-json-required-conditionally-design.md`

## Global Constraints

- Repository root: `C:\dev\ktsu-dev\JsonRequiredConditionally`. It already exists as a git repo on branch `main` containing only the spec and this plan.
- Library target frameworks, verbatim: `net10.0;net9.0;net8.0;net7.0;net6.0;net5.0;netstandard2.0;netstandard2.1`
- Test project targets `net10.0` only.
- `System.Text.Json` `PackageReference` is conditional on `netstandard2.0;netstandard2.1` only — it is in-box from `net5.0` up.
- Library namespace: `ktsu.JsonRequiredConditionally`. Test namespace: `ktsu.JsonRequiredConditionally.Tests`.
- Tabs for indentation. CRLF line endings. File-scoped namespaces. **Using directives go INSIDE the namespace** (see `C:\dev\ktsu-dev\Extensions\Extensions\ReflectionExtensions.cs`).
- Every `.cs` file starts with exactly:
  ```csharp
  // Copyright (c) ktsu.dev
  // All rights reserved.
  // Licensed under the MIT license.
  ```
- Braces on all control flow. Explicit accessibility modifiers. No `this.` qualifier. Nullable reference types enabled. Warnings as errors.
- Use `Ensure.NotNull(x)` from Polyfill for public-entrypoint parameter validation.
- No global warning suppressions. Targeted `[SuppressMessage]` with a justification only where unavoidable.
- Tests use semantic asserts (`Assert.ThrowsExactly`, `Assert.HasCount`, `Assert.IsEmpty`, `CollectionAssert.AreEqual`) — never `Assert.IsTrue`/`IsFalse` on a comparison.
- Do NOT hand-write `VERSION.md`, `CHANGELOG.md`, or `LICENSE.md`. The ktsu CI pipeline generates them.
- Commit messages use ktsu version tags. Use `[minor]` for the commit that first introduces the public API, `[patch]` for everything after.

## File Structure

| File | Responsibility |
|---|---|
| `JsonRequiredConditionally/JsonRequiredIfSiblingIsAttribute.cs` | Public attribute. Data only. |
| `JsonRequiredConditionally/JsonRequiredConditionallyException.cs` | Public exception carrying `MissingProperties`. |
| `JsonRequiredConditionally/ValueMatcher.cs` | Internal. Enum-normalizing equality. |
| `JsonRequiredConditionally/RequirementRule.cs` | Internal. `SiblingCondition` (OR within) + `RequirementRule` (AND across). |
| `JsonRequiredConditionally/RequirementRuleCompiler.cs` | Internal. Reflection → rules; type eligibility test. |
| `JsonRequiredConditionally/PresenceScanner.cs` | Internal. Reader copy → set of present property names. |
| `JsonRequiredConditionally/InnerOptionsCache.cs` | Internal. Cached per-`(root options, excluded type)` options clones. |
| `JsonRequiredConditionally/JsonRequiredConditionallyConverter.cs` | Internal generic converter. Orchestrates scan → materialize → validate. |
| `JsonRequiredConditionally/JsonRequiredConditionallyConverterFactory.cs` | Public factory + internal `ExcludingFactory`. |
| `JsonRequiredConditionally/AssemblyInfo.cs` | `InternalsVisibleTo` for the test assembly. |
| `JsonRequiredConditionally.Test/TestModels.cs` | Shared model types for all test classes. |

---

### Task 1: Repository scaffolding and the attribute

**Files:**
- Create: `global.json`, `Directory.Packages.props`, `JsonRequiredConditionally.sln`
- Create: `JsonRequiredConditionally/JsonRequiredConditionally.csproj`
- Create: `JsonRequiredConditionally/JsonRequiredIfSiblingIsAttribute.cs`
- Create: `JsonRequiredConditionally/AssemblyInfo.cs`
- Create: `JsonRequiredConditionally.Test/JsonRequiredConditionally.Test.csproj`
- Create: `JsonRequiredConditionally.Test/AttributeTests.cs`
- Create: metadata `AUTHORS.md`, `AUTHORS.url`, `PROJECT_URL.url`, `COPYRIGHT.md`, `DESCRIPTION.md`, `TAGS.md`
- Copy from `C:\dev\ktsu-dev\Extensions`: `.editorconfig`, `.gitattributes`, `.gitignore`, `.runsettings`, `.github/`

**Interfaces:**
- Consumes: nothing.
- Produces: `ktsu.JsonRequiredConditionally.JsonRequiredIfSiblingIsAttribute` with `string SiblingName { get; }` and `object? Value { get; }`; constructor `(string siblingName, object? value)`.

- [ ] **Step 1: Copy the shared dotfiles and CI workflow from the Extensions repo**

```powershell
$src = 'C:\dev\ktsu-dev\Extensions'
$dst = 'C:\dev\ktsu-dev\JsonRequiredConditionally'
foreach ($f in @('.editorconfig', '.gitattributes', '.gitignore', '.runsettings')) {
	Copy-Item -Path (Join-Path $src $f) -Destination (Join-Path $dst $f) -Force
}
Copy-Item -Path (Join-Path $src '.github') -Destination $dst -Recurse -Force
```

- [ ] **Step 2: Write the metadata files**

`AUTHORS.md`:
```
ktsu.dev contributors
```

`COPYRIGHT.md`:
```
Copyright (c) 2026 ktsu-dev contributors
```

`DESCRIPTION.md`:
```
A System.Text.Json attribute that makes a property required only when a sibling property has a given value, enforced during deserialization.
```

`TAGS.md`:
```
json;system.text.json;serialization;deserialization;validation;attribute;required;conditional;converter;dotnet;csharp
```

`AUTHORS.url`:
```
[InternetShortcut]
URL=https://github.com/ktsu-dev
```

`PROJECT_URL.url`:
```
[InternetShortcut]
URL=https://github.com/ktsu-dev/JsonRequiredConditionally
```

- [ ] **Step 3: Write `global.json`**

Copy the exact SDK pin from Extensions so both repos move together:

```json
{
  "sdk": {
    "version": "10.0.100",
    "rollForward": "latestFeature"
  },
  "msbuild-sdks": {
    "MSTest.Sdk": "4.3.3",
    "ktsu.Sdk": "2.15.1"
  },
  "test": {
    "runner": "Microsoft.Testing.Platform"
  }
}
```

- [ ] **Step 4: Determine the current System.Text.Json version and write `Directory.Packages.props`**

Run this to get the latest stable version:

```powershell
dotnet package search System.Text.Json --exact-match --take 1 --format json
```

Use that version below in place of `9.0.0` if it is newer. `9.0.0` is a known-good floor — do not go below it, since the `JsonSerializerOptions` copy constructor and `PropertyNameCaseInsensitive` behavior this library depends on must be present.

```xml
<Project>
  <PropertyGroup>
    <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
  </PropertyGroup>
  <ItemGroup>
    <PackageVersion Include="Polyfill" Version="11.0.1" />
    <PackageVersion Include="System.Text.Json" Version="9.0.0" />
  </ItemGroup>
</Project>
```

- [ ] **Step 5: Write the library csproj**

`JsonRequiredConditionally/JsonRequiredConditionally.csproj`:

```xml
<Project>
  <Sdk Name="Microsoft.NET.Sdk" />
  <Sdk Name="ktsu.Sdk" />

  <PropertyGroup>
    <TargetFrameworks>net10.0;net9.0;net8.0;net7.0;net6.0;net5.0;netstandard2.0;netstandard2.1;</TargetFrameworks>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Polyfill" />
  </ItemGroup>

  <ItemGroup Condition="'$(TargetFramework)' == 'netstandard2.0' Or '$(TargetFramework)' == 'netstandard2.1'">
    <PackageReference Include="System.Text.Json" />
  </ItemGroup>
</Project>
```

- [ ] **Step 6: Write the test csproj**

`JsonRequiredConditionally.Test/JsonRequiredConditionally.Test.csproj`:

```xml
<Project>
  <Sdk Name="MSTest.Sdk" />
  <Sdk Name="ktsu.Sdk" />

  <PropertyGroup>
    <IsTestProject>true</IsTestProject>
    <TargetFramework>net10.0</TargetFramework>
    <TargetFrameworks></TargetFrameworks>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="..\JsonRequiredConditionally\JsonRequiredConditionally.csproj" />
  </ItemGroup>
</Project>
```

- [ ] **Step 7: Create the solution and add both projects**

```powershell
cd C:\dev\ktsu-dev\JsonRequiredConditionally
dotnet new sln --name JsonRequiredConditionally
dotnet sln add JsonRequiredConditionally\JsonRequiredConditionally.csproj
dotnet sln add JsonRequiredConditionally.Test\JsonRequiredConditionally.Test.csproj
```

- [ ] **Step 8: Write the failing test**

`JsonRequiredConditionally.Test/AttributeTests.cs`:

```csharp
// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace ktsu.JsonRequiredConditionally.Tests;

using System.Reflection;

[TestClass]
public class AttributeTests
{
	public enum Kind
	{
		Basic = 0,
		Advanced = 1,
	}

	public sealed class Target
	{
		public Kind Kind { get; set; }

		[JsonRequiredIfSiblingIs(nameof(Kind), Kind.Advanced)]
		public string? Tuning { get; set; }
	}

	[TestMethod]
	public void AttributeStoresSiblingNameAndEnumValue()
	{
		PropertyInfo property = typeof(Target).GetProperty(nameof(Target.Tuning))!;

		JsonRequiredIfSiblingIsAttribute attribute = property
			.GetCustomAttributes<JsonRequiredIfSiblingIsAttribute>()
			.Single();

		Assert.AreEqual("Kind", attribute.SiblingName);
		Assert.AreEqual(Kind.Advanced, attribute.Value);
	}

	[TestMethod]
	public void AttributeAllowsMultipleOnOneMember()
	{
		PropertyInfo property = typeof(MultiTarget).GetProperty(nameof(MultiTarget.Tuning))!;

		JsonRequiredIfSiblingIsAttribute[] attributes = [.. property.GetCustomAttributes<JsonRequiredIfSiblingIsAttribute>()];

		Assert.HasCount(2, attributes);
	}

	[TestMethod]
	public void AttributeAcceptsNullValue()
	{
		JsonRequiredIfSiblingIsAttribute attribute = new("Sibling", null);

		Assert.IsNull(attribute.Value);
	}

	[TestMethod]
	public void AttributeRejectsNullSiblingName()
	{
		Assert.ThrowsExactly<ArgumentNullException>(() => new JsonRequiredIfSiblingIsAttribute(null!, 1));
	}

	public sealed class MultiTarget
	{
		public Kind Kind { get; set; }

		[JsonRequiredIfSiblingIs(nameof(Kind), Kind.Basic)]
		[JsonRequiredIfSiblingIs(nameof(Kind), Kind.Advanced)]
		public string? Tuning { get; set; }
	}
}
```

- [ ] **Step 9: Run the test to verify it fails**

Run: `dotnet test`
Expected: FAIL — compilation error CS0246, `JsonRequiredIfSiblingIsAttribute` could not be found.

- [ ] **Step 10: Write the attribute**

`JsonRequiredConditionally/JsonRequiredIfSiblingIsAttribute.cs`:

```csharp
// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace ktsu.JsonRequiredConditionally;

/// <summary>
/// Marks a property or field as required during JSON deserialization only when a sibling
/// member of the same type has a particular value.
/// </summary>
/// <remarks>
/// Multiple attributes on one member group implicitly by <see cref="SiblingName"/>. Values within
/// a group are combined with OR; the groups themselves are combined with AND. The member is
/// considered satisfied when it is physically present in the payload, even if its value is null.
/// </remarks>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = true, Inherited = true)]
public sealed class JsonRequiredIfSiblingIsAttribute : Attribute
{
	/// <summary>
	/// Initializes a new instance of the <see cref="JsonRequiredIfSiblingIsAttribute"/> class.
	/// </summary>
	/// <param name="siblingName">The CLR name of the sibling member to inspect. Use <c>nameof</c>.</param>
	/// <param name="value">The value the sibling must have for this member to be required.</param>
	public JsonRequiredIfSiblingIsAttribute(string siblingName, object? value)
	{
		Ensure.NotNull(siblingName);

		SiblingName = siblingName;
		Value = value;
	}

	/// <summary>
	/// Gets the CLR name of the sibling member to inspect.
	/// </summary>
	public string SiblingName { get; }

	/// <summary>
	/// Gets the value the sibling must have for the decorated member to be required.
	/// </summary>
	public object? Value { get; }
}
```

- [ ] **Step 11: Write `AssemblyInfo.cs`**

`JsonRequiredConditionally/AssemblyInfo.cs`:

```csharp
// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("ktsu.JsonRequiredConditionally.Test")]
```

- [ ] **Step 12: Verify the test assembly name matches**

Run: `dotnet build JsonRequiredConditionally.Test\JsonRequiredConditionally.Test.csproj -getProperty:AssemblyName`

Expected: `ktsu.JsonRequiredConditionally.Test`. If it differs, correct the string in `AssemblyInfo.cs` to the reported name — later tasks test internal types and will not compile otherwise.

- [ ] **Step 13: Run the tests to verify they pass**

Run: `dotnet test`
Expected: PASS, 4 tests.

- [ ] **Step 14: Commit**

```bash
git add -A
git commit -m "[minor] Add JsonRequiredIfSiblingIs attribute and repo scaffolding"
```

---

### Task 2: Enum-normalizing value comparison

**Files:**
- Create: `JsonRequiredConditionally/ValueMatcher.cs`
- Create: `JsonRequiredConditionally.Test/ValueMatcherTests.cs`

**Interfaces:**
- Consumes: nothing.
- Produces: `internal static class ValueMatcher` with `internal static bool Matches(object? actual, object? expected)`.

Why this exists: an attribute argument of `2` boxes an `int`, and `((object)2).Equals((object)Kind.Advanced)` is `false` even though `Kind.Advanced == 2`. Without normalization the rule silently never fires.

- [ ] **Step 1: Write the failing test**

`JsonRequiredConditionally.Test/ValueMatcherTests.cs`:

```csharp
// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace ktsu.JsonRequiredConditionally.Tests;

[TestClass]
public class ValueMatcherTests
{
	public enum Kind
	{
		Basic = 0,
		Advanced = 1,
		Expert = 2,
	}

	public enum Other
	{
		Advanced = 1,
	}

	[TestMethod]
	public void MatchesIdenticalEnumValues()
	{
		Assert.IsTrue(ValueMatcher.Matches(Kind.Advanced, Kind.Advanced));
	}

	[TestMethod]
	public void DoesNotMatchDifferentEnumValues()
	{
		Assert.IsFalse(ValueMatcher.Matches(Kind.Basic, Kind.Advanced));
	}

	[TestMethod]
	public void MatchesBoxedIntegerAgainstEnum()
	{
		Assert.IsTrue(ValueMatcher.Matches(Kind.Expert, 2));
		Assert.IsTrue(ValueMatcher.Matches(2, Kind.Expert));
	}

	[TestMethod]
	public void MatchesEnumsOfDifferentTypesWithEqualUnderlyingValues()
	{
		Assert.IsTrue(ValueMatcher.Matches(Kind.Advanced, Other.Advanced));
	}

	[TestMethod]
	public void MatchesNullToNull()
	{
		Assert.IsTrue(ValueMatcher.Matches(null, null));
	}

	[TestMethod]
	public void DoesNotMatchNullToValue()
	{
		Assert.IsFalse(ValueMatcher.Matches(null, Kind.Advanced));
		Assert.IsFalse(ValueMatcher.Matches(Kind.Advanced, null));
	}

	[TestMethod]
	public void MatchesEqualStringsOrdinally()
	{
		Assert.IsTrue(ValueMatcher.Matches("Advanced", "Advanced"));
		Assert.IsFalse(ValueMatcher.Matches("advanced", "Advanced"));
	}

	[TestMethod]
	public void MatchesEqualPrimitives()
	{
		Assert.IsTrue(ValueMatcher.Matches(42, 42));
		Assert.IsTrue(ValueMatcher.Matches(true, true));
		Assert.IsFalse(ValueMatcher.Matches(42, 43));
	}

	[TestMethod]
	public void DoesNotMatchUnconvertibleValueAgainstEnum()
	{
		Assert.IsFalse(ValueMatcher.Matches(Kind.Advanced, "Advanced"));
	}
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test --filter "FullyQualifiedName~ValueMatcherTests"`
Expected: FAIL — compilation error CS0103, `ValueMatcher` does not exist.

- [ ] **Step 3: Write the implementation**

`JsonRequiredConditionally/ValueMatcher.cs`:

```csharp
// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace ktsu.JsonRequiredConditionally;

using System.Globalization;

/// <summary>
/// Compares a sibling's runtime value against the constant supplied to an attribute.
/// </summary>
internal static class ValueMatcher
{
	/// <summary>
	/// Determines whether a sibling's actual value equals the expected attribute value.
	/// </summary>
	/// <param name="actual">The value read from the materialized instance.</param>
	/// <param name="expected">The constant supplied to the attribute.</param>
	/// <returns>True when the values are considered equal.</returns>
	internal static bool Matches(object? actual, object? expected)
	{
		if (actual is null || expected is null)
		{
			return actual is null && expected is null;
		}

		Type actualType = actual.GetType();
		Type expectedType = expected.GetType();

		if (actualType.IsEnum || expectedType.IsEnum)
		{
			return MatchesAsEnum(actual, expected, actualType.IsEnum ? actualType : expectedType);
		}

		if (actual is string actualText && expected is string expectedText)
		{
			return string.Equals(actualText, expectedText, StringComparison.Ordinal);
		}

		return actual.Equals(expected);
	}

	private static bool MatchesAsEnum(object actual, object expected, Type enumType)
	{
		Type underlying = Enum.GetUnderlyingType(enumType);

		try
		{
			object actualValue = Convert.ChangeType(actual, underlying, CultureInfo.InvariantCulture);
			object expectedValue = Convert.ChangeType(expected, underlying, CultureInfo.InvariantCulture);

			return actualValue.Equals(expectedValue);
		}
		catch (InvalidCastException)
		{
			return false;
		}
		catch (FormatException)
		{
			return false;
		}
		catch (OverflowException)
		{
			return false;
		}
	}
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test --filter "FullyQualifiedName~ValueMatcherTests"`
Expected: PASS, 9 tests.

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "[patch] Add enum-normalizing value comparison"
```

---

### Task 3: The exception type

**Files:**
- Create: `JsonRequiredConditionally/JsonRequiredConditionallyException.cs`
- Create: `JsonRequiredConditionally.Test/ExceptionTests.cs`

**Interfaces:**
- Consumes: nothing.
- Produces: `public sealed class JsonRequiredConditionallyException : JsonException` with `public IReadOnlyList<string> MissingProperties { get; }`, plus constructors `()`, `(string message)`, `(string message, Exception innerException)`, and `(IReadOnlyList<string> missingProperties)`.

Note: do not add `[Serializable]` or a binary-serialization constructor. Those trigger `SYSLIB0051` on `net8.0` and above, which is an error under warnings-as-errors.

- [ ] **Step 1: Write the failing test**

`JsonRequiredConditionally.Test/ExceptionTests.cs`:

```csharp
// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace ktsu.JsonRequiredConditionally.Tests;

using System.Text.Json;

[TestClass]
public class ExceptionTests
{
	[TestMethod]
	public void ExceptionDerivesFromJsonException()
	{
		JsonRequiredConditionallyException exception = new(["tuning"]);

		Assert.IsInstanceOfType<JsonException>(exception);
	}

	[TestMethod]
	public void ExceptionExposesMissingProperties()
	{
		JsonRequiredConditionallyException exception = new(["tuning", "host"]);

		CollectionAssert.AreEqual(new List<string> { "tuning", "host" }, exception.MissingProperties.ToList());
	}

	[TestMethod]
	public void ExceptionMessageNamesEveryMissingProperty()
	{
		JsonRequiredConditionallyException exception = new(["tuning", "host"]);

		StringAssert.Contains(exception.Message, "tuning");
		StringAssert.Contains(exception.Message, "host");
	}

	[TestMethod]
	public void DefaultConstructorProducesEmptyMissingProperties()
	{
		JsonRequiredConditionallyException exception = new();

		Assert.IsEmpty(exception.MissingProperties);
	}

	[TestMethod]
	public void MessageConstructorPreservesMessage()
	{
		JsonRequiredConditionallyException exception = new("custom message");

		Assert.AreEqual("custom message", exception.Message);
		Assert.IsEmpty(exception.MissingProperties);
	}

	[TestMethod]
	public void InnerExceptionConstructorPreservesBoth()
	{
		InvalidOperationException inner = new("inner");
		JsonRequiredConditionallyException exception = new("outer", inner);

		Assert.AreEqual("outer", exception.Message);
		Assert.AreSame(inner, exception.InnerException);
	}
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test --filter "FullyQualifiedName~ExceptionTests"`
Expected: FAIL — compilation error CS0246, `JsonRequiredConditionallyException` could not be found.

- [ ] **Step 3: Write the implementation**

`JsonRequiredConditionally/JsonRequiredConditionallyException.cs`:

```csharp
// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace ktsu.JsonRequiredConditionally;

using System.Text.Json;

/// <summary>
/// Thrown when one or more properties were required by their sibling values but were absent
/// from the JSON payload.
/// </summary>
public sealed class JsonRequiredConditionallyException : JsonException
{
	/// <summary>
	/// Initializes a new instance of the <see cref="JsonRequiredConditionallyException"/> class.
	/// </summary>
	public JsonRequiredConditionallyException()
		: base() => MissingProperties = [];

	/// <summary>
	/// Initializes a new instance of the <see cref="JsonRequiredConditionallyException"/> class.
	/// </summary>
	/// <param name="message">The message that describes the error.</param>
	public JsonRequiredConditionallyException(string message)
		: base(message) => MissingProperties = [];

	/// <summary>
	/// Initializes a new instance of the <see cref="JsonRequiredConditionallyException"/> class.
	/// </summary>
	/// <param name="message">The message that describes the error.</param>
	/// <param name="innerException">The exception that caused this exception.</param>
	public JsonRequiredConditionallyException(string message, Exception innerException)
		: base(message, innerException) => MissingProperties = [];

	/// <summary>
	/// Initializes a new instance of the <see cref="JsonRequiredConditionallyException"/> class.
	/// </summary>
	/// <param name="missingProperties">The JSON names of the properties that were required but absent.</param>
	public JsonRequiredConditionallyException(IReadOnlyList<string> missingProperties)
		: base(BuildMessage(missingProperties)) => MissingProperties = missingProperties;

	/// <summary>
	/// Gets the JSON names of the properties that were required but absent from the payload.
	/// </summary>
	public IReadOnlyList<string> MissingProperties { get; }

	private static string BuildMessage(IReadOnlyList<string> missingProperties)
	{
		Ensure.NotNull(missingProperties);

		string names = string.Join("', '", missingProperties);

		return $"The following properties are required by their sibling values but were absent from the JSON payload: '{names}'.";
	}
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test --filter "FullyQualifiedName~ExceptionTests"`
Expected: PASS, 6 tests.

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "[patch] Add JsonRequiredConditionallyException"
```

---

### Task 4: Rule compilation

**Files:**
- Create: `JsonRequiredConditionally/RequirementRule.cs`
- Create: `JsonRequiredConditionally/RequirementRuleCompiler.cs`
- Create: `JsonRequiredConditionally.Test/TestModels.cs`
- Create: `JsonRequiredConditionally.Test/RequirementRuleCompilerTests.cs`

**Interfaces:**
- Consumes: `JsonRequiredIfSiblingIsAttribute` (Task 1), `ValueMatcher.Matches` (Task 2).
- Produces:
  - `internal sealed class SiblingCondition` — constructor `(string siblingName, Func<object, object?> accessor, object?[] acceptedValues)`, method `internal bool IsSatisfiedBy(object instance)`.
  - `internal sealed class RequirementRule` — constructor `(string jsonName, string memberName, SiblingCondition[] conditions)`, properties `JsonName`, `MemberName`, `Conditions`, method `internal bool IsRequiredFor(object instance)`.
  - `internal static class RequirementRuleCompiler` — `internal static bool HasRules(Type type)`, `internal static RequirementRule[] Compile(Type type, JsonSerializerOptions options)`.

Grouping semantics: attributes on one member are grouped by `SiblingName` using `StringComparer.Ordinal`. Each group becomes one `SiblingCondition` whose `AcceptedValues` are OR-ed. `RequirementRule.IsRequiredFor` AND-s the conditions.

- [ ] **Step 1: Write the shared test models**

`JsonRequiredConditionally.Test/TestModels.cs`:

```csharp
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
```

- [ ] **Step 2: Write the failing test**

`JsonRequiredConditionally.Test/RequirementRuleCompilerTests.cs`:

```csharp
// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace ktsu.JsonRequiredConditionally.Tests;

using System.Text.Json;

[TestClass]
public class RequirementRuleCompilerTests
{
	[TestMethod]
	public void HasRulesDetectsDecoratedMembers()
	{
		Assert.IsTrue(RequirementRuleCompiler.HasRules(typeof(SimpleConfig)));
	}

	[TestMethod]
	public void HasRulesRejectsUndecoratedTypes()
	{
		Assert.IsFalse(RequirementRuleCompiler.HasRules(typeof(PlainConfig)));
	}

	[TestMethod]
	public void HasRulesRejectsPrimitivesStringsAndEnums()
	{
		Assert.IsFalse(RequirementRuleCompiler.HasRules(typeof(int)));
		Assert.IsFalse(RequirementRuleCompiler.HasRules(typeof(string)));
		Assert.IsFalse(RequirementRuleCompiler.HasRules(typeof(Kind)));
		Assert.IsFalse(RequirementRuleCompiler.HasRules(typeof(List<SimpleConfig>)));
	}

	[TestMethod]
	public void CompileUsesClrNameWhenNoNamingPolicy()
	{
		RequirementRule[] rules = RequirementRuleCompiler.Compile(typeof(SimpleConfig), new JsonSerializerOptions());

		Assert.HasCount(1, rules);
		Assert.AreEqual("Tuning", rules[0].JsonName);
		Assert.AreEqual("Tuning", rules[0].MemberName);
	}

	[TestMethod]
	public void CompileAppliesNamingPolicy()
	{
		JsonSerializerOptions options = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

		RequirementRule[] rules = RequirementRuleCompiler.Compile(typeof(SimpleConfig), options);

		Assert.AreEqual("tuning", rules[0].JsonName);
	}

	[TestMethod]
	public void CompilePrefersJsonPropertyNameOverNamingPolicy()
	{
		JsonSerializerOptions options = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

		RequirementRule[] rules = RequirementRuleCompiler.Compile(typeof(RenamedConfig), options);

		Assert.AreEqual("tuning_value", rules[0].JsonName);
	}

	[TestMethod]
	public void SameSiblingValuesCollapseToOneOredCondition()
	{
		RequirementRule[] rules = RequirementRuleCompiler.Compile(typeof(OrConfig), new JsonSerializerOptions());

		Assert.HasCount(1, rules[0].Conditions);
		Assert.IsTrue(rules[0].IsRequiredFor(new OrConfig { Kind = Kind.Advanced }));
		Assert.IsTrue(rules[0].IsRequiredFor(new OrConfig { Kind = Kind.Expert }));
		Assert.IsFalse(rules[0].IsRequiredFor(new OrConfig { Kind = Kind.Basic }));
	}

	[TestMethod]
	public void DifferentSiblingsProduceAndedConditions()
	{
		RequirementRule[] rules = RequirementRuleCompiler.Compile(typeof(AndConfig), new JsonSerializerOptions());

		Assert.HasCount(2, rules[0].Conditions);
		Assert.IsTrue(rules[0].IsRequiredFor(new AndConfig { Kind = Kind.Advanced, Mode = Mode.Remote }));
		Assert.IsFalse(rules[0].IsRequiredFor(new AndConfig { Kind = Kind.Advanced, Mode = Mode.Local }));
		Assert.IsFalse(rules[0].IsRequiredFor(new AndConfig { Kind = Kind.Basic, Mode = Mode.Remote }));
	}

	[TestMethod]
	public void UnresolvableSiblingNameThrowsInvalidOperationException()
	{
		InvalidOperationException exception = Assert.ThrowsExactly<InvalidOperationException>(
			() => RequirementRuleCompiler.Compile(typeof(BrokenConfig), new JsonSerializerOptions()));

		StringAssert.Contains(exception.Message, "NoSuchMember");
		StringAssert.Contains(exception.Message, nameof(BrokenConfig));
	}
}
```

- [ ] **Step 3: Run the test to verify it fails**

Run: `dotnet test --filter "FullyQualifiedName~RequirementRuleCompilerTests"`
Expected: FAIL — compilation error CS0103, `RequirementRuleCompiler` does not exist.

- [ ] **Step 4: Write the rule types**

`JsonRequiredConditionally/RequirementRule.cs`:

```csharp
// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace ktsu.JsonRequiredConditionally;

/// <summary>
/// One sibling and the set of values that make the decorated member required. Values are OR-ed.
/// </summary>
internal sealed class SiblingCondition(string siblingName, Func<object, object?> accessor, object?[] acceptedValues)
{
	/// <summary>
	/// Gets the CLR name of the sibling this condition inspects.
	/// </summary>
	internal string SiblingName { get; } = siblingName;

	/// <summary>
	/// Gets the values that satisfy this condition.
	/// </summary>
	internal object?[] AcceptedValues { get; } = acceptedValues;

	/// <summary>
	/// Determines whether the instance's sibling value matches any accepted value.
	/// </summary>
	/// <param name="instance">The materialized object.</param>
	/// <returns>True when any accepted value matches.</returns>
	internal bool IsSatisfiedBy(object instance)
	{
		object? actual = accessor(instance);

		foreach (object? expected in AcceptedValues)
		{
			if (ValueMatcher.Matches(actual, expected))
			{
				return true;
			}
		}

		return false;
	}
}

/// <summary>
/// One decorated member and the conditions that make it required. Conditions are AND-ed.
/// </summary>
internal sealed class RequirementRule(string jsonName, string memberName, SiblingCondition[] conditions)
{
	/// <summary>
	/// Gets the name this member carries in the JSON payload.
	/// </summary>
	internal string JsonName { get; } = jsonName;

	/// <summary>
	/// Gets the CLR name of the decorated member.
	/// </summary>
	internal string MemberName { get; } = memberName;

	/// <summary>
	/// Gets the conditions that must all hold for the member to be required.
	/// </summary>
	internal SiblingCondition[] Conditions { get; } = conditions;

	/// <summary>
	/// Determines whether the member is required for the given instance.
	/// </summary>
	/// <param name="instance">The materialized object.</param>
	/// <returns>True when every condition is satisfied.</returns>
	internal bool IsRequiredFor(object instance)
	{
		foreach (SiblingCondition condition in Conditions)
		{
			if (!condition.IsSatisfiedBy(instance))
			{
				return false;
			}
		}

		return true;
	}
}
```

- [ ] **Step 5: Write the compiler**

`JsonRequiredConditionally/RequirementRuleCompiler.cs`:

```csharp
// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace ktsu.JsonRequiredConditionally;

using System.Collections;
using System.Collections.Concurrent;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

/// <summary>
/// Builds the requirement rules for a type by reflecting over its decorated members.
/// </summary>
internal static class RequirementRuleCompiler
{
	private const BindingFlags MemberFlags = BindingFlags.Public | BindingFlags.Instance;
	private const BindingFlags SiblingFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

	private static readonly ConcurrentDictionary<Type, bool> EligibilityCache = new();

	/// <summary>
	/// Determines whether a type carries at least one decorated member and is shaped like an object.
	/// </summary>
	/// <param name="type">The candidate type.</param>
	/// <returns>True when the type should be routed through the converter.</returns>
	internal static bool HasRules(Type type) => EligibilityCache.GetOrAdd(type, IsEligible);

	/// <summary>
	/// Compiles the requirement rules for a type against a specific set of serializer options.
	/// </summary>
	/// <param name="type">The type to compile rules for.</param>
	/// <param name="options">The options whose naming policy resolves JSON names.</param>
	/// <returns>One rule per decorated member.</returns>
	/// <exception cref="InvalidOperationException">A sibling name does not resolve to a readable member.</exception>
	internal static RequirementRule[] Compile(Type type, JsonSerializerOptions options)
	{
		List<RequirementRule> rules = [];

		foreach (MemberInfo member in EnumerateCandidateMembers(type))
		{
			JsonRequiredIfSiblingIsAttribute[] attributes =
				[.. member.GetCustomAttributes<JsonRequiredIfSiblingIsAttribute>(inherit: true)];

			if (attributes.Length == 0)
			{
				continue;
			}

			SiblingCondition[] conditions = BuildConditions(type, attributes);

			rules.Add(new RequirementRule(ResolveJsonName(member, options), member.Name, conditions));
		}

		return [.. rules];
	}

	private static SiblingCondition[] BuildConditions(Type type, JsonRequiredIfSiblingIsAttribute[] attributes)
	{
		List<SiblingCondition> conditions = [];

		foreach (IGrouping<string, JsonRequiredIfSiblingIsAttribute> group in
			attributes.GroupBy(attribute => attribute.SiblingName, StringComparer.Ordinal))
		{
			Func<object, object?> accessor = CreateAccessor(type, group.Key);
			object?[] values = [.. group.Select(attribute => attribute.Value)];

			conditions.Add(new SiblingCondition(group.Key, accessor, values));
		}

		return [.. conditions];
	}

	private static IEnumerable<MemberInfo> EnumerateCandidateMembers(Type type)
	{
		foreach (PropertyInfo property in type.GetProperties(MemberFlags))
		{
			yield return property;
		}

		foreach (FieldInfo field in type.GetFields(MemberFlags))
		{
			yield return field;
		}
	}

	private static string ResolveJsonName(MemberInfo member, JsonSerializerOptions options)
	{
		JsonPropertyNameAttribute? nameAttribute = member.GetCustomAttribute<JsonPropertyNameAttribute>(inherit: true);

		return nameAttribute is not null
			? nameAttribute.Name
			: options.PropertyNamingPolicy?.ConvertName(member.Name) ?? member.Name;
	}

	private static Func<object, object?> CreateAccessor(Type type, string siblingName)
	{
		PropertyInfo? property = type.GetProperty(siblingName, SiblingFlags);
		if (property is not null && property.CanRead)
		{
			return instance => property.GetValue(instance);
		}

		FieldInfo? field = type.GetField(siblingName, SiblingFlags);
		if (field is not null)
		{
			return instance => field.GetValue(instance);
		}

		throw new InvalidOperationException(
			$"[{nameof(JsonRequiredIfSiblingIsAttribute)}] on type '{type.Name}' names sibling '{siblingName}', which is not a readable property or field of that type.");
	}

	private static bool IsEligible(Type type)
	{
		if (type.IsPrimitive || type.IsEnum || type == typeof(string) || type == typeof(decimal))
		{
			return false;
		}

		if (typeof(IEnumerable).IsAssignableFrom(type))
		{
			return false;
		}

		foreach (MemberInfo member in EnumerateCandidateMembers(type))
		{
			if (member.IsDefined(typeof(JsonRequiredIfSiblingIsAttribute), inherit: true))
			{
				return true;
			}
		}

		return false;
	}
}
```

- [ ] **Step 6: Run the tests to verify they pass**

Run: `dotnet test --filter "FullyQualifiedName~RequirementRuleCompilerTests"`
Expected: PASS, 9 tests.

- [ ] **Step 7: Commit**

```bash
git add -A
git commit -m "[patch] Add requirement rule compilation"
```

---

### Task 5: Presence scanning

**Files:**
- Create: `JsonRequiredConditionally/PresenceScanner.cs`
- Create: `JsonRequiredConditionally.Test/PresenceScannerTests.cs`

**Interfaces:**
- Consumes: nothing.
- Produces: `internal static class PresenceScanner` with `internal static HashSet<string> ScanPropertyNames(Utf8JsonReader reader, StringComparer comparer)`.

The `reader` parameter is deliberately **by value**. `Utf8JsonReader` is a struct, so the copy advances independently and the caller's reader stays parked at `StartObject` ready for the real deserialization. Only the object's *immediate* property names are collected; nested values are skipped wholesale.

- [ ] **Step 1: Write the failing test**

`JsonRequiredConditionally.Test/PresenceScannerTests.cs`:

```csharp
// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace ktsu.JsonRequiredConditionally.Tests;

using System.Text;
using System.Text.Json;

[TestClass]
public class PresenceScannerTests
{
	private static HashSet<string> Scan(string json, StringComparer? comparer = null)
	{
		byte[] bytes = Encoding.UTF8.GetBytes(json);
		Utf8JsonReader reader = new(bytes);
		reader.Read();

		return PresenceScanner.ScanPropertyNames(reader, comparer ?? StringComparer.Ordinal);
	}

	[TestMethod]
	public void CollectsTopLevelPropertyNames()
	{
		HashSet<string> names = Scan("""{"a":1,"b":"x","c":null}""");

		Assert.HasCount(3, names);
		Assert.IsTrue(names.Contains("a"));
		Assert.IsTrue(names.Contains("b"));
		Assert.IsTrue(names.Contains("c"));
	}

	[TestMethod]
	public void RecordsExplicitNullAsPresent()
	{
		HashSet<string> names = Scan("""{"a":null}""");

		Assert.IsTrue(names.Contains("a"));
	}

	[TestMethod]
	public void IgnoresNestedPropertyNames()
	{
		HashSet<string> names = Scan("""{"outer":{"inner":1},"sibling":2}""");

		Assert.HasCount(2, names);
		Assert.IsTrue(names.Contains("outer"));
		Assert.IsTrue(names.Contains("sibling"));
		Assert.IsFalse(names.Contains("inner"));
	}

	[TestMethod]
	public void IgnoresPropertyNamesInsideArrays()
	{
		HashSet<string> names = Scan("""{"items":[{"inner":1},{"inner":2}],"count":2}""");

		Assert.HasCount(2, names);
		Assert.IsFalse(names.Contains("inner"));
	}

	[TestMethod]
	public void ReturnsEmptyForEmptyObject()
	{
		Assert.IsEmpty(Scan("{}"));
	}

	[TestMethod]
	public void ReturnsEmptyWhenNotPositionedOnAnObject()
	{
		Assert.IsEmpty(Scan("""[1,2,3]"""));
	}

	[TestMethod]
	public void HonoursCaseInsensitiveComparer()
	{
		HashSet<string> names = Scan("""{"Tuning":1}""", StringComparer.OrdinalIgnoreCase);

		Assert.IsTrue(names.Contains("tuning"));
	}

	[TestMethod]
	public void DoesNotAdvanceTheCallersReader()
	{
		byte[] bytes = Encoding.UTF8.GetBytes("""{"a":1,"b":2}""");
		Utf8JsonReader reader = new(bytes);
		reader.Read();

		PresenceScanner.ScanPropertyNames(reader, StringComparer.Ordinal);

		Assert.AreEqual(JsonTokenType.StartObject, reader.TokenType);
	}
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test --filter "FullyQualifiedName~PresenceScannerTests"`
Expected: FAIL — compilation error CS0103, `PresenceScanner` does not exist.

- [ ] **Step 3: Write the implementation**

`JsonRequiredConditionally/PresenceScanner.cs`:

```csharp
// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace ktsu.JsonRequiredConditionally;

using System.Text.Json;

/// <summary>
/// Collects the immediate property names of a JSON object without materializing it.
/// </summary>
internal static class PresenceScanner
{
	/// <summary>
	/// Reads forward over a copy of the reader, collecting the current object's property names.
	/// </summary>
	/// <param name="reader">A copy of the caller's reader, parked on <see cref="JsonTokenType.StartObject"/>.</param>
	/// <param name="comparer">The comparer matching the serializer's case sensitivity.</param>
	/// <returns>The set of property names physically present on the object.</returns>
	internal static HashSet<string> ScanPropertyNames(Utf8JsonReader reader, StringComparer comparer)
	{
		HashSet<string> names = new(comparer);

		if (reader.TokenType != JsonTokenType.StartObject)
		{
			return names;
		}

		while (reader.Read())
		{
			if (reader.TokenType == JsonTokenType.EndObject)
			{
				break;
			}

			if (reader.TokenType != JsonTokenType.PropertyName)
			{
				continue;
			}

			names.Add(reader.GetString()!);

			reader.Read();
			reader.Skip();
		}

		return names;
	}
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test --filter "FullyQualifiedName~PresenceScannerTests"`
Expected: PASS, 8 tests.

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "[patch] Add JSON property presence scanning"
```

---

### Task 6: Converter, factory, and re-entrancy

**Files:**
- Create: `JsonRequiredConditionally/JsonRequiredConditionallyConverter.cs`
- Create: `JsonRequiredConditionally/JsonRequiredConditionallyConverterFactory.cs`
- Create: `JsonRequiredConditionally/InnerOptionsCache.cs`
- Modify: `JsonRequiredConditionally.Test/TestModels.cs`
- Create: `JsonRequiredConditionally.Test/ConverterTests.cs`
- Create: `JsonRequiredConditionally.Test/NestingTests.cs`

**Interfaces:**
- Consumes: `RequirementRuleCompiler.HasRules`/`.Compile` (Task 4), `PresenceScanner.ScanPropertyNames` (Task 5), `JsonRequiredConditionallyException` (Task 3).
- Produces:
  - `public sealed class JsonRequiredConditionallyConverterFactory : JsonConverterFactory` with a public parameterless constructor.
  - `internal sealed class JsonRequiredConditionallyConverter<T> : JsonConverter<T>` with constructor `(JsonSerializerOptions options, JsonRequiredConditionallyConverterFactory factory)`.
  - `internal sealed class ExcludingFactory : JsonConverterFactory` with constructor `(Type excludedType, JsonRequiredConditionallyConverterFactory root, JsonSerializerOptions rootOptions)` and properties `ExcludedType`, `RootOptions`. The `root` parameter stays a primary-constructor capture — it needs no property.
  - `internal static class InnerOptionsCache` with `internal static JsonSerializerOptions Get(JsonSerializerOptions rootOptions, Type excludedType, JsonRequiredConditionallyConverterFactory factory)` and `internal static JsonSerializerOptions FindRoot(JsonSerializerOptions options)`.

The converter must materialize `T` without re-entering itself. The naive fix — strip the factory from the inner options — would silently disable validation for every *nested* decorated type, so it is wrong and must not be written. Instead the inner options excludes **only the type currently being converted**, and each frame **resets** the exclusion to its own type rather than accumulating. That keeps cyclic graphs (`T → U → T`) validated at every level, and terminates because recursion is bounded by JSON nesting depth, which `JsonSerializerOptions.MaxDepth` already caps.

Caching matters for the same reason: without it every nesting level allocates a fresh `JsonSerializerOptions`, and a fresh options object starts with an empty metadata cache. Keying by `(root options, excluded type)` bounds allocation to one clone per decorated type per root options.

- [ ] **Step 1: Add the nested and cyclic test models**

Append to `JsonRequiredConditionally.Test/TestModels.cs`:

```csharp
/// <summary>Holds a decorated child, to prove nested validation runs.</summary>
public sealed class OuterConfig
{
	public string? Label { get; set; }

	public SimpleConfig? Child { get; set; }
}

/// <summary>Holds decorated children in collections.</summary>
public sealed class CollectionConfig
{
	public List<SimpleConfig> Items { get; set; } = [];

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
```

- [ ] **Step 2: Write the failing converter test**

`JsonRequiredConditionally.Test/ConverterTests.cs`:

```csharp
// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace ktsu.JsonRequiredConditionally.Tests;

using System.Text.Json;

[TestClass]
public class ConverterTests
{
	internal static JsonSerializerOptions CreateOptions() =>
		new() { Converters = { new JsonRequiredConditionallyConverterFactory() } };

	[TestMethod]
	public void AbsentRequiredPropertyThrows()
	{
		JsonRequiredConditionallyException exception = Assert.ThrowsExactly<JsonRequiredConditionallyException>(
			() => JsonSerializer.Deserialize<SimpleConfig>("""{"Kind":"Advanced"}""", CreateOptions()));

		CollectionAssert.AreEqual(new List<string> { "Tuning" }, exception.MissingProperties.ToList());
	}

	[TestMethod]
	public void ExplicitNullSatisfiesTheRequirement()
	{
		SimpleConfig? config = JsonSerializer.Deserialize<SimpleConfig>(
			"""{"Kind":"Advanced","Tuning":null}""", CreateOptions());

		Assert.IsNotNull(config);
		Assert.IsNull(config.Tuning);
	}

	[TestMethod]
	public void PresentValueSatisfiesTheRequirement()
	{
		SimpleConfig? config = JsonSerializer.Deserialize<SimpleConfig>(
			"""{"Kind":"Advanced","Tuning":"fast"}""", CreateOptions());

		Assert.IsNotNull(config);
		Assert.AreEqual("fast", config.Tuning);
	}

	[TestMethod]
	public void NonMatchingSiblingLeavesPropertyOptional()
	{
		SimpleConfig? config = JsonSerializer.Deserialize<SimpleConfig>(
			"""{"Kind":"Expert"}""", CreateOptions());

		Assert.IsNotNull(config);
		Assert.IsNull(config.Tuning);
	}

	[TestMethod]
	public void OrSemanticsAcrossValuesOfOneSibling()
	{
		JsonSerializerOptions options = CreateOptions();

		Assert.ThrowsExactly<JsonRequiredConditionallyException>(
			() => JsonSerializer.Deserialize<OrConfig>("""{"Kind":"Advanced"}""", options));
		Assert.ThrowsExactly<JsonRequiredConditionallyException>(
			() => JsonSerializer.Deserialize<OrConfig>("""{"Kind":"Expert"}""", options));

		OrConfig? basic = JsonSerializer.Deserialize<OrConfig>("""{"Kind":"Basic"}""", options);
		Assert.IsNotNull(basic);
	}

	[TestMethod]
	public void AndSemanticsAcrossDifferentSiblings()
	{
		JsonSerializerOptions options = CreateOptions();

		Assert.ThrowsExactly<JsonRequiredConditionallyException>(
			() => JsonSerializer.Deserialize<AndConfig>("""{"Kind":"Advanced","Mode":"Remote"}""", options));

		AndConfig? local = JsonSerializer.Deserialize<AndConfig>("""{"Kind":"Advanced","Mode":"Local"}""", options);
		Assert.IsNotNull(local);
	}

	[TestMethod]
	public void UndecoratedTypeIsNotClaimedByTheFactory()
	{
		JsonRequiredConditionallyConverterFactory factory = new();

		Assert.IsFalse(factory.CanConvert(typeof(PlainConfig)));
		Assert.IsTrue(factory.CanConvert(typeof(SimpleConfig)));
	}

	[TestMethod]
	public void NullTokenDeserializesToNull()
	{
		SimpleConfig? config = JsonSerializer.Deserialize<SimpleConfig>("null", CreateOptions());

		Assert.IsNull(config);
	}

	[TestMethod]
	public void SerializationRoundTripsWithoutValidation()
	{
		SimpleConfig config = new() { Kind = Kind.Advanced, Tuning = null };

		string json = JsonSerializer.Serialize(config, CreateOptions());

		StringAssert.Contains(json, "Tuning");
	}
}
```

- [ ] **Step 3: Write the failing nesting test**

`JsonRequiredConditionally.Test/NestingTests.cs`:

```csharp
// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace ktsu.JsonRequiredConditionally.Tests;

using System.Text.Json;

[TestClass]
public class NestingTests
{
	private static JsonSerializerOptions CreateOptions() =>
		new() { Converters = { new JsonRequiredConditionallyConverterFactory() } };

	[TestMethod]
	public void NestedObjectIsValidated()
	{
		Assert.ThrowsExactly<JsonRequiredConditionallyException>(
			() => JsonSerializer.Deserialize<OuterConfig>(
				"""{"Label":"x","Child":{"Kind":"Advanced"}}""", CreateOptions()));
	}

	[TestMethod]
	public void ValidNestedObjectDeserializes()
	{
		OuterConfig? outer = JsonSerializer.Deserialize<OuterConfig>(
			"""{"Label":"x","Child":{"Kind":"Advanced","Tuning":"fast"}}""", CreateOptions());

		Assert.IsNotNull(outer);
		Assert.AreEqual("fast", outer.Child!.Tuning);
	}

	[TestMethod]
	public void ListElementsAreValidated()
	{
		Assert.ThrowsExactly<JsonRequiredConditionallyException>(
			() => JsonSerializer.Deserialize<CollectionConfig>(
				"""{"Items":[{"Kind":"Basic"},{"Kind":"Advanced"}],"Lookup":{}}""", CreateOptions()));
	}

	[TestMethod]
	public void DictionaryValuesAreValidated()
	{
		Assert.ThrowsExactly<JsonRequiredConditionallyException>(
			() => JsonSerializer.Deserialize<CollectionConfig>(
				"""{"Items":[],"Lookup":{"a":{"Kind":"Advanced"}}}""", CreateOptions()));
	}

	[TestMethod]
	public void CyclicTypeGraphValidatesAtEveryLevel()
	{
		string json = """
			{"Kind":"Basic","Next":{"Mode":"Local","Next":{"Kind":"Advanced"}}}
			""";

		Assert.ThrowsExactly<JsonRequiredConditionallyException>(
			() => JsonSerializer.Deserialize<NodeA>(json, CreateOptions()));
	}

	[TestMethod]
	public void DeeplyNestedValidGraphDeserializes()
	{
		string json = """
			{"Kind":"Advanced","Tuning":"a","Next":{"Mode":"Remote","Endpoint":"b","Next":{"Kind":"Basic"}}}
			""";

		NodeA? node = JsonSerializer.Deserialize<NodeA>(json, CreateOptions());

		Assert.IsNotNull(node);
		Assert.AreEqual("b", node.Next!.Endpoint);
	}

	[TestMethod]
	public void InnerOptionsAreCachedPerRootAndExcludedType()
	{
		JsonSerializerOptions root = CreateOptions();
		JsonRequiredConditionallyConverterFactory factory = new();

		JsonSerializerOptions first = InnerOptionsCache.Get(root, typeof(SimpleConfig), factory);
		JsonSerializerOptions second = InnerOptionsCache.Get(root, typeof(SimpleConfig), factory);
		JsonSerializerOptions other = InnerOptionsCache.Get(root, typeof(OrConfig), factory);

		Assert.AreSame(first, second);
		Assert.AreNotSame(first, other);
	}
}
```

- [ ] **Step 4: Run the tests to verify they fail**

Run: `dotnet test --filter "FullyQualifiedName~ConverterTests|FullyQualifiedName~NestingTests"`
Expected: FAIL — compilation error CS0246, `JsonRequiredConditionallyConverterFactory` could not be found.

- [ ] **Step 5: Write the inner options cache**

`JsonRequiredConditionally/InnerOptionsCache.cs`:

```csharp
// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace ktsu.JsonRequiredConditionally;

using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;

/// <summary>
/// Caches the per-type options clones used to materialize objects without converter re-entrancy.
/// </summary>
internal static class InnerOptionsCache
{
	private static readonly ConditionalWeakTable<JsonSerializerOptions, ConcurrentDictionary<Type, JsonSerializerOptions>> Cache = new();

	/// <summary>
	/// Gets the options used to materialize <paramref name="excludedType"/> without re-entering its own converter.
	/// </summary>
	/// <param name="rootOptions">The user's original options, shared by every frame.</param>
	/// <param name="excludedType">The type whose converter must be bypassed.</param>
	/// <param name="factory">The root factory, used for every other type.</param>
	/// <returns>A cached options instance.</returns>
	internal static JsonSerializerOptions Get(
		JsonSerializerOptions rootOptions,
		Type excludedType,
		JsonRequiredConditionallyConverterFactory factory)
	{
		ConcurrentDictionary<Type, JsonSerializerOptions> perType =
			Cache.GetValue(rootOptions, static _ => new ConcurrentDictionary<Type, JsonSerializerOptions>());

		return perType.GetOrAdd(excludedType, type => Build(rootOptions, type, factory));
	}

	/// <summary>
	/// Finds the root options for a frame by looking for a marker factory in the current options.
	/// </summary>
	/// <param name="options">The options a converter was created with.</param>
	/// <returns>The user's original options.</returns>
	internal static JsonSerializerOptions FindRoot(JsonSerializerOptions options)
	{
		foreach (JsonConverter converter in options.Converters)
		{
			if (converter is ExcludingFactory excluding)
			{
				return excluding.RootOptions;
			}
		}

		return options;
	}

	private static JsonSerializerOptions Build(
		JsonSerializerOptions rootOptions,
		Type excludedType,
		JsonRequiredConditionallyConverterFactory factory)
	{
		JsonSerializerOptions inner = new(rootOptions);

		for (int i = inner.Converters.Count - 1; i >= 0; i--)
		{
			if (inner.Converters[i] is JsonRequiredConditionallyConverterFactory or ExcludingFactory)
			{
				inner.Converters.RemoveAt(i);
			}
		}

		inner.Converters.Add(new ExcludingFactory(excludedType, factory, rootOptions));

		return inner;
	}
}
```

- [ ] **Step 6: Write the converter**

`JsonRequiredConditionally/JsonRequiredConditionallyConverter.cs`:

```csharp
// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace ktsu.JsonRequiredConditionally;

using System.Text.Json;
using System.Text.Json.Serialization;

/// <summary>
/// Validates conditional requirements for <typeparamref name="T"/> during deserialization.
/// </summary>
/// <typeparam name="T">The type being converted.</typeparam>
internal sealed class JsonRequiredConditionallyConverter<T> : JsonConverter<T>
{
	private readonly JsonSerializerOptions innerOptions;
	private readonly RequirementRule[] rules;
	private readonly StringComparer nameComparer;

	/// <summary>
	/// Initializes a new instance of the <see cref="JsonRequiredConditionallyConverter{T}"/> class.
	/// </summary>
	/// <param name="options">The options this converter was created for.</param>
	/// <param name="factory">The factory that created this converter.</param>
	internal JsonRequiredConditionallyConverter(JsonSerializerOptions options, JsonRequiredConditionallyConverterFactory factory)
	{
		Ensure.NotNull(options);
		Ensure.NotNull(factory);

		rules = RequirementRuleCompiler.Compile(typeof(T), options);
		nameComparer = options.PropertyNameCaseInsensitive ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
		innerOptions = InnerOptionsCache.Get(InnerOptionsCache.FindRoot(options), typeof(T), factory);
	}

	/// <inheritdoc/>
	public override T? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
	{
		if (reader.TokenType == JsonTokenType.Null)
		{
			return default;
		}

		HashSet<string> present = PresenceScanner.ScanPropertyNames(reader, nameComparer);

		T? value = JsonSerializer.Deserialize<T>(ref reader, innerOptions);

		if (value is not null)
		{
			Validate(value, present);
		}

		return value;
	}

	/// <inheritdoc/>
	public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options) =>
		JsonSerializer.Serialize(writer, value, innerOptions);

	private void Validate(T value, HashSet<string> present)
	{
		List<string>? missing = null;

		foreach (RequirementRule rule in rules)
		{
			if (present.Contains(rule.JsonName))
			{
				continue;
			}

			if (!rule.IsRequiredFor(value))
			{
				continue;
			}

			missing ??= [];
			missing.Add(rule.JsonName);
		}

		if (missing is not null)
		{
			throw new JsonRequiredConditionallyException(missing);
		}
	}
}
```

- [ ] **Step 7: Write the factory and the excluding factory**

`JsonRequiredConditionally/JsonRequiredConditionallyConverterFactory.cs`:

```csharp
// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace ktsu.JsonRequiredConditionally;

using System.Text.Json;
using System.Text.Json.Serialization;

/// <summary>
/// Creates converters that enforce <see cref="JsonRequiredIfSiblingIsAttribute"/> during deserialization.
/// </summary>
/// <remarks>
/// Add one instance to <see cref="JsonSerializerOptions.Converters"/>. Types with no decorated
/// members are not claimed and keep the serializer's normal fast path.
/// </remarks>
public sealed class JsonRequiredConditionallyConverterFactory : JsonConverterFactory
{
	/// <inheritdoc/>
	public override bool CanConvert(Type typeToConvert)
	{
		Ensure.NotNull(typeToConvert);

		return RequirementRuleCompiler.HasRules(typeToConvert);
	}

	/// <inheritdoc/>
	public override JsonConverter? CreateConverter(Type typeToConvert, JsonSerializerOptions options)
	{
		Ensure.NotNull(typeToConvert);
		Ensure.NotNull(options);

		Type converterType = typeof(JsonRequiredConditionallyConverter<>).MakeGenericType(typeToConvert);

		try
		{
			return (JsonConverter?)Activator.CreateInstance(
				converterType,
				BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public,
				binder: null,
				args: [options, this],
				culture: null);
		}
		catch (TargetInvocationException exception) when (exception.InnerException is not null)
		{
			ExceptionDispatchInfo.Capture(exception.InnerException).Throw();
			throw;
		}
	}
}
```

The `try`/`catch` is load-bearing, not defensive noise. `Activator.CreateInstance` with an `args` array wraps any constructor exception in a `TargetInvocationException`. Rule compilation runs in that constructor, so without the unwrap the `InvalidOperationException` for an unresolvable sibling name would reach callers as a meaningless reflection wrapper. `ExceptionDispatchInfo.Capture(...).Throw()` rethrows the original with its stack trace intact.

Required usings for this file, inside the namespace:

```csharp
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Text.Json;
using System.Text.Json.Serialization;
```

Append `ExcludingFactory` to the same file:

```csharp
/// <summary>
/// Delegates to the root factory for every type except one, breaking converter re-entrancy for
/// the type currently being materialized while leaving every other type validated.
/// </summary>
internal sealed class ExcludingFactory(
	Type excludedType,
	JsonRequiredConditionallyConverterFactory root,
	JsonSerializerOptions rootOptions) : JsonConverterFactory
{
	/// <summary>
	/// Gets the type this factory refuses to convert.
	/// </summary>
	internal Type ExcludedType { get; } = excludedType;

	/// <summary>
	/// Gets the user's original options, propagated so every frame shares one cache root.
	/// </summary>
	internal JsonSerializerOptions RootOptions { get; } = rootOptions;

	/// <inheritdoc/>
	public override bool CanConvert(Type typeToConvert) =>
		typeToConvert != ExcludedType && root.CanConvert(typeToConvert);

	/// <inheritdoc/>
	public override JsonConverter? CreateConverter(Type typeToConvert, JsonSerializerOptions options) =>
		root.CreateConverter(typeToConvert, options);
}
```

- [ ] **Step 8: Run the tests to verify they pass**

Run: `dotnet test --filter "FullyQualifiedName~ConverterTests|FullyQualifiedName~NestingTests"`
Expected: PASS, 16 tests.

- [ ] **Step 9: Run the full suite**

Run: `dotnet test`
Expected: PASS, all tests from Tasks 1-6.

- [ ] **Step 10: Commit**

```bash
git add -A
git commit -m "[patch] Add converter, factory, and nested graph validation"
```

---

### Task 7: Naming policy and case sensitivity

**Files:**
- Create: `JsonRequiredConditionally.Test/NamingTests.cs`

**Interfaces:**
- Consumes: everything from Task 6. No production changes are expected — this task proves the naming behavior already built in Tasks 4 and 6 works end to end.

If a test here fails, the fix belongs in `RequirementRuleCompiler.ResolveJsonName` or the `nameComparer` selection in the converter constructor.

- [ ] **Step 1: Write the failing test**

`JsonRequiredConditionally.Test/NamingTests.cs`:

```csharp
// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace ktsu.JsonRequiredConditionally.Tests;

using System.Text.Json;
using System.Text.Json.Serialization;

[TestClass]
public class NamingTests
{
	private static JsonSerializerOptions CreateOptions(
		JsonNamingPolicy? policy = null,
		bool caseInsensitive = false) =>
		new()
		{
			PropertyNamingPolicy = policy,
			PropertyNameCaseInsensitive = caseInsensitive,
			Converters =
			{
				new JsonStringEnumConverter(),
				new JsonRequiredConditionallyConverterFactory(),
			},
		};

	[TestMethod]
	public void CamelCasePolicyMatchesCamelCasedPayload()
	{
		JsonSerializerOptions options = CreateOptions(JsonNamingPolicy.CamelCase);

		SimpleConfig? config = JsonSerializer.Deserialize<SimpleConfig>(
			"""{"kind":"Advanced","tuning":"fast"}""", options);

		Assert.IsNotNull(config);
		Assert.AreEqual("fast", config.Tuning);
	}

	[TestMethod]
	public void CamelCasePolicyStillDetectsAbsence()
	{
		JsonSerializerOptions options = CreateOptions(JsonNamingPolicy.CamelCase);

		JsonRequiredConditionallyException exception = Assert.ThrowsExactly<JsonRequiredConditionallyException>(
			() => JsonSerializer.Deserialize<SimpleConfig>("""{"kind":"Advanced"}""", options));

		CollectionAssert.AreEqual(new List<string> { "tuning" }, exception.MissingProperties.ToList());
	}

	[TestMethod]
	public void JsonPropertyNameOverridesTheNamingPolicy()
	{
		JsonSerializerOptions options = CreateOptions(JsonNamingPolicy.CamelCase);

		RenamedConfig? config = JsonSerializer.Deserialize<RenamedConfig>(
			"""{"kind":"Advanced","tuning_value":"fast"}""", options);

		Assert.IsNotNull(config);
		Assert.AreEqual("fast", config.Tuning);
	}

	[TestMethod]
	public void JsonPropertyNameAbsenceIsReportedUnderTheJsonName()
	{
		JsonSerializerOptions options = CreateOptions(JsonNamingPolicy.CamelCase);

		JsonRequiredConditionallyException exception = Assert.ThrowsExactly<JsonRequiredConditionallyException>(
			() => JsonSerializer.Deserialize<RenamedConfig>("""{"kind":"Advanced"}""", options));

		CollectionAssert.AreEqual(new List<string> { "tuning_value" }, exception.MissingProperties.ToList());
	}

	[TestMethod]
	public void CaseInsensitiveMatchingAcceptsDifferentlyCasedPayload()
	{
		JsonSerializerOptions options = CreateOptions(caseInsensitive: true);

		SimpleConfig? config = JsonSerializer.Deserialize<SimpleConfig>(
			"""{"KIND":"Advanced","TUNING":"fast"}""", options);

		Assert.IsNotNull(config);
		Assert.AreEqual("fast", config.Tuning);
	}

	[TestMethod]
	public void CaseSensitiveMatchingTreatsMiscasedNameAsAbsent()
	{
		JsonSerializerOptions options = CreateOptions(caseInsensitive: false);

		Assert.ThrowsExactly<JsonRequiredConditionallyException>(
			() => JsonSerializer.Deserialize<SimpleConfig>("""{"Kind":"Advanced","TUNING":"fast"}""", options));
	}
}
```

- [ ] **Step 2: Run the tests**

Run: `dotnet test --filter "FullyQualifiedName~NamingTests"`
Expected: PASS, 6 tests. If any fail, fix `ResolveJsonName` or the comparer selection, then re-run until green.

- [ ] **Step 3: Commit**

```bash
git add -A
git commit -m "[patch] Cover naming policy and case sensitivity"
```

---

### Task 8: Materialization semantics

**Files:**
- Modify: `JsonRequiredConditionally.Test/TestModels.cs` (add record and multi-violation models)
- Create: `JsonRequiredConditionally.Test/SemanticsTests.cs`

**Interfaces:**
- Consumes: everything from Task 6. No production changes are expected. These tests pin behavior the spec commits to, so that a future refactor cannot silently change it.

- [ ] **Step 1: Add the remaining test models**

Append to `JsonRequiredConditionally.Test/TestModels.cs`:

```csharp
/// <summary>Constructor-parameterized, to prove evaluation happens after materialization.</summary>
public sealed record RecordConfig(
	Kind Kind,
	[property: JsonRequiredIfSiblingIs(nameof(Kind), Kind.Advanced)] string? Tuning);

/// <summary>Two independently violated members, to prove violations aggregate.</summary>
public sealed class MultiViolationConfig
{
	public Kind Kind { get; set; }

	[JsonRequiredIfSiblingIs(nameof(Kind), Kind.Advanced)]
	public string? Tuning { get; set; }

	[JsonRequiredIfSiblingIs(nameof(Kind), Kind.Advanced)]
	public string? Host { get; set; }
}

/// <summary>The zero enum value is a meaningful case, so an absent sibling matches it.</summary>
public sealed class ZeroValueConfig
{
	public Kind Kind { get; set; }

	[JsonRequiredIfSiblingIs(nameof(Kind), Kind.Basic)]
	public string? Name { get; set; }
}

/// <summary>A null-valued sibling condition.</summary>
public sealed class NullSiblingConfig
{
	public string? Kind { get; set; }

	[JsonRequiredIfSiblingIs(nameof(Kind), null)]
	public string? Fallback { get; set; }
}
```

- [ ] **Step 2: Write the failing test**

`JsonRequiredConditionally.Test/SemanticsTests.cs`:

```csharp
// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace ktsu.JsonRequiredConditionally.Tests;

using System.Text.Json;
using System.Text.Json.Serialization;

[TestClass]
public class SemanticsTests
{
	private static JsonSerializerOptions CreateOptions() =>
		new()
		{
			Converters =
			{
				new JsonStringEnumConverter(),
				new JsonRequiredConditionallyConverterFactory(),
			},
		};

	[TestMethod]
	public void RecordWithMissingRequiredPropertyThrows()
	{
		Assert.ThrowsExactly<JsonRequiredConditionallyException>(
			() => JsonSerializer.Deserialize<RecordConfig>("""{"Kind":"Advanced"}""", CreateOptions()));
	}

	[TestMethod]
	public void RecordWithPresentPropertyDeserializes()
	{
		RecordConfig? config = JsonSerializer.Deserialize<RecordConfig>(
			"""{"Kind":"Advanced","Tuning":"fast"}""", CreateOptions());

		Assert.IsNotNull(config);
		Assert.AreEqual("fast", config.Tuning);
	}

	[TestMethod]
	public void EveryViolationIsReportedInOneException()
	{
		JsonRequiredConditionallyException exception = Assert.ThrowsExactly<JsonRequiredConditionallyException>(
			() => JsonSerializer.Deserialize<MultiViolationConfig>("""{"Kind":"Advanced"}""", CreateOptions()));

		Assert.HasCount(2, exception.MissingProperties);
		CollectionAssert.Contains(exception.MissingProperties.ToList(), "Tuning");
		CollectionAssert.Contains(exception.MissingProperties.ToList(), "Host");
	}

	[TestMethod]
	public void AbsentSiblingReadsAsClrDefaultAndMatchesZeroValuedEnum()
	{
		Assert.ThrowsExactly<JsonRequiredConditionallyException>(
			() => JsonSerializer.Deserialize<ZeroValueConfig>("{}", CreateOptions()));
	}

	[TestMethod]
	public void NullSiblingMatchesNullCondition()
	{
		Assert.ThrowsExactly<JsonRequiredConditionallyException>(
			() => JsonSerializer.Deserialize<NullSiblingConfig>("""{"Kind":null}""", CreateOptions()));

		NullSiblingConfig? config = JsonSerializer.Deserialize<NullSiblingConfig>(
			"""{"Kind":"set"}""", CreateOptions());

		Assert.IsNotNull(config);
	}

	[TestMethod]
	public void UnresolvableSiblingSurfacesOnFirstUse()
	{
		Assert.ThrowsExactly<InvalidOperationException>(
			() => JsonSerializer.Deserialize<BrokenConfig>("""{"Kind":"Advanced"}""", CreateOptions()));
	}
}
```

- [ ] **Step 3: Run the tests**

Run: `dotnet test --filter "FullyQualifiedName~SemanticsTests"`
Expected: PASS, 6 tests.

If `UnresolvableSiblingSurfacesOnFirstUse` reports a wrapped exception rather than `InvalidOperationException` directly, STJ has wrapped it. Change the assertion to catch the wrapper and assert on `InnerException`, and note the actual behavior in the README.

- [ ] **Step 4: Run the full suite**

Run: `dotnet test`
Expected: PASS, all tests.

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "[patch] Pin record, aggregation, and absent-sibling semantics"
```

---

### Task 9: README and release readiness

**Files:**
- Create: `README.md`
- Modify: `CLAUDE.md` (create it for this repo)

**Interfaces:**
- Consumes: the complete public API.
- Produces: documentation only.

- [ ] **Step 1: Write `README.md`**

````markdown
# ktsu.JsonRequiredConditionally

A System.Text.Json attribute that makes a property required only when a sibling property has a given
value, enforced during deserialization.

## Install

```
dotnet add package ktsu.JsonRequiredConditionally
```

## Usage

Decorate the conditionally-required member, then register the factory once:

```csharp
using ktsu.JsonRequiredConditionally;

public sealed class Config
{
	public Kind Kind { get; set; }

	[JsonRequiredIfSiblingIs(nameof(Kind), Kind.Advanced)]
	public string? Tuning { get; set; }
}

JsonSerializerOptions options = new()
{
	Converters = { new JsonRequiredConditionallyConverterFactory() },
};

// throws JsonRequiredConditionallyException
JsonSerializer.Deserialize<Config>("""{"Kind":"Advanced"}""", options);

// succeeds
JsonSerializer.Deserialize<Config>("""{"Kind":"Basic"}""", options);
```

## Combining conditions

Attributes group implicitly by sibling name. Values within a group are OR-ed; the groups themselves
are AND-ed.

```csharp
[JsonRequiredIfSiblingIs(nameof(Kind), Kind.Advanced)]
[JsonRequiredIfSiblingIs(nameof(Kind), Kind.Expert)]
[JsonRequiredIfSiblingIs(nameof(Mode), Mode.Remote)]
public string? Tuning { get; set; }
// required when (Kind is Advanced OR Expert) AND (Mode is Remote)
```

This is what makes the common case — one enum sibling with several qualifying values — expressible.
AND-ing those would be unsatisfiable, since `Kind` cannot be both `Advanced` and `Expert`.

## Semantics

**Presence satisfies the requirement, not non-nullness.** A required property that is physically
present passes even when its value is `null`. This mirrors how `[JsonRequired]` itself behaves.

```jsonc
{ "Kind": "Advanced" }                  // absent  -> throws
{ "Kind": "Advanced", "Tuning": null }  // present -> passes
```

**Absent siblings read as their CLR default.** Sibling values are read from the materialized object,
so a sibling missing from the payload reads as `default`. An enum sibling whose zero value is a
meaningful case is therefore matched by an absent sibling:

```csharp
[JsonRequiredIfSiblingIs(nameof(Kind), Kind.Basic)]  // Basic == 0
public string? Name { get; set; }
// {} -> Kind defaults to Basic, so Name is required
```

If you need to distinguish "absent" from "defaulted", mark the sibling `[JsonRequired]` or make it
nullable.

**An unresolvable sibling name throws `InvalidOperationException`** the first time the type is used.
A typo'd `nameof` target is a coding error, and failing loudly beats a rule that quietly never fires.

**All violations are reported together, with paths.** `JsonRequiredConditionallyException.MissingProperties`
lists every property that was required but absent across the whole object graph, not just the first,
and each entry is a path rather than a bare name:

```csharp
// { "Kind": "Advanced", "Child": { "Kind": "Advanced" } }
exception.MissingProperties   // ["Tuning", "Child.Tuning"]
                              // arrays: "Children[1].Tuning"
                              // dicts:  "Lookup.a.Tuning"
```

**Serialization is not validated.** `Write` is plain delegation.

## Supported frameworks

`net10.0`, `net9.0`, `net8.0`, `net7.0`, `netstandard2.0`, `netstandard2.1`.

`net5.0` and `net6.0` are not supported. The graph walk needs `JsonSerializerOptions.GetTypeInfo`,
introduced in System.Text.Json 7.0; those frameworks resolve System.Text.Json from their shared
framework, where it predates the API. Both are also long out of support.

## How it works

`[JsonRequired]` maps to `JsonPropertyInfo.IsRequired`, a `bool` baked into a type's contract when
metadata is built — before any JSON is read. A condition that depends on sibling values cannot be
evaluated at that point, because the object does not exist yet. So this library cannot extend the
native required-check and instead supplies a converter.

The converter buffers its subtree, materializes it through a cached copy of your options with the
factory removed, then walks the materialized object graph alongside the JSON, applying rules at every
level. It validates the whole subtree itself rather than relying on System.Text.Json to re-enter it
for nested values — which it cannot, because converter resolution is cached per type, so a
self-referential type would go unvalidated below the outermost level.

The walk takes its member model from `JsonTypeInfo.Properties` — System.Text.Json's own view of which
members it populates — rather than from reflection. That is what keeps `[JsonIgnore]`, `IncludeFields`,
`[JsonInclude]` on non-public members, get-only properties and constructor binding all behaving the
same way in validation as they do in deserialization.

A type is claimed if it *reaches* a decorated member, directly or through its member graph. Reaching
rather than merely carrying is what lets violations be reported with paths rooted at the outermost
container.

## Limitations

**Types behind a custom converter are not validated.** System.Text.Json exposes no property model for
a type that has its own `JsonConverter`, so the walk cannot descend through one. Decorated types
reachable only behind a custom converter are skipped.

**Register the factory in `Converters`, not via `[JsonConverter]`.** Applying it as a type-level
attribute is not supported.

## License

MIT
````

- [ ] **Step 2: Write `CLAUDE.md`**

Mirror the structure of `C:\dev\ktsu-dev\Extensions\CLAUDE.md`: project overview, build and test commands, source organization table, key patterns, build system, test structure, version management.

- [ ] **Step 3: Verify the package builds for every target framework**

Run: `dotnet build --configuration Release`
Expected: no warnings, no errors, all six target frameworks build (`net5.0` and `net6.0` were dropped during Task 6R — see the design spec).

- [ ] **Step 4: Verify the package packs**

Run: `dotnet pack --configuration Release --output ./staging`
Expected: `ktsu.JsonRequiredConditionally.<version>.nupkg` produced in `./staging`.

- [ ] **Step 5: Run the full suite one final time**

Run: `dotnet test --configuration Release`
Expected: PASS, all tests.

- [ ] **Step 6: Commit**

```bash
git add -A
git commit -m "[patch] Add README and repo documentation"
```

---

## Verification Checklist

Before declaring the work complete, confirm each with actual command output:

- [ ] `dotnet build --configuration Release` produces zero warnings across all six target frameworks.
- [ ] `dotnet test` passes with every test from Tasks 1-8 green.
- [ ] `dotnet pack --configuration Release --output ./staging` produces a `.nupkg`.
- [ ] The public API is exactly three types: `JsonRequiredIfSiblingIsAttribute`, `JsonRequiredConditionallyConverterFactory`, `JsonRequiredConditionallyException`.
- [ ] No `[SuppressMessage]` attributes were added without a written justification.
- [ ] `VERSION.md`, `CHANGELOG.md`, and `LICENSE.md` were not hand-written.
