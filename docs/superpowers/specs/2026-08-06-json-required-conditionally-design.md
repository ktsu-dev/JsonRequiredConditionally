# ktsu.JsonRequiredConditionally — Design

**Date:** 2026-08-06
**Status:** Approved, pending implementation plan

## Problem

`System.Text.Json`'s `[JsonRequired]` marks a property as unconditionally mandatory. There is no
built-in way to express *conditional* requirement — "this property is mandatory only when a sibling
property has a particular value".

STJ cannot be extended to do this in place. `[JsonRequired]` maps to `JsonPropertyInfo.IsRequired`, a
`bool` baked into the type's contract when metadata is built, before any JSON is read. A condition that
depends on sibling values cannot be evaluated at that point, because the object does not exist yet.

Enforcement therefore has to happen after the object is materialized, while still knowing which
properties were physically present in the payload. A buffering `JsonConverter` is the mechanism that
provides both.

## Scope

In scope:

- A `[JsonRequiredIfSiblingIs(nameof(Sibling), value)]` attribute.
- A converter factory that enforces it during deserialization.
- A dedicated exception carrying every violation found.

Explicitly out of scope:

- A standalone public validator for already-constructed objects. Enforcement is converter-only.
- Validation on serialization. `Write` is plain delegation.
- Conditions beyond sibling equality. Richer predicates would be a separate attribute
  (e.g. `[JsonRequiredIfSiblingMatches(nameof(Predicate))]`) added later without breaking this one.

## Package

New repository `ktsu-dev/JsonRequiredConditionally`, publishing `ktsu.JsonRequiredConditionally`.

It does not go in `ktsu.Extensions`. Extensions is a dependency-free library of BCL extension methods
consumed broadly across the ecosystem; pulling `System.Text.Json` into it would affect every consumer
for a feature most of them do not use.

Target frameworks mirror Extensions:

```
net10.0;net9.0;net8.0;net7.0;net6.0;net5.0;netstandard2.0;netstandard2.1
```

`System.Text.Json` ships in-box from `net5.0` onward, so the `PackageReference` is conditional on
`netstandard2.0;netstandard2.1` only. `Polyfill` is referenced per ktsu convention, and `Ensure.NotNull`
is used for parameter validation.

## Public surface

Three public types.

```csharp
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field,
	AllowMultiple = true, Inherited = true)]
public sealed class JsonRequiredIfSiblingIsAttribute(string siblingName, object? value) : Attribute
{
	public string SiblingName { get; } = siblingName;
	public object? Value { get; } = value;
}

public sealed class JsonRequiredConditionallyConverterFactory : JsonConverterFactory;

public sealed class JsonRequiredConditionallyException : JsonException
{
	public IReadOnlyList<string> MissingProperties { get; }
}
```

Attribute arguments must be compile-time constants, which rules out lambdas. An `object?`-typed
parameter accepts enum values (boxed) — the same mechanism `[DefaultValue(MyEnum.Foo)]` has always
relied on — so the intended call shape compiles directly:

```csharp
[JsonRequiredIfSiblingIs(nameof(Kind), Kind.Advanced)]
public string? Tuning { get; set; }
```

`JsonRequiredConditionallyException` derives from `JsonException` so STJ decorates it with path
information and so existing `catch (JsonException)` handlers keep working.

Registration is once per options instance and applies to every type:

```csharp
var options = new JsonSerializerOptions();
options.Converters.Add(new JsonRequiredConditionallyConverterFactory());
```

## Semantics

### What counts as satisfied

Presence in the payload, not non-nullness. A property that is required and physically present
satisfies the rule even if its value is `null`.

```jsonc
{ "Kind": "Advanced" }                  // Tuning absent  -> throws
{ "Kind": "Advanced", "Tuning": null }  // Tuning present -> passes
```

This is deliberate: it mirrors how `[JsonRequired]` itself behaves, and it is the only semantic a
converter can offer that a post-hoc validator cannot.

### Multiple attributes

Attributes group implicitly by sibling name. Within a group the values are OR-ed; across groups the
groups are AND-ed.

```csharp
[JsonRequiredIfSiblingIs(nameof(Kind), Kind.Advanced)]
[JsonRequiredIfSiblingIs(nameof(Kind), Kind.Expert)]
[JsonRequiredIfSiblingIs(nameof(Mode), Mode.Remote)]
public string? Tuning { get; set; }
// required when (Kind is Advanced OR Expert) AND (Mode is Remote)
```

OR within a group is what makes the common case — one enum sibling with several qualifying values —
expressible at all; AND-ing those would be unsatisfiable, since `Kind` cannot be both `Advanced` and
`Expert`. AND across groups covers multi-sibling conjunctions. Together they express sum-of-products
conditions with no explicit `Group` parameter.

### Comparison

`Equals`, with one normalization: if either side is an enum, both are converted to the enum's
underlying type before comparing. Without this, `[JsonRequiredIfSiblingIs(nameof(Kind), 2)]` against a
`Kind`-typed sibling boxes an `int` and silently never matches. Strings compare ordinal. A `null`
argument matches a null sibling.

## Architecture

### Type selection

`CanConvert(Type)` returns `true` only for object-like types (not primitives, enums, strings,
collections, or dictionaries) carrying at least one member decorated with the attribute. The result is
cached in a `ConcurrentDictionary<Type, bool>`.

Types that do not opt in never enter the converter and keep STJ's normal fast path, so the buffering
cost is confined to types that actually use the feature.

### Read

The converter has two obligations: materialize `T` without re-entering itself, and know which
properties were physically present. Both come out of one pass over the reader.

1. **Scan.** `Utf8JsonReader` is a struct, so it is copied and walked forward, collecting the object's
   immediate property names into a `HashSet<string>` and `Skip()`ping nested values. STJ guarantees the
   complete value is buffered before a custom converter is invoked, so both the copy and the `Skip` are
   safe. No `JsonDocument` is allocated and the subtree is not materialized twice.
2. **Materialize.** Deserialize from the untouched original reader using a cached inner
   `JsonSerializerOptions`.
3. **Evaluate.** Apply the type's compiled rules against the materialized instance and the presence set.
4. **Throw** a single aggregated exception if any rule is violated.

`Read` returns `default` for a `JsonTokenType.Null` token without evaluating rules.

### Re-entrancy

The inner options is a clone of the incoming options with the factory replaced by one that excludes
**only the type currently being converted**. Each frame resets the exclusion to its own type rather
than accumulating.

This matters for correctness. Removing the factory outright would silently disable validation for every
nested type. Accumulating exclusions would disable it for cyclic type graphs — in `T → U → T`, the
inner `T` would go unchecked. Resetting per frame keeps every level validated, and terminates because
recursion is bounded by the JSON's nesting depth, which STJ already caps via `MaxDepth`.

Inner options are cached per converter instance. STJ caches converters per `(type, options)` pair, so
this yields one clone per pair rather than one per deserialization — important, because a fresh
`JsonSerializerOptions` carries no metadata cache and would be severely slow.

### Name resolution

Two name spaces that must not be conflated:

- **The decorated property's presence** is checked against its *JSON* name: `[JsonPropertyName]` if
  present, otherwise `options.PropertyNamingPolicy?.ConvertName(clrName) ?? clrName`. Lookup honors
  `options.PropertyNameCaseInsensitive`.
- **The sibling's value** is read from the *materialized object* by CLR member name via reflection, so
  `nameof` works directly and no JSON-name mapping is involved.

Reading sibling values post-materialization rather than from the payload also means records and
constructor-parameterized types work unchanged, since STJ has already run the constructor by then.

### Rule compilation

Rules compile once per type and are cached: for each decorated member, its resolved JSON name plus its
attributes grouped by sibling name. Evaluation is a lookup and a comparison per group.

### Errors

All violations for an object accumulate into one `JsonRequiredConditionallyException` listing every
missing property, rather than failing on the first. `MissingProperties` exposes the list for callers
that need to act on it programmatically.

### Write

Delegates to the inner options unchanged. Serialization is not validated.

## Testing

MSTest, semantic asserts, per ktsu convention.

- Absent property throws; explicitly `null` property passes.
- OR within one sibling: each qualifying value triggers the requirement, non-qualifying does not.
- AND across siblings: all groups must match.
- Naming: `PropertyNamingPolicy.CamelCase`, `[JsonPropertyName]` override, and
  `PropertyNameCaseInsensitive`.
- Enum normalization: boxed `int` argument matches the equivalent enum sibling.
- `null` attribute argument matches a null sibling.
- Nested objects are validated, including inside collections and dictionaries.
- Records and constructor-parameterized types.
- Cyclic type graphs (`T → U → T`) validate at every level.
- Multiple violations aggregate into one exception.
- A type with no decorated members bypasses the converter entirely.
- `null` JSON token deserializes to `null` without evaluating rules.

## Open items

None. Ready for an implementation plan.
