# ktsu.JsonRequiredConditionally

> A System.Text.Json attribute that makes a property required only when a sibling property has a given value, enforced during deserialization.

[![License](https://img.shields.io/github/license/ktsu-dev/JsonRequiredConditionally.svg?label=License&logo=nuget)](LICENSE.md)
[![NuGet Version](https://img.shields.io/nuget/v/ktsu.JsonRequiredConditionally?label=Stable&logo=nuget)](https://nuget.org/packages/ktsu.JsonRequiredConditionally)
[![NuGet Version](https://img.shields.io/nuget/vpre/ktsu.JsonRequiredConditionally?label=Latest&logo=nuget)](https://nuget.org/packages/ktsu.JsonRequiredConditionally)
[![NuGet Downloads](https://img.shields.io/nuget/dt/ktsu.JsonRequiredConditionally?label=Downloads&logo=nuget)](https://nuget.org/packages/ktsu.JsonRequiredConditionally)
[![GitHub commit activity](https://img.shields.io/github/commit-activity/m/ktsu-dev/JsonRequiredConditionally?label=Commits&logo=github)](https://github.com/ktsu-dev/JsonRequiredConditionally/commits/main)
[![GitHub contributors](https://img.shields.io/github/contributors/ktsu-dev/JsonRequiredConditionally?label=Contributors&logo=github)](https://github.com/ktsu-dev/JsonRequiredConditionally/graphs/contributors)
[![GitHub Actions Workflow Status](https://img.shields.io/github/actions/workflow/status/ktsu-dev/JsonRequiredConditionally/dotnet.yml?branch=main&label=Build&logo=github)](https://github.com/ktsu-dev/JsonRequiredConditionally/actions)

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
using System.Text.Json;
using System.Text.Json.Serialization;

public sealed class Config
{
	public Kind Kind { get; set; }

	[JsonRequiredIfSiblingIs(nameof(Kind), Kind.Advanced)]
	public string? Tuning { get; set; }
}

JsonSerializerOptions options = new()
{
	Converters =
	{
		// Needed only because this example writes Kind as a string ("Advanced"); it is not a
		// requirement of JsonRequiredConditionally itself, which works with any converter setup.
		new JsonStringEnumConverter(),
		new JsonRequiredConditionallyConverterFactory(),
	},
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

**The attribute value is widened to the sibling's type before comparing.** An attribute argument can
only be a compile-time constant, so it is routinely a different type from the sibling it is compared
against. `[JsonRequiredIfSiblingIs(nameof(Count), 1)]` matches a `long`, `short`, `byte`, `uint` or
`nint` sibling, and an enum sibling is matched by an `int`, by another enum with the same underlying
value, or by the enum member's own name written as a string:

```csharp
[JsonRequiredIfSiblingIs(nameof(Kind), "Advanced")]  // matches Kind == Kind.Advanced
```

String siblings are still compared ordinally against string constants only; a number is never
formatted into a string to make it match. **A pairing that could never match throws
`InvalidOperationException`** at first use, for the same reason an unresolvable sibling name does.

**Members System.Text.Json would not populate are not validated below.** A get-only property on a
`struct` is one of these: System.Text.Json uses a value type's implicit parameterless constructor
unless an explicit `[JsonConstructor]` says otherwise, so such a property keeps its default and the
JSON that would have fed it is discarded. Nothing beneath it is validated, because validating it would
report violations against a payload the serializer never applied.

**All violations are reported together, with paths.** `JsonRequiredConditionallyException.MissingProperties`
lists every property that was required but absent across the whole object graph, not just the first,
and each entry is a path rather than a bare name:

```csharp
// { "Kind": "Advanced", "Child": { "Kind": "Advanced" } }
exception.MissingProperties   // ["Tuning", "Child.Tuning"]
                              // arrays: "Children[1].Tuning"
                              // dicts:  "Lookup.a.Tuning"
```

**Serialization is not validated.** `Write` is plain delegation. It can still throw, but only to refuse
a configuration this library cannot support: `ReferenceHandler` and a type-level
`[JsonObjectCreationHandling]` are rejected on write as well as on read. See
[Limitations](#limitations).

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
members it populates — rather than from reflection. That is what keeps `[JsonIgnore]`,
`[JsonInclude]` on non-public members, get-only properties and constructor binding all behaving the
same way in validation as they do in deserialization.

Whether a type is *claimed* is decided before any caller options exist, because a converter factory's
`CanConvert` is given only a `Type`. That check therefore looks for decorated members with fields
included, so a plain public field carrying the attribute claims its type — but the rules themselves are
compiled against your real options, so with `IncludeFields = false` no rule is produced for a field and
nothing is enforced on it, exactly as System.Text.Json would not populate it. With
`IncludeFields = true` the rule exists and is enforced.

A type is claimed if it *reaches* a decorated member, directly or through its member graph. Reaching
rather than merely carrying is what lets violations be reported with paths rooted at the outermost
container.

## Limitations

**Types behind a custom converter are not validated.** System.Text.Json exposes no property model for
a type that has its own `JsonConverter`, so the walk cannot descend through one. Decorated types
reachable only behind a custom converter are skipped.

**Polymorphic hierarchies are not claimed, and therefore not validated.** A type carrying
`[JsonPolymorphic]` or `[JsonDerivedType]`, or deriving from one that does, is skipped: System.Text.Json
writes and reads the type discriminator around the derived type's converter and refuses outright when
that converter is a custom one, so claiming such a type would break a working polymorphic model on
both read and write merely by registering this library. Refusing to claim keeps it working; the cost is
no enforcement inside the hierarchy.

That cost extends one step further than it first appears. A container whose *only* route to a decorated
type runs through a polymorphic member is itself left unclaimed, so violations found beneath it are
reported with the path rooted at the inner type rather than at the container — `Tuning` rather than
`Shape.Child.Tuning`. The requirement is still enforced; only the prefix is lost. Refusal is also
decided by *declared* type participation, so a decorated class that merely implements an interface
carrying `[JsonDerivedType]` is skipped even when it is deserialized concretely and no polymorphic
dispatch ever happens.

**`JsonObjectCreationHandling.Populate` is not supported and throws `NotSupportedException`.** The
converter re-materializes each claimed subtree into a fresh instance, so the object System.Text.Json
intended to populate would be discarded together with anything already in it. All three routes to
`Populate` are refused — `JsonSerializerOptions.PreferredObjectCreationHandling`, a type-level
`[JsonObjectCreationHandling]`, and a property-level one. Either unregister the factory or stop using
`Populate`.

The options-level and property-level routes throw on **deserialization only**; serializing with them
configured still works, because `Populate` cannot affect writing. The type-level route is the
exception: it throws on both. Claiming a type gives it a converter, which makes its `JsonTypeInfoKind`
`None`, and System.Text.Json then refuses to apply a type-level `[JsonObjectCreationHandling]` to a
contract with no property model — in either direction. There is no working write path to preserve
there, so the library throws first and says why, instead of leaving you with
`InvalidOperationException: Invalid JsonTypeInfo operation for JsonTypeInfoKind 'None'`.

**`ReferenceHandler` is not supported and throws `NotSupportedException`, on both read and write.** The
buffered JSON of a `$ref` node carries none of the referenced object's properties, so every armed
requirement on it would be reported missing, and a `$values` array arrives as a JSON object the walk
cannot descend into. Writing on its own is in fact correct — `Write` delegates through options that
keep the handler — but a document this library emitted with `$id`/`$ref`/`$values` is one it would then
reject on read, so writing is refused too rather than letting the library produce input it cannot
consume. Modelling `$id`/`$ref`/`$values` correctly is future work; until then the error is loud rather
than wrong.

**Not compatible with trimming or ahead-of-time compilation.** The factory is annotated with
`[RequiresUnreferencedCode]` and `[RequiresDynamicCode]`; constructing it in a trimmed or AOT-published
application produces a build warning.

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
