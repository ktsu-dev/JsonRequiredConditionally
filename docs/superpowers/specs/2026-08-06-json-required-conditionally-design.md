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

### Unresolvable sibling names

If `SiblingName` does not resolve to a readable property or field on the declaring type, rule
compilation throws `InvalidOperationException` naming the type and the unresolved member. This is a
coding error, not a data error, so it fails loudly at first use of the type rather than being treated
as a non-matching condition — silently degrading to "never required" would turn a typo into a rule
that quietly never fires.

### Absent siblings

Sibling values are read from the materialized object, so a sibling that was itself absent from the
payload reads as its CLR default. An enum sibling whose zero value is a meaningful case is therefore
matched by an absent sibling:

```csharp
[JsonRequiredIfSiblingIs(nameof(Kind), Kind.Basic)]  // Basic == 0
public string? Name { get; set; }
// {} -> Kind defaults to Basic, so Name is required
```

This is accepted rather than worked around. It follows directly from evaluating against the object
instead of the payload, which is what makes records and constructor-parameterized types work. Callers
who need to distinguish "absent" from "defaulted" should mark the sibling `[JsonRequired]` or make it
nullable. Covered by a test so the behavior is pinned.

## Architecture

### Type selection

`CanConvert(Type)` returns `true` only for object-like types (not primitives, enums, strings,
collections, or dictionaries) carrying at least one member decorated with the attribute. The result is
cached in a `ConcurrentDictionary<Type, bool>`.

Types that do not opt in never enter the converter and keep STJ's normal fast path, so the buffering
cost is confined to types that actually use the feature.

### Read

1. **Buffer.** `JsonDocument.ParseValue` captures the converter's whole subtree.
2. **Materialize.** Deserialize the buffered element through a cached clone of the options with this
   library's factory removed outright.
3. **Evaluate.** Walk the materialized graph alongside the JSON, applying rules at every level.
4. **Throw** a single aggregated exception if any rule is violated.

`Read` returns `default` for a `JsonTokenType.Null` token without evaluating rules.

### Why the converter validates the whole subtree itself

An earlier version of this design had the converter validate only its own object and rely on STJ
re-entering it for nested types, using an inner options that excluded **only the type currently being
converted**. That is wrong, and the reason is worth recording.

STJ caches converter resolution per type *within* an options instance. Excluding `T` from the inner
options therefore excludes **every** nested occurrence of `T` in that materialization pass, not just
the instance being unwrapped. A directly self-referential decorated type — `TreeNode` with a
`TreeNode? Child` — was validated only at the outermost level, silently. `T → U → T` worked only
because `U` got its own cache entry that re-admitted `T`, which is why the original tests missed it.

No arrangement of `JsonSerializerOptions` fixes this: converter selection is per-type, not
per-instance, so "deserialize *this* object without the converter but nested same-type objects with
it" is inexpressible. Hence the converter owns the whole subtree.

Termination is guaranteed because the walk is driven by the JSON element tree, which is finite — not
by the object graph, which may be cyclic.

The cost is real and accepted: a `JsonDocument` per decorated subtree, and the walk must mirror STJ's
name mapping and collection traversal rather than delegating to it.

The factory-free clone is cached per options instance, because a fresh `JsonSerializerOptions` carries
no metadata cache and would be severely slow to rebuild per deserialization.

### Graph traversal

The walk pairs each JSON element with the object STJ materialized from it:

- **Objects** recurse member by member, mapping CLR members to JSON properties by resolved JSON name.
  Members carrying `[JsonIgnore]` are skipped, and members are de-duplicated by resolved JSON name so
  a `new`-hidden base member cannot cause the same element to be descended twice.
- **Arrays** zip the JSON items against the materialized sequence in order.
- **Dictionaries** match JSON property names against `entry.Key.ToString()`, so non-string-keyed
  dictionaries descend correctly and no key cast is attempted.
- **Scalars** terminate the descent.

Indexed properties are never read — `PropertyInfo.GetValue` throws on them, and they are reachable in
ordinary input because STJ materializes `object`-typed members as `JsonElement`.

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

All violations across the whole subtree accumulate into one `JsonRequiredConditionallyException`,
rather than failing on the first.

`MissingProperties` holds **paths**, not bare property names: `Tuning` at the root, `Child.Tuning`
nested, `Children[1].Tuning` through an array, `Lookup.a.Tuning` through a dictionary. Bare names
became ambiguous once violations from every depth landed in one list — a two-level failure of the same
property read as `'Tuning', 'Tuning'` with no way to tell the nodes apart.

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
- Nested objects are validated, including as elements of collections and dictionary values.
- Directly self-referential types (`TreeNode` with a `TreeNode? Child`) validate at every depth,
  including through a collection of themselves. This is the case the original re-entrancy design
  silently skipped.
- Violations at different depths aggregate into one exception, each carrying its own path.
- Non-string-keyed dictionaries descend correctly.
- A decorated type with an `object`-typed member deserializes without throwing when that member holds
  a JSON object.
- A `[JsonIgnore]`d member whose resolved name collides with a real JSON property is not descended into.
- Nested validation still fires under `PropertyNameCaseInsensitive` with differently-cased JSON.
- An unresolvable `SiblingName` throws `InvalidOperationException` on first use of the type.
- An absent sibling reads as its CLR default, and a zero-valued enum condition matches it.
- Records and constructor-parameterized types.
- Cyclic type graphs (`T → U → T`) validate at every level.
- Multiple violations aggregate into one exception.
- A type with no decorated members bypasses the converter entirely.
- `null` JSON token deserializes to `null` without evaluating rules.

## Open items

None. Ready for an implementation plan.
