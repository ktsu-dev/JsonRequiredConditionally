# `[JsonRequiredAndNotEmpty]` — Design

**Date:** 2026-08-25
**Status:** Approved, pending implementation plan

## Problem

`ktsu.JsonRequiredConditionally` today answers exactly one question: was this JSON property
physically present in the payload, given what a sibling member holds. That is deliberately a
*presence* question, and the README states the semantics as "presence, not non-nullness".

A second question keeps arising in consuming models: the property was present, but what it carried
was nothing. An empty array, an empty object, an empty string. `System.Text.Json` has no attribute
that inspects a value at all. The complete set of `JsonAttribute` subclasses through .NET 11 preview
is Constructor, Converter, DerivedType, ExtensionData, Ignore, Include, NamingPolicy, NumberHandling,
ObjectCreationHandling, Polymorphic, PropertyName, PropertyOrder, Required, Serializable,
SourceGenerationOptions and UnmappedMemberHandling. `[JsonRequired]` is presence-only, and its own
documentation is explicit that "a `null` token in JSON will not trigger a validation error".

### Why not DataAnnotations

`System.ComponentModel.DataAnnotations` looks like the obvious home. `[MinLength(1)]` applies to
collections and strings and expresses emptiness directly. Emptiness is also answerable from the
materialized object alone, without the payload, which is the opposite of the presence question this
library was built for. So the burden is on this design to justify not using it.

Three blockers were measured, not assumed, against a probe modeled on
`TheThreeThousands.Launcher.ApplicationHelper`:

```
--- Model (all empty): valid=False
    PublicList: ... minimum length of '1'.
    PublicString: The PublicString field is required.
--- Holder (child is invalid): valid=True
```

1. **Non-public members are invisible.** `Validator.TryValidateObject` reads properties through
   `TypeDescriptor`, which yields public properties only. Identical `[Required, MinLength(1)]`
   annotations on an `internal` and a `private` property produced no results at all. Consuming
   models in this ecosystem routinely mark members `private` or `internal` with `[JsonInclude]`,
   so DataAnnotations would validate almost none of them.
2. **No recursion.** A holder containing an invalid child was reported valid. Validating a
   `Collection<T>` member means hand-writing the walk and the error paths that `GraphValidator`
   already produces as `Applications[3].Categories`.
3. **`[Required]` is non-null, not non-empty, for anything that is not a `string`.** A record
   wrapping `""` passed, because `RequiredAttribute` only special-cases `System.String` itself.
   Semantic string types are records, so they pass while holding nothing.

There is also no `[RequiredIf]` in DataAnnotations. Subclassing `ValidationAttribute` and reading
siblings off `ValidationContext.ObjectInstance` is possible, but that reconstructs this library
without the one capability it uniquely has.

The conclusion is that the check belongs here, justified by non-public member support, graph
recursion and dotted path reporting, and explicitly **not** by payload access. This widens the
package's scope from conditional presence to declarative requirement and emptiness validation over
the System.Text.Json contract model. That widening is intentional and must be stated in the README.

## Scope

In scope:

- A `[JsonRequiredAndNotEmpty]` attribute meaning present **and** non-empty.
- Emptiness evaluated against the payload element.
- A second violation category on the existing public exception.

Explicitly out of scope:

- A conditional variant (`[JsonRequiredAndNotEmptyIfSiblingIs]`). Addable later without disturbing
  this attribute, and not yet needed.
- Validation on serialization. `Write` stays plain delegation, matching the existing attribute.
- Any notion of emptiness beyond the table below. No minimum counts, no predicates.

## Semantics

`[JsonRequiredAndNotEmpty]` targets properties and fields, with `AllowMultiple = false` and
`Inherited = true`, matching `JsonRequiredIfSiblingIsAttribute`'s targeting. `AllowMultiple` is
`false` because, unlike the conditional attribute, the rule carries no arguments to vary.

A decorated member is satisfied when its JSON property is present and its value is non-empty:

| Payload | Result |
|---|---|
| property absent | violation, reported as **missing** |
| `null` | violation, reported as **empty** |
| `""` | violation, reported as **empty** |
| `[]` | violation, reported as **empty** |
| `{}` | violation, reported as **empty** |
| `"   "` | satisfied |
| `"x"`, `[1]`, `{"a":1}` | satisfied |
| number, `true`, `false` | never empty, rejected at compile time |

Whitespace-only strings are **not** empty. This follows Microsoft's own definition: a string is empty
if it is explicitly assigned `""` or `String.Empty`, and an empty string has a `Length` of 0. It
diverges from DataAnnotations' `[Required]`, which treats whitespace as absent, and that divergence is
deliberate and documented.

`null` counts as empty rather than as present. This is the one place the new attribute departs from
`[JsonRequired]`'s stated semantics, and it is the entire point of the attribute: a member that is
required and not empty is not satisfied by an explicit null.

An absent member is reported as missing rather than empty, so `MissingProperties` keeps meaning
exactly what it means today.

### Combining with other attributes

Rules of different kinds compile and evaluate independently. A member may carry any combination.

`[JsonRequired]` plus `[JsonRequiredAndNotEmpty]` is the expected pairing for a collection that must
be supplied and must have contents. System.Text.Json enforces presence itself and throws its own
`JsonException` before the walk runs, so on an absent property the caller sees STJ's exception rather
than `JsonRequiredConditionallyException`. On a present but empty property, STJ is satisfied and the
walk reports it in `EmptyProperties`. This asymmetry is worth documenting in the README, because the
exception type a caller catches depends on which of the two failed.

`[JsonRequiredIfSiblingIs]` plus `[JsonRequiredAndNotEmpty]` on the same member produces two
independent rules. The conditional rule can fire only on absence, and the new rule fires on absence or
emptiness, so an absent member under a satisfied sibling condition appears once in
`MissingProperties`, not twice. Deduplication is therefore required when appending to the missing
list.

## The payload element as the source of truth

Emptiness is judged from the `JsonElement` the member was materialized from, not from the
materialized CLR value.

The alternative would be type-testing the value: `string.Length`, `ICollection.Count`,
`ICollection<T>.Count`, array length, `IEnumerable.MoveNext`. Three reasons against it.

**It works through custom converters.** `GraphValidator` already documents that it cannot see beneath
a type carrying its own converter, because System.Text.Json leaves `JsonTypeInfo.Properties` empty
there. In consuming models that describes a large share of members, including every semantic string
type and everything behind a round-trip string converter. Value-based checking cannot even attempt
those, as the measured `[Required]`-passes-on-a-record-wrapping-`""` result shows. The element has the
string sitting right there.

**It gives one definition of empty, evaluated one way.** Value-based checking needs branches across
`string`, `ICollection`, `ICollection<T>`, arrays and bare `IEnumerable`. A hybrid that prefers the
value and falls back to the element would answer differently depending on which branch fired, which
is precisely the "silently right in specific documented ways" failure the existing code comments warn
about.

**It enumerates nothing.** No lazy sequence is consumed and nothing is allocated.

The cost, stated plainly: a converter that maps a non-empty JSON representation onto an empty
collection, such as `"none"` becoming `[]`, will be judged non-empty. That divergence is the price of
seeing through converters at all, and it is accepted.

## Components

### `JsonRequiredAndNotEmptyAttribute.cs` (new, public)

Parameterless attribute. Carries no state. Documents the emptiness table above in its remarks.

### `EmptinessInspector.cs` (new, internal)

Static, one method:

```csharp
internal static bool IsEmpty(JsonElement element)
```

Returns true for `Null`, for `String` with `Length == 0`, for `Array` with `GetArrayLength() == 0`,
and for `Object` with no properties. False for everything else, including `Undefined`, numbers and
booleans. Unit-testable standalone, in the same shape as `PresenceScanner`.

Absence is not its concern. The caller distinguishes absent from present-and-empty, because the two
land in different violation categories.

### `RequirementRule.cs` (modified)

Gains a `NonEmptyRule(string jsonName, string memberName)` type alongside `RequirementRule`.

A separate type rather than a mode flag on `RequirementRule`, because the existing rule is
sibling-conditional and evaluates against a materialized instance, while this one is unconditional and
evaluates against a `JsonElement`. A single type with a discriminator would leave half its state unused
on every instance and half its evaluation path unreachable per mode.

`NonEmptyRule` is pure data. It has no `IsSatisfiedBy`, because satisfaction depends on the element,
which the rule does not hold. `GraphValidator` applies `EmptinessInspector` to the element it already
has.

### `RequirementRuleCompiler.cs` (modified)

- A second cache, `ConditionalWeakTable<JsonSerializerOptions, ConcurrentDictionary<Type, NonEmptyRule[]>>`,
  mirroring the existing `RuleCache` exactly, including its `IDE0028` suppression for netstandard2.0.
- `CompileNonEmpty(Type, JsonSerializerOptions)` and `GetNonEmptyRules(Type, JsonSerializerOptions)`,
  alongside `Compile` and `GetRules`. Both walk `JsonTypeInfo.Properties` the same way `Compile` does,
  so `[JsonIgnore]`, `IncludeFields`, `[JsonInclude]` on non-public members and naming policies behave
  identically for both rule kinds.
- `HasDirectlyDecoratedMember` also probes for `JsonRequiredAndNotEmptyAttribute`, so a type carrying
  only the new attribute is claimed. Reachability comes free, because `EnumerateReachableMemberTypes`
  routes through `HasDirectlyDecoratedMember`.
- `EligibilityCache` stays keyed on `Type` alone, unchanged.

Eligibility keeps the existing deliberate asymmetry: `HasDirectlyDecoratedMember` probes with
`IncludeFields = true` so a decorated plain field claims its type, while rule compilation runs against
the caller's real options. With `IncludeFields = false`, no rule is produced and nothing is enforced.

### `GraphValidator.cs` (modified)

`Walk` gains a second loop after the existing rule loop, inside the same `HasRules(type)` guard:

```
foreach (NonEmptyRule rule in RequirementRuleCompiler.GetNonEmptyRules(type, plainOptions))
{
    if (!TryGetProperty(element, rule.JsonName, comparer, userOptions.PropertyNameCaseInsensitive, out JsonElement child))
    {
        missing.Add(Combine(path, rule.JsonName));
    }
    else if (EmptinessInspector.IsEmpty(child))
    {
        empty.Add(Combine(path, rule.JsonName));
    }
}
```

`Validate` threads a second `List<string>` alongside `missing` and throws when either is non-empty.
`TryGetProperty` is reused rather than `PresenceScanner`, because the value is needed, not just the
name. Case sensitivity and naming policy resolution are unchanged, driven by `userOptions` as
documented on `Walk`.

Path formatting is inherited unchanged, so an empty member inside a sequence reports as
`Children[1].Tuning` and inside a dictionary as `Lookup.a.Tuning`.

### `JsonRequiredConditionallyException.cs` (modified)

Additive only:

- New `IReadOnlyList<string> EmptyProperties { get; }`, defaulting to `[]` on every existing
  constructor.
- New constructor `JsonRequiredConditionallyException(IReadOnlyList<string> missingProperties,
  IReadOnlyList<string> emptyProperties)`. Both lists are copied, for the reason already documented
  on the single-list constructor.
- `BuildMessage` widens to name both categories. When only one category is populated, the message is
  worded for that category alone. The existing single-list constructor's message text does not change,
  so existing assertions on it keep passing.

`MissingProperties` keeps its current meaning and its current message wording. That existing tests
pass untouched is itself the check that the semantics did not drift.

### `SerializerFeatureGuard.cs` (unchanged)

The containment analysis is unaffected. The new attribute introduces no new way for a serializer
feature to make the graph mean something the walk cannot see. `ReferenceHandler`,
`JsonObjectCreationHandling.Populate` and polymorphism are handled exactly as they are today, and a
type claimed only because of the new attribute routes through the same guard.

## Compile-time rejection

A member whose JSON form can never be empty is a mistake, not a rule that should quietly never fire.
`CompileNonEmpty` throws `InvalidOperationException` in that case, mirroring `EnsureValueCanEverMatch`
and its message shape:

```
[JsonRequiredAndNotEmptyAttribute] on member 'Count' of type 'Config' decorates a member of type
'Int32', whose JSON representation can never be empty.
```

Deliberately narrow. It fires only when both hold:

1. The member's declared type is non-nullable and is `bool`, an enum, or one of the built-in numeric
   types (`byte`, `sbyte`, `short`, `ushort`, `int`, `uint`, `long`, `ulong`, `nint`, `nuint`,
   `float`, `double`, `decimal`).

   `Nullable<T>` of any of those is explicitly **not** rejected. A `null` payload is empty per the
   semantics table, so `[JsonRequiredAndNotEmpty]` on an `int?` is meaningful: it rejects an explicit
   null. Only the non-nullable form can never be empty.
2. `options.GetConverter(type)` returns a converter declared in the `System.Text.Json` assembly.

The second condition is what keeps a `JsonStringEnumConverter`-backed enum, or any custom converter
that changes a type's JSON shape, out of the rejection. False negatives are accepted. False positives
are not, because a false positive breaks a model that would otherwise work.

This is the least valuable component in the design and the first thing to cut if it proves fiddly in
implementation. Cutting it changes no other behavior.

## Testing

New files:

- `NotEmptyTests.cs` crossing every payload shape in the semantics table against every member shape:
  `string`, a custom-converted string-like type, `List<T>`, `HashSet<T>`, `T[]`, and
  `Dictionary<string, T>`.
- `EmptinessInspectorTests.cs` for `EmptinessInspector.IsEmpty` in isolation, including `Undefined`.

Additions to existing files:

- `EligibilityTests.cs`: a type carrying only `[JsonRequiredAndNotEmpty]` is claimed, including when
  it is reachable only through a nested collection such as `List<List<T>>`.
- `NestingTests.cs`: path formatting for an empty member at `Children[1].X` and inside a dictionary.
- `ExceptionTests.cs`: `EmptyProperties`, the two-list constructor, both-categories message wording,
  and the unchanged single-list message.
- `SemanticsTests.cs`: `null` is empty, whitespace is not empty, absent lands in `MissingProperties`
  and not `EmptyProperties`.
- `RequirementRuleCompilerTests.cs`: the rejection fires for `int` and for a built-in enum, and does
  not fire for a `JsonStringEnumConverter`-backed enum or for `int?`.
- `ConverterTests.cs`: a member carrying both `[JsonRequiredIfSiblingIs]` and
  `[JsonRequiredAndNotEmpty]`, absent under a satisfied sibling condition, appears exactly once in
  `MissingProperties`.
- `NamingTests.cs`: the new rule respects naming policies and `[JsonPropertyName]`, since it resolves
  JSON names through the same contract model.

All tests run across the existing `net10.0;net9.0;net8.0;net7.0` matrix. The `RuntimeFrameworkVersion`
and MSTest.Sdk pinning constraints recorded in `CLAUDE.md` are untouched.

Every existing test must pass unmodified. Any change required to an existing test is a signal that
`MissingProperties` semantics moved, and is a design failure rather than a test to update.

## Documentation

- `README.md`: the new attribute, the emptiness table, the whitespace divergence from
  DataAnnotations, and a section on why `[MinLength(1)]` is not the answer, citing the three measured
  blockers.
- `CLAUDE.md`: the source organization table gains `JsonRequiredAndNotEmptyAttribute.cs` and
  `EmptinessInspector.cs`, `RequirementRule.cs` and `RequirementRuleCompiler.cs` entries widen to two
  rule kinds, and the "Presence, not non-nullness" key pattern gains the new attribute as its stated
  exception. The test structure list gains the two new files.
- `DESCRIPTION.md` and `TAGS.md`: scope widens from conditional presence to declarative requirement
  and emptiness validation.

## Versioning

`[minor]`. The change is purely additive: one new public attribute, one new public property and one
new public constructor on an existing exception. No existing public member changes meaning or
signature.
