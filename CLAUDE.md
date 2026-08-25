# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

**ktsu.JsonRequiredConditionally** is a System.Text.Json library providing two attributes:
`[JsonRequiredIfSiblingIs(nameof(Sibling), value)]`, which makes a property or field required during
deserialization only when a sibling member holds a particular value, and `[JsonRequiredAndNotEmpty]`,
which makes a property or field required and non-empty unconditionally. It targets multiple frameworks:
net10.0, net9.0, net8.0, net7.0, netstandard2.0, and netstandard2.1.

## Build and Test Commands

```bash
# Build the library (all target frameworks)
dotnet build

# Build Release configuration
dotnet build --configuration Release

# Run all tests
dotnet test

# Run a specific test by name filter
dotnet test --filter "FullyQualifiedName~ConverterTests.AbsentRequiredPropertyThrows"

# Run tests in a specific test class
dotnet test --filter "FullyQualifiedName~NestingTests"

# Run one leg of the matrix only
dotnet test --framework net7.0

# Create NuGet package
dotnet pack --configuration Release --output ./staging
```

### Release verification with `ktsubuild`

CI (`.github/workflows/dotnet.yml`) does not build with raw `dotnet build`/`dotnet pack` or the
older PSBuild module — it runs `ktsu.KtsuBuild.Tool` (`ktsubuild`), installed as a dotnet tool.
Use the same tool locally before a release, not `dotnet build`/`pack` by hand:

```powershell
# Install once, to an isolated tool-path (keeps it out of the global tool set)
dotnet tool install ktsu.KtsuBuild.Tool --tool-path "$env:TEMP\ktsubuild"

# Verify restore, build and test across all six target frameworks
& "$env:TEMP\ktsubuild\ktsubuild" build --workspace "<repo-path>" --configuration Release --verbose

# Preview pack/publish/release without doing it
& "$env:TEMP\ktsubuild\ktsubuild" release --workspace "<repo-path>" --configuration Release --dry-run --verbose
```

Three hazards when running `ktsubuild` locally:

1. **`--workspace` defaults to the current working directory.** Always pass it explicitly —
   omitting it risks operating on the wrong repository.
2. **`ktsubuild release` without `--dry-run` genuinely packs, publishes and releases.** Always
   include `--dry-run` for local verification.
3. **Never run `ktsubuild ci` locally.** That is the full pipeline CI runs — it rewrites the
   generated metadata files (`VERSION.md`, `CHANGELOG.md`, `LICENSE.md`) and can tag and publish.

## Architecture

### Source Organization

The library is organized around one public entry point (the attribute) and a small internal pipeline
that compiles and applies its rules:

| File | Role |
|------|------|
| `JsonRequiredIfSiblingIsAttribute.cs` | Public attribute. Carries the sibling name and the accepted value; repeatable per member. |
| `JsonRequiredAndNotEmptyAttribute.cs` | Public attribute. Parameterless, `AllowMultiple = false`. Marks a member required and unconditionally non-empty, independent of any sibling. |
| `JsonRequiredConditionallyConverterFactory.cs` | Public `JsonConverterFactory`. Claims any type that reaches a decorated member (`RequirementRuleCompiler.HasRules`), runs `SerializerFeatureGuard.EnsureSupported`, then constructs a closed `JsonRequiredConditionallyConverter<T>` for it. Its constructor carries the `[RequiresUnreferencedCode]`/`[RequiresDynamicCode]` annotations — they cannot go on the overrides, whose base declarations have none. |
| `JsonRequiredConditionallyConverter.cs` | Internal `JsonConverter<T>`. Buffers the incoming subtree into a `JsonDocument`, deserializes it through a factory-free clone of the options (`PlainOptionsCache`), then hands the materialized object and the buffered JSON to `GraphValidator`. `Write` is plain delegation — serialization is not validated. |
| `GraphValidator.cs` | Walks the materialized object graph alongside its source `JsonElement`, recursing through nested objects, arrays (zipped against `JsonElement.EnumerateArray()`), and dictionaries (via `IDictionary` enumeration). Collects violations into two categories through `ViolationCollector`, missing for an absent property and empty for one present but empty, each as a dotted/indexed path (e.g. `Child.Tuning`, `Children[1].Tuning`, `Lookup.a.Tuning`), and throws one `JsonRequiredConditionallyException` carrying both lists at the end. |
| `RequirementRuleCompiler.cs` | Reflects over a type's `JsonTypeInfo.Properties` (System.Text.Json's own contract model, not raw reflection) to decide type eligibility (`HasRules`, via a reachability walk) and to compile `RequirementRule[]` for a type (`Compile`/`GetRules`, cached per `JsonSerializerOptions`). `CompileNonEmpty`/`GetNonEmptyRules` mirror `Compile`/`GetRules` for `[JsonRequiredAndNotEmpty]`, walking the same contract model and caching the same way in a parallel `ConditionalWeakTable`. `HasDirectlyDecoratedMember` probes for either attribute, so a type carrying only the new one is still claimed. Also owns `IsPopulatedByDeserialization`, which approximates System.Text.Json's constructor-binding rules for get-only properties — including its value-type rule: a `struct` without an explicit `[JsonConstructor]` always uses its implicit parameterless constructor, so none of its properties are constructor-bound however many public parameterized constructors it declares. Excludes polymorphic types from eligibility entirely. |
| `RequirementRule.cs` | Defines `SiblingCondition` (one sibling name plus its OR-ed accepted values) and `RequirementRule` (one decorated member plus its AND-ed conditions), plus `NonEmptyRule` (one decorated member, no conditions). Pure data plus the `IsSatisfiedBy` / `IsRequiredFor` predicates `GraphValidator` evaluates against a materialized instance. `NonEmptyRule` carries no predicate of its own, because it is evaluated against a `JsonElement`, not an instance. |
| `ValueMatcher.cs` | Compares a sibling's runtime value against an attribute's constant, widening the constant to the sibling's runtime type via `Convert.ChangeType` (invariantly) so an `int` argument matches a `long`/`short`/`byte`/`uint`/`nint` sibling, plus enum-aware coercion in both directions — an `int`, another enum with the same underlying value, or the enum member's *name* as a string all match an enum sibling. `CanEverMatch` answers the same question against a sibling's *declared* type, so `RequirementRuleCompiler` can reject a pairing that could never match instead of leaving a rule that quietly never fires. |
| `SerializerFeatureGuard.cs` | Rejects the serializer configurations the design cannot model, from `CreateConverter` (deliberately not the converter's constructor, which would wrap the exception in a `TargetInvocationException`). Currently `ReferenceHandler` and `JsonObjectCreationHandling.Populate`, the latter reached reflectively because the API postdates net7.0's in-box System.Text.Json and this library uses no conditional compilation. |
| `PresenceScanner.cs` | Collects the set of property names physically present on a `JsonElement` object, using the case sensitivity of the caller's `JsonSerializerOptions`. |
| `EmptinessInspector.cs` | Static, one method, `IsEmpty(JsonElement)`. Judges emptiness from the payload element itself, not the materialized value: `null`, a zero-length string, a zero-element array, and a property-less object are empty, numbers and booleans never are. Does not answer absence. The caller distinguishes that itself, since the two land in different violation categories. |
| `PlainOptionsCache.cs` | Caches, per `JsonSerializerOptions` instance (via `ConditionalWeakTable`), a clone with this library's factory removed. Materializing through the clone is what stops the converter re-entering itself. |
| `JsonRequiredConditionallyException.cs` | Public `JsonException` subclass. `MissingProperties` holds the full list of unmet-requirement paths from one validation pass, not just the first. `EmptyProperties` holds the parallel list for properties that were present but carried an empty value. |

### Key Patterns

**Contract-model-driven, not reflection-driven walking**: both `GraphValidator` and
`RequirementRuleCompiler` derive "which members does this type actually have" from
`JsonTypeInfo.Properties` — the same model System.Text.Json itself deserializes through — rather than
from `Type.GetProperties()`. This is what keeps `[JsonIgnore]`, `IncludeFields`, `[JsonInclude]` on
non-public members, get-only properties, and constructor binding behaving identically in validation and
in deserialization. It is also why a type behind its own custom `JsonConverter` cannot be validated:
System.Text.Json leaves `JsonTypeInfo.Properties` empty for such a type.

The one deliberate exception is *eligibility*, which a converter factory must decide from a `Type`
alone, with no caller options in hand. `HasDirectlyDecoratedMember` therefore probes with
`IncludeFields = true` so a decorated plain field still claims its type; rule compilation then runs
against the caller's real options, so with `IncludeFields = false` no rule is produced and nothing is
enforced — the type is merely buffered for nothing. Reachability (`EnumerateReachableMemberTypes`)
keeps the fields-off probe, because it must agree with what the walk will actually descend into.

**Factory-free re-entry**: the converter must deserialize through a `JsonSerializerOptions` that does
not carry `JsonRequiredConditionallyConverterFactory` itself, or every claimed type would recurse into
its own converter instead of being populated. `PlainOptionsCache` produces and caches that clone once
per options instance.

**Two options instances, two jobs**: `GraphValidator.Validate` takes both `plainOptions` (factory-free;
drives which members exist, via `RequirementRuleCompiler`) and `userOptions` (the caller's own; drives
naming policy and case sensitivity for matching JSON property names). Passing the wrong one for either
job silently breaks in specific, documented ways — see the remarks on `GraphValidator.Walk`.

**Reachability, not just direct decoration**: `RequirementRuleCompiler.HasRules` claims a type if a
decorated member exists anywhere in its transitively reachable member graph, not only directly on the
type. A container with no decorated member of its own (e.g. `List<Decorated>`) must still be claimed at
its own top so validation, and path context, extend all the way down. Collection member types are
unwrapped *recursively* (`EnumerateElementTypes`), so `List<List<T>>`, `T[][]` and
`Dictionary<string, List<T>>` reach `T` — unwrapping one level left the holder unclaimed and collapsed
`Grid[0][0].Tuning` to a bare `Tuning`.

**Containment, not best effort**: the converter materializes through factory-free options, so the walk
is the *sole* validator inside its subtree. Any serializer feature that changes what the graph means
but is invisible to `JsonTypeInfo.Properties` becomes either silent non-validation or a false positive.
Two responses exist, and which one applies is deliberate: polymorphic hierarchies are **not claimed**
(a working model must keep working when the library is registered), while `ReferenceHandler` and
`JsonObjectCreationHandling.Populate` **throw** (there is no way to be silently right).

The throws are split by the path they can actually affect, in `SerializerFeatureGuard`:

| Feature | Refused in | Read | Write |
|---------|-----------|------|-------|
| `ReferenceHandler` | `EnsureCanClaim` (factory) | throws | throws |
| Type-level `[JsonObjectCreationHandling]` | `EnsureCanClaim` (factory) | throws | throws |
| `JsonSerializerOptions.PreferredObjectCreationHandling` | `EnsureCanRead` (converter `Read`) | throws | works |
| Property-level `[JsonObjectCreationHandling]` | `EnsureCanRead` (converter `Read`) | throws | works |

`Populate` is deserialization-only, so refusing it on write would protect nothing — except for the
type-level route, where claiming the type makes its `JsonTypeInfoKind` `None` and System.Text.Json then
refuses to apply the attribute in *either* direction; throwing first replaces its
`InvalidOperationException: Invalid JsonTypeInfo operation for JsonTypeInfoKind 'None'` with an
explanation. Neither check may move into the converter's **constructor**: `Activator.CreateInstance`
would wrap it in `TargetInvocationException`. `Read` is safe because System.Text.Json calls it directly.

Getting the holder *claimed* is what routes it into the guard at all. `IsPopulatedByDeserialization`
therefore treats a get-only property as populated when its declaring contract prefers `Populate` —
without that, a holder whose only decorated member sat behind a populated get-only property was never
claimed, was never asked about, and silently lost its payload when the inner type's converter returned
a fresh instance the holder had no setter to accept.

**Presence, not non-nullness**: a rule is satisfied when the JSON property is present in the payload,
regardless of whether its value is `null` — mirroring `[JsonRequired]`'s own semantics.

`[JsonRequiredAndNotEmpty]` is the one stated exception to this pattern. It treats `null` as empty
rather than as present, because a member that is required and not empty is not satisfied by an
explicit null. Emptiness itself is judged from the payload's `JsonElement`, not the materialized CLR
value, specifically so it can see through a member behind its own custom converter, at the accepted
cost that a converter mapping a non-empty JSON representation onto an empty collection is judged
non-empty.

### Build System

Uses the ktsu.Sdk custom MSBuild SDK for standardized configuration. Key files:
- `global.json` — SDK versions (.NET SDK, MSTest.Sdk, ktsu.Sdk)
- `Directory.Packages.props` — Central Package Management for NuGet dependencies (`Polyfill`, `System.Text.Json`)
- `.editorconfig` — Strict code style enforcement (all rules as errors)

`System.Text.Json` is referenced explicitly only for `netstandard2.0` and `netstandard2.1`; the other
target frameworks resolve it from their shared framework, which is why `net5.0` and `net6.0` cannot be
supported — their shared-framework version of System.Text.Json predates `JsonSerializerOptions.GetTypeInfo`
(introduced in 7.0), which the graph walk depends on.

### Test Structure

Tests use MSTest.Sdk and are multi-targeted to `net10.0;net9.0;net8.0;net7.0`, so each in-box
System.Text.Json version the library ships against is actually exercised rather than merely compiled
against. Test files are organized by concern rather than one-to-one with source files:
- `JsonRequiredConditionally.Test/AttributeTests.cs` — attribute construction and metadata
- `JsonRequiredConditionally.Test/ContainmentTests.cs` — polymorphism, `Populate` and `ReferenceHandler`: what is refused and what throws
- `JsonRequiredConditionally.Test/ConverterTests.cs` — end-to-end deserialization behavior
- `JsonRequiredConditionally.Test/EligibilityTests.cs` — which types are claimed: decorated fields, nested collections, struct constructor binding
- `JsonRequiredConditionally.Test/EmptinessInspectorTests.cs` — `EmptinessInspector.IsEmpty` unit tests
- `JsonRequiredConditionally.Test/ExceptionTests.cs` — `JsonRequiredConditionallyException` construction and messages
- `JsonRequiredConditionally.Test/NamingTests.cs` — naming policies and `[JsonPropertyName]` interaction
- `JsonRequiredConditionally.Test/NestingTests.cs` — nested objects, arrays, and dictionaries, and path formatting
- `JsonRequiredConditionally.Test/NotEmptyTests.cs` — `[JsonRequiredAndNotEmpty]` end-to-end, every payload shape from the semantics table against every member shape
- `JsonRequiredConditionally.Test/PresenceScannerTests.cs` — `PresenceScanner` unit tests
- `JsonRequiredConditionally.Test/RequirementRuleCompilerTests.cs` — `RequirementRuleCompiler` unit tests
- `JsonRequiredConditionally.Test/RuntimeMatrixTests.cs` — asserts each leg runs on its own shared framework
- `JsonRequiredConditionally.Test/SemanticsTests.cs` — presence-vs-nullness, default-sibling, and other documented semantics
- `JsonRequiredConditionally.Test/SerializerCapabilities.cs` — runtime probes for System.Text.Json features that differ across versions
- `JsonRequiredConditionally.Test/SiblingMatchingTests.cs` — widened and enum-name sibling matching, and unconvertible pairings
- `JsonRequiredConditionally.Test/TrimAnnotationTests.cs` — the public factory's trim/AOT annotations
- `JsonRequiredConditionally.Test/ValueMatcherTests.cs` — `ValueMatcher` unit tests
- `JsonRequiredConditionally.Test/TestModels.cs` — shared model types used across the above

Three things about the matrix are easy to break and were each a real trap:

1. **ktsu.Sdk pins `RuntimeFrameworkVersion` to `10.0.0` for every target framework.** Left alone,
   all four legs run on the .NET 10 shared framework and load System.Text.Json 10, so the matrix
   tests nothing but compile compatibility. The test project clears it and sets
   `RollForward=LatestPatch`; `RuntimeMatrixTests` fails the build if that is ever lost.
2. **Running the matrix needs the .NET 7, 8, 9 and 10 runtimes installed.** CI installs them via
   `actions/setup-dotnet`. Locally, a leg whose runtime is missing fails to launch outright.
3. **MSTest.Sdk 4.x has no `net7.0` asset**, so the test project pins MSTest.Sdk `3.11.1` rather than
   the `4.3.3` in `global.json`. Dropping that pin silently drops net7.0 from the matrix.

Not covered: the `netstandard2.0`/`netstandard2.1` assets, which bind System.Text.Json from NuGet.
Reaching those needs a `net472` test leg — a genuine follow-up.

`InternalsVisibleTo("ktsu.JsonRequiredConditionally.Test")` in `AssemblyInfo.cs` lets the test project
exercise the internal pipeline types (`GraphValidator`, `RequirementRuleCompiler`, `RequirementRule`,
`ValueMatcher`, `PresenceScanner`, `PlainOptionsCache`) directly, not just through the public converter.

## Version Management

Versioning is calculated automatically from git history. Include version markers in commit messages:
- `[major]` — Breaking API changes
- `[minor]` — New features (auto-detected for .cs file changes)
- `[patch]` — Bug fixes
- `[pre]` — Pre-release/experimental changes

Auto-generated files (do not edit manually): `VERSION.md`, `CHANGELOG.md`, `LICENSE.md`
