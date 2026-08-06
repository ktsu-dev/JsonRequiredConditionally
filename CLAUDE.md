# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

**ktsu.JsonRequiredConditionally** is a System.Text.Json library providing a single attribute,
`[JsonRequiredIfSiblingIs(nameof(Sibling), value)]`, that makes a property or field required during
deserialization only when a sibling member holds a particular value. It targets multiple frameworks:
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
dotnet test --filter "FullyQualifiedName~ConverterTests.ThrowsWhenRequiredPropertyIsAbsent"

# Run tests in a specific test class
dotnet test --filter "FullyQualifiedName~GraphValidatorTests"

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
| `JsonRequiredConditionallyConverterFactory.cs` | Public `JsonConverterFactory`. Claims any type that reaches a decorated member (`RequirementRuleCompiler.HasRules`) and constructs a closed `JsonRequiredConditionallyConverter<T>` for it. |
| `JsonRequiredConditionallyConverter.cs` | Internal `JsonConverter<T>`. Buffers the incoming subtree into a `JsonDocument`, deserializes it through a factory-free clone of the options (`PlainOptionsCache`), then hands the materialized object and the buffered JSON to `GraphValidator`. `Write` is plain delegation — serialization is not validated. |
| `GraphValidator.cs` | Walks the materialized object graph alongside its source `JsonElement`, recursing through nested objects, arrays (zipped against `JsonElement.EnumerateArray()`), and dictionaries (via `IDictionary` enumeration). Collects every missing required property as a dotted/indexed path (e.g. `Child.Tuning`, `Children[1].Tuning`, `Lookup.a.Tuning`) and throws one `JsonRequiredConditionallyException` at the end. |
| `RequirementRuleCompiler.cs` | Reflects over a type's `JsonTypeInfo.Properties` (System.Text.Json's own contract model, not raw reflection) to decide type eligibility (`HasRules`, via a reachability walk) and to compile `RequirementRule[]` for a type (`Compile`/`GetRules`, cached per `JsonSerializerOptions`). Also owns `IsPopulatedByDeserialization`, which approximates System.Text.Json's constructor-binding rules for get-only properties. |
| `RequirementRule.cs` | Defines `SiblingCondition` (one sibling name plus its OR-ed accepted values) and `RequirementRule` (one decorated member plus its AND-ed conditions). Pure data plus the `IsSatisfiedBy` / `IsRequiredFor` predicates `GraphValidator` evaluates against a materialized instance. |
| `ValueMatcher.cs` | Compares a sibling's runtime value against an attribute's constant, with enum-aware coercion (via `Convert.ChangeType` against the enum's underlying type) so that, e.g., an `int`-backed attribute value still matches an enum-typed sibling. |
| `PresenceScanner.cs` | Collects the set of property names physically present on a `JsonElement` object, using the case sensitivity of the caller's `JsonSerializerOptions`. |
| `PlainOptionsCache.cs` | Caches, per `JsonSerializerOptions` instance (via `ConditionalWeakTable`), a clone with this library's factory removed. Materializing through the clone is what stops the converter re-entering itself. |
| `JsonRequiredConditionallyException.cs` | Public `JsonException` subclass. `MissingProperties` holds the full list of unmet-requirement paths from one validation pass, not just the first. |

### Key Patterns

**Contract-model-driven, not reflection-driven walking**: both `GraphValidator` and
`RequirementRuleCompiler` derive "which members does this type actually have" from
`JsonTypeInfo.Properties` — the same model System.Text.Json itself deserializes through — rather than
from `Type.GetProperties()`. This is what keeps `[JsonIgnore]`, `IncludeFields`, `[JsonInclude]` on
non-public members, get-only properties, and constructor binding behaving identically in validation and
in deserialization. It is also why a type behind its own custom `JsonConverter` cannot be validated:
System.Text.Json leaves `JsonTypeInfo.Properties` empty for such a type.

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
its own top so validation, and path context, extend all the way down.

**Presence, not non-nullness**: a rule is satisfied when the JSON property is present in the payload,
regardless of whether its value is `null` — mirroring `[JsonRequired]`'s own semantics.

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

Tests use MSTest.Sdk and target .NET 10.0 only. Test files are organized by concern rather than
one-to-one with source files:
- `JsonRequiredConditionally.Test/AttributeTests.cs` — attribute construction and metadata
- `JsonRequiredConditionally.Test/ConverterTests.cs` — end-to-end deserialization behavior
- `JsonRequiredConditionally.Test/ExceptionTests.cs` — `JsonRequiredConditionallyException` construction and messages
- `JsonRequiredConditionally.Test/NamingTests.cs` — naming policies and `[JsonPropertyName]` interaction
- `JsonRequiredConditionally.Test/NestingTests.cs` — nested objects, arrays, and dictionaries, and path formatting
- `JsonRequiredConditionally.Test/PresenceScannerTests.cs` — `PresenceScanner` unit tests
- `JsonRequiredConditionally.Test/RequirementRuleCompilerTests.cs` — `RequirementRuleCompiler` unit tests
- `JsonRequiredConditionally.Test/SemanticsTests.cs` — presence-vs-nullness, default-sibling, and other documented semantics
- `JsonRequiredConditionally.Test/ValueMatcherTests.cs` — `ValueMatcher` unit tests
- `JsonRequiredConditionally.Test/TestModels.cs` — shared model types used across the above

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
