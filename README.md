# ktsu.JsonRequiredConditionally

> A System.Text.Json attribute that makes a property required only when a sibling property has a given value, enforced during deserialization.

[![License](https://img.shields.io/github/license/ktsu-dev/JsonRequiredConditionally.svg?label=License&logo=nuget)](LICENSE.md)
[![NuGet Version](https://img.shields.io/nuget/v/ktsu.JsonRequiredConditionally?label=Stable&logo=nuget)](https://nuget.org/packages/ktsu.JsonRequiredConditionally)
[![NuGet Version](https://img.shields.io/nuget/vpre/ktsu.JsonRequiredConditionally?label=Latest&logo=nuget)](https://nuget.org/packages/ktsu.JsonRequiredConditionally)
[![NuGet Downloads](https://img.shields.io/nuget/dt/ktsu.JsonRequiredConditionally?label=Downloads&logo=nuget)](https://nuget.org/packages/ktsu.JsonRequiredConditionally)
[![GitHub commit activity](https://img.shields.io/github/commit-activity/m/ktsu-dev/JsonRequiredConditionally?label=Commits&logo=github)](https://github.com/ktsu-dev/JsonRequiredConditionally/commits/main)
[![GitHub contributors](https://img.shields.io/github/contributors/ktsu-dev/JsonRequiredConditionally?label=Contributors&logo=github)](https://github.com/ktsu-dev/JsonRequiredConditionally/graphs/contributors)
[![GitHub Actions Workflow Status](https://img.shields.io/github/actions/workflow/status/ktsu-dev/JsonRequiredConditionally/dotnet.yml?label=Build&logo=github)](https://github.com/ktsu-dev/JsonRequiredConditionally/actions)

## Introduction

`ktsu.JsonRequiredConditionally` fills a gap in System.Text.Json's built-in `[JsonRequired]`: a property that
is only mandatory under some condition, typically the value of a sibling discriminator property. Rather than
hand-rolling that check in a custom converter or in post-deserialization validation, decorate the member with
`[JsonRequiredIfSiblingIs(nameof(Sibling), value)]` and register one factory. The library walks the whole
materialized object graph — nested objects, arrays, and dictionaries — and reports every violation it finds in
a single exception, with a path to each one.

## Features

- **Conditional requirement**: `[JsonRequiredIfSiblingIs(nameof(Sibling), value)]` marks a member required
  only when a sibling holds a specific value.
- **OR within a sibling, AND across siblings**: repeat the attribute with the same sibling name to accept
  several qualifying values; use different sibling names to require all of several conditions at once.
- **Presence-based, like `[JsonRequired]`**: a required property that is physically present in the payload
  passes even when its value is `null`.
- **Whole-graph validation**: nested objects, arrays, and dictionaries are all walked, and every missing
  property is reported together, not just the first.
- **Path-qualified errors**: `JsonRequiredConditionallyException.MissingProperties` reports paths such as
  `Child.Tuning`, `Children[1].Tuning`, and `Lookup.a.Tuning`, not bare member names.
- **Drop-in converter factory**: one line in `JsonSerializerOptions.Converters` opts a whole object graph in;
  types with no decorated members keep the serializer's normal fast path.

## Installation

### Package Manager Console

```powershell
Install-Package ktsu.JsonRequiredConditionally
```

### .NET CLI

```bash
dotnet add package ktsu.JsonRequiredConditionally
```

### Package Reference

```xml
<PackageReference Include="ktsu.JsonRequiredConditionally" Version="x.y.z" />
```

## Usage Examples

### Basic Example

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

### Combining Conditions

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

## Supported Frameworks

`net10.0`, `net9.0`, `net8.0`, `net7.0`, `netstandard2.0`, `netstandard2.1`.

`net5.0` and `net6.0` are not supported. The graph walk needs `JsonSerializerOptions.GetTypeInfo`,
introduced in System.Text.Json 7.0; those frameworks resolve System.Text.Json from their shared
framework, where it predates the API. Both are also long out of support.

## How It Works

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

## API Reference

### `JsonRequiredIfSiblingIsAttribute`

Marks a property or field as required during deserialization only when a named sibling member has one
of the given values. Repeatable; `AllowMultiple = true`, `Inherited = true`.

#### Constructor

| Signature | Description |
|-----------|-------------|
| `JsonRequiredIfSiblingIsAttribute(string siblingName, object? value)` | `siblingName` is the CLR name of the sibling to inspect (use `nameof`); `value` is one value that satisfies the condition. |

#### Properties

| Name | Type | Description |
|------|------|-------------|
| `SiblingName` | `string` | The CLR name of the sibling member to inspect. |
| `Value` | `object?` | The value the sibling must have for the decorated member to be required. |

### `JsonRequiredConditionallyConverterFactory`

A `JsonConverterFactory` that claims any type reaching a member decorated with
`JsonRequiredIfSiblingIsAttribute`. Add one instance to `JsonSerializerOptions.Converters`. Types with
no decorated members are not claimed and keep the serializer's normal fast path.

### `JsonRequiredConditionallyException`

A `JsonException` thrown when one or more properties were required by their sibling values but were
absent from the JSON payload.

#### Properties

| Name | Type | Description |
|------|------|-------------|
| `MissingProperties` | `IReadOnlyList<string>` | The paths of every property that was required but absent, e.g. `Tuning`, `Child.Tuning`, `Children[1].Tuning`, `Lookup.a.Tuning`. |

## Contributing

Contributions are welcome! Feel free to open issues or submit pull requests.

## License

This project is licensed under the MIT License. See the [LICENSE.md](LICENSE.md) file for details.
