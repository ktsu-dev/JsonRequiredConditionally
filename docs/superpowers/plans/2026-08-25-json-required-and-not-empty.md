# `[JsonRequiredAndNotEmpty]` Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a `[JsonRequiredAndNotEmpty]` attribute to `ktsu.JsonRequiredConditionally` that requires a member to be both present in the JSON payload and non-empty.

**Architecture:** Emptiness is judged from the `JsonElement` the member was materialized from, never from the materialized CLR value, so the check sees through custom converters that `JsonTypeInfo.Properties` cannot describe. A new unconditional `NonEmptyRule` compiles alongside the existing sibling-conditional `RequirementRule`, both driven by System.Text.Json's own contract model, and `GraphValidator` evaluates both in one walk. Violations split into two categories on the existing public exception.

**Tech Stack:** C# on .NET, System.Text.Json, ktsu.Sdk, MSTest.

**Spec:** `docs/superpowers/specs/2026-08-25-json-required-and-not-empty-design.md`

## Global Constraints

- **Indentation is tabs**, not spaces. Line endings CRLF.
- **File-scoped namespaces** (`namespace ktsu.JsonRequiredConditionally;`), `using` directives **inside** the namespace.
- Every new file starts with `// Copyright (c) 2023-2026 ktsu-dev contributors` followed by a blank line.
- Library namespace `ktsu.JsonRequiredConditionally`. Test namespace `ktsu.JsonRequiredConditionally.Tests`.
- **No `this.` qualifiers.** Always specify accessibility modifiers. Always brace control flow.
- **Warnings are errors.** Nullable reference types are enabled.
- **No conditional compilation.** No `#if NET8_0_OR_GREATER`. The library targets `net10.0;net9.0;net8.0;net7.0;netstandard2.0;netstandard2.1` from one source, so every API used must exist on all six. `JsonElement.ValueEquals`, `JsonElement.GetArrayLength` and `JsonElement.EnumerateObject` all do.
- **No global warning suppressions.** Targeted `[SuppressMessage]` with a justification only.
- Tests use **semantic asserts** (`Assert.IsEmpty`, `Assert.HasCount`, `Assert.ThrowsExactly`, `CollectionAssert.AreEqual`), never `Assert.IsTrue`/`Assert.IsFalse` on a computed boolean where a semantic assert exists.
- Tests multi-target `net10.0;net9.0;net8.0;net7.0`. A single `dotnet test --filter` run therefore executes each test four times, once per framework. All four legs must pass.
- **Every existing test must pass unmodified.** If a task requires editing an existing test's expectations, stop: that means `MissingProperties` semantics moved, which is a design failure rather than a test to update.
- The feature is a **`[minor]`** version bump. The marker goes on the final commit (Task 8).

---

## File Structure

**Created:**

| File | Responsibility |
|---|---|
| `JsonRequiredConditionally/JsonRequiredAndNotEmptyAttribute.cs` | The public attribute. Parameterless, carries no state. |
| `JsonRequiredConditionally/EmptinessInspector.cs` | Internal static. One method answering "is this `JsonElement` empty". |
| `JsonRequiredConditionally.Test/EmptinessInspectorTests.cs` | Unit tests for the inspector in isolation. |
| `JsonRequiredConditionally.Test/NotEmptyTests.cs` | End-to-end: every payload shape against every member shape. |

**Modified:**

| File | Change |
|---|---|
| `JsonRequiredConditionally/RequirementRule.cs` | Add the `NonEmptyRule` type alongside `SiblingCondition` and `RequirementRule`. |
| `JsonRequiredConditionally/RequirementRuleCompiler.cs` | Add `NonEmptyRuleCache`, `CompileNonEmpty`, `GetNonEmptyRules`. Widen `HasDirectlyDecoratedMember`. |
| `JsonRequiredConditionally/GraphValidator.cs` | Add `ViolationCollector`, thread it through the walk, add the second rule loop. |
| `JsonRequiredConditionally/JsonRequiredConditionallyException.cs` | Add `EmptyProperties`, a two-list constructor, a `BuildMessage` overload. |
| `JsonRequiredConditionally.Test/TestModels.cs` | Add the models the new tests deserialize. |
| `JsonRequiredConditionally.Test/AttributeTests.cs` | Attribute metadata tests. |
| `JsonRequiredConditionally.Test/RequirementRuleCompilerTests.cs` | Rule compilation tests. |
| `JsonRequiredConditionally.Test/EligibilityTests.cs` | Claiming tests. |
| `JsonRequiredConditionally.Test/ExceptionTests.cs` | Two-category exception tests. |
| `JsonRequiredConditionally.Test/NestingTests.cs` | Path formatting for empty members. |
| `JsonRequiredConditionally.Test/NamingTests.cs` | Naming policy and `[JsonPropertyName]`. |
| `JsonRequiredConditionally.Test/ConverterTests.cs` | Self-sufficiency and deduplication. |
| `README.md`, `CLAUDE.md`, `DESCRIPTION.md`, `TAGS.md` | Documentation. |

**One implementation decision not in the spec.** `GraphValidator.Walk` and its three `Descend*` helpers currently take a `List<string> missing` parameter and already carry seven parameters each. Rather than adding an eighth to every one of them, this plan replaces that single parameter with a `ViolationCollector` holding both lists. Parameter counts stay as they are, and the two categories travel together. `ViolationCollector` is internal and lives in `GraphValidator.cs`, following the precedent of `RequirementRule.cs` holding two types in one file.

---

### Task 1: `EmptinessInspector`

Self-contained. No dependency on any other task.

**Files:**
- Create: `JsonRequiredConditionally/EmptinessInspector.cs`
- Test: `JsonRequiredConditionally.Test/EmptinessInspectorTests.cs`

**Interfaces:**
- Consumes: nothing.
- Produces: `internal static bool EmptinessInspector.IsEmpty(JsonElement element)`. Returns `true` for JSON `null`, a zero-length string, a zero-element array, and an object with no properties. Returns `false` for everything else, including `JsonValueKind.Undefined`, numbers and booleans. It does **not** answer absence: the caller distinguishes absent from present-and-empty because they land in different violation categories.

- [ ] **Step 1: Write the failing test**

Create `JsonRequiredConditionally.Test/EmptinessInspectorTests.cs`:

```csharp
// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.JsonRequiredConditionally.Tests;

using System.Text.Json;

[TestClass]
public class EmptinessInspectorTests
{
	private static bool IsEmpty(string json)
	{
		using JsonDocument document = JsonDocument.Parse(json);

		return EmptinessInspector.IsEmpty(document.RootElement);
	}

	[TestMethod]
	public void NullIsEmpty()
	{
		Assert.IsTrue(IsEmpty(/*lang=json,strict*/ "null"));
	}

	[TestMethod]
	public void ZeroLengthStringIsEmpty()
	{
		Assert.IsTrue(IsEmpty(/*lang=json,strict*/ "\"\""));
	}

	[TestMethod]
	public void WhitespaceStringIsNotEmpty()
	{
		Assert.IsFalse(IsEmpty(/*lang=json,strict*/ "\"   \""));
		Assert.IsFalse(IsEmpty(/*lang=json,strict*/ "\"\\t\\n\""));
	}

	[TestMethod]
	public void PopulatedStringIsNotEmpty()
	{
		Assert.IsFalse(IsEmpty(/*lang=json,strict*/ "\"x\""));
	}

	[TestMethod]
	public void EmptyArrayIsEmpty()
	{
		Assert.IsTrue(IsEmpty(/*lang=json,strict*/ "[]"));
	}

	[TestMethod]
	public void PopulatedArrayIsNotEmpty()
	{
		Assert.IsFalse(IsEmpty(/*lang=json,strict*/ "[1]"));
	}

	[TestMethod]
	public void ArrayOfOneEmptyStringIsNotEmpty()
	{
		Assert.IsFalse(IsEmpty(/*lang=json,strict*/ "[\"\"]"));
	}

	[TestMethod]
	public void EmptyObjectIsEmpty()
	{
		Assert.IsTrue(IsEmpty(/*lang=json,strict*/ "{}"));
	}

	[TestMethod]
	public void PopulatedObjectIsNotEmpty()
	{
		Assert.IsFalse(IsEmpty(/*lang=json,strict*/ """{"a":1}"""));
	}

	[TestMethod]
	public void ObjectWithOnlyNullValueIsNotEmpty()
	{
		Assert.IsFalse(IsEmpty(/*lang=json,strict*/ """{"a":null}"""));
	}

	[TestMethod]
	public void NumbersAndBooleansAreNeverEmpty()
	{
		Assert.IsFalse(IsEmpty(/*lang=json,strict*/ "0"));
		Assert.IsFalse(IsEmpty(/*lang=json,strict*/ "false"));
		Assert.IsFalse(IsEmpty(/*lang=json,strict*/ "true"));
	}

	[TestMethod]
	public void UndefinedIsNotEmpty()
	{
		Assert.IsFalse(EmptinessInspector.IsEmpty(default));
	}
}
```

`Assert.IsTrue`/`IsFalse` are correct here rather than a smell: `IsEmpty` returns a boolean that *is* the assertion subject, so there is no semantic assert to prefer.

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test --filter "FullyQualifiedName~EmptinessInspectorTests"`
Expected: compile failure, `CS0103: The name 'EmptinessInspector' does not exist in the current context`.

- [ ] **Step 3: Write minimal implementation**

Create `JsonRequiredConditionally/EmptinessInspector.cs`:

```csharp
// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.JsonRequiredConditionally;

using System.Text.Json;

/// <summary>
/// Answers whether a JSON value carries nothing.
/// </summary>
/// <remarks>
/// Emptiness is judged from the payload element rather than from the materialized CLR value. That is
/// what lets it answer correctly for a type behind its own <see cref="System.Text.Json.Serialization.JsonConverter"/>,
/// whose <see cref="System.Text.Json.Serialization.Metadata.JsonTypeInfo.Properties"/> System.Text.Json
/// leaves empty and whose CLR shape the library therefore cannot inspect. The cost is that a converter
/// mapping a non-empty JSON representation onto an empty collection is judged non-empty.
/// </remarks>
internal static class EmptinessInspector
{
	/// <summary>
	/// Determines whether a JSON value is empty.
	/// </summary>
	/// <param name="element">The value to inspect.</param>
	/// <returns>
	/// True for null, a zero-length string, a zero-element array, and an object with no properties.
	/// False for every other value, including numbers, booleans and
	/// <see cref="JsonValueKind.Undefined"/>.
	/// </returns>
	/// <remarks>
	/// A whitespace-only string is <em>not</em> empty. This follows the framework's own definition,
	/// under which a string is empty when its length is zero, and diverges deliberately from
	/// <c>System.ComponentModel.DataAnnotations.RequiredAttribute</c>, which treats whitespace as absent.
	/// Absence is not answered here: the caller distinguishes an absent property from a present but
	/// empty one, because the two are reported in different categories.
	/// </remarks>
	internal static bool IsEmpty(JsonElement element)
	{
		switch (element.ValueKind)
		{
			case JsonValueKind.Null:
				return true;

			case JsonValueKind.String:
				// ValueEquals compares against the raw UTF-8 payload, so no string is materialized
				// just to measure its length.
				return element.ValueEquals(string.Empty);

			case JsonValueKind.Array:
				return element.GetArrayLength() == 0;

			case JsonValueKind.Object:
			{
				JsonElement.ObjectEnumerator properties = element.EnumerateObject();

				return !properties.MoveNext();
			}

			default:
				return false;
		}
	}
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test --filter "FullyQualifiedName~EmptinessInspectorTests"`
Expected: PASS, 13 tests per framework across four frameworks.

- [ ] **Step 5: Commit**

```bash
git add JsonRequiredConditionally/EmptinessInspector.cs JsonRequiredConditionally.Test/EmptinessInspectorTests.cs
git commit -m "feat: add EmptinessInspector for payload-element emptiness"
```

---

### Task 2: `JsonRequiredAndNotEmptyAttribute`

Self-contained. No dependency on Task 1.

**Files:**
- Create: `JsonRequiredConditionally/JsonRequiredAndNotEmptyAttribute.cs`
- Modify: `JsonRequiredConditionally.Test/AttributeTests.cs`

**Interfaces:**
- Consumes: nothing.
- Produces: `public sealed class JsonRequiredAndNotEmptyAttribute : Attribute`, parameterless, `[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false, Inherited = true)]`. Tasks 3 and 4 detect it with `MemberInfo.IsDefined(typeof(JsonRequiredAndNotEmptyAttribute), inherit: true)`.

- [ ] **Step 1: Write the failing test**

Append to `JsonRequiredConditionally.Test/AttributeTests.cs`, inside the existing `[TestClass]`:

```csharp
	[TestMethod]
	public void NotEmptyAttributeTargetsPropertiesAndFields()
	{
		AttributeUsageAttribute? usage = typeof(JsonRequiredAndNotEmptyAttribute)
			.GetCustomAttributes(typeof(AttributeUsageAttribute), inherit: false)
			.Cast<AttributeUsageAttribute>()
			.SingleOrDefault();

		Assert.IsNotNull(usage);
		Assert.AreEqual(AttributeTargets.Property | AttributeTargets.Field, usage.ValidOn);
	}

	[TestMethod]
	public void NotEmptyAttributeIsNotRepeatable()
	{
		AttributeUsageAttribute? usage = typeof(JsonRequiredAndNotEmptyAttribute)
			.GetCustomAttributes(typeof(AttributeUsageAttribute), inherit: false)
			.Cast<AttributeUsageAttribute>()
			.SingleOrDefault();

		Assert.IsNotNull(usage);
		Assert.IsFalse(usage.AllowMultiple);
		Assert.IsTrue(usage.Inherited);
	}

	[TestMethod]
	public void NotEmptyAttributeIsSealed()
	{
		Assert.IsTrue(typeof(JsonRequiredAndNotEmptyAttribute).IsSealed);
	}
```

If `AttributeTests.cs` does not already have `using System.Linq;` inside the namespace, add it.

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test --filter "FullyQualifiedName~AttributeTests"`
Expected: compile failure, `CS0246: The type or namespace name 'JsonRequiredAndNotEmptyAttribute' could not be found`.

- [ ] **Step 3: Write minimal implementation**

Create `JsonRequiredConditionally/JsonRequiredAndNotEmptyAttribute.cs`:

```csharp
// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.JsonRequiredConditionally;

/// <summary>
/// Marks a property or field as required during JSON deserialization and additionally requires that
/// the value it carries is not empty.
/// </summary>
/// <remarks>
/// <para>
/// The member is satisfied only when its JSON property is physically present in the payload
/// <em>and</em> its value is non-empty. Emptiness is judged from the payload:
/// </para>
/// <list type="bullet">
/// <item><description>The property being absent is a violation, reported in <see cref="JsonRequiredConditionallyException.MissingProperties"/>.</description></item>
/// <item><description><c>null</c>, <c>""</c>, <c>[]</c> and <c>{}</c> are violations, reported in <see cref="JsonRequiredConditionallyException.EmptyProperties"/>.</description></item>
/// <item><description>A whitespace-only string is <em>not</em> empty. A string is empty when its length is zero.</description></item>
/// <item><description>A number or boolean can never be empty, so decorating such a member produces a rule that is always satisfied.</description></item>
/// </list>
/// <para>
/// This attribute is self-sufficient and is meant to be used alone. Pairing it with
/// <see cref="System.Text.Json.Serialization.JsonRequiredAttribute"/> is redundant, because this
/// attribute already reports an absent property. Pairing also costs diagnostics: System.Text.Json
/// enforces its own required-property check during deserialization, strictly before this library's
/// walk runs, so an absent property raises that exception and the walk never gets to collect the
/// remaining violations in the payload.
/// </para>
/// </remarks>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false, Inherited = true)]
public sealed class JsonRequiredAndNotEmptyAttribute : Attribute
{
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test --filter "FullyQualifiedName~AttributeTests"`
Expected: PASS, including all pre-existing tests in the class.

- [ ] **Step 5: Commit**

```bash
git add JsonRequiredConditionally/JsonRequiredAndNotEmptyAttribute.cs JsonRequiredConditionally.Test/AttributeTests.cs
git commit -m "feat: add JsonRequiredAndNotEmptyAttribute"
```

---

### Task 3: `NonEmptyRule` and its compilation

Depends on Task 2.

**Files:**
- Modify: `JsonRequiredConditionally/RequirementRule.cs` (append)
- Modify: `JsonRequiredConditionally/RequirementRuleCompiler.cs`
- Modify: `JsonRequiredConditionally.Test/TestModels.cs` (append)
- Modify: `JsonRequiredConditionally.Test/RequirementRuleCompilerTests.cs` (append)

**Interfaces:**
- Consumes: `JsonRequiredAndNotEmptyAttribute` from Task 2.
- Produces:
  - `internal sealed class NonEmptyRule(string jsonName, string memberName)` with `internal string JsonName { get; }` and `internal string MemberName { get; }`. Pure data, no `IsSatisfiedBy`: satisfaction depends on the `JsonElement`, which the rule does not hold.
  - `internal static NonEmptyRule[] RequirementRuleCompiler.CompileNonEmpty(Type type, JsonSerializerOptions options)`
  - `internal static NonEmptyRule[] RequirementRuleCompiler.GetNonEmptyRules(Type type, JsonSerializerOptions options)` — cached per options instance, consumed by Task 6.

- [ ] **Step 1: Add the test models**

Append to `JsonRequiredConditionally.Test/TestModels.cs`. These models are used by Tasks 3, 4, 6 and 7, so add them all now.

```csharp
/// <summary>A decorated string member.</summary>
public sealed class NotEmptyStringConfig
{
	[JsonRequiredAndNotEmpty]
	public string? Name { get; set; }
}

/// <summary>A decorated list member.</summary>
public sealed class NotEmptyListConfig
{
	[JsonRequiredAndNotEmpty]
	public List<string>? Items { get; set; }
}

/// <summary>A decorated set member.</summary>
public sealed class NotEmptySetConfig
{
	[JsonRequiredAndNotEmpty]
	public HashSet<string>? Tags { get; set; }
}

/// <summary>A decorated array member.</summary>
public sealed class NotEmptyArrayConfig
{
	[JsonRequiredAndNotEmpty]
	public string[]? Values { get; set; }
}

/// <summary>A decorated dictionary member, which arrives as a JSON object.</summary>
public sealed class NotEmptyDictionaryConfig
{
	[JsonRequiredAndNotEmpty]
	public Dictionary<string, string>? Lookup { get; set; }
}

/// <summary>A decorated non-nullable int: present is always non-empty.</summary>
public sealed class NotEmptyIntConfig
{
	[JsonRequiredAndNotEmpty]
	public int Count { get; set; }
}

/// <summary>A decorated nullable int: an explicit null is empty.</summary>
public sealed class NotEmptyNullableIntConfig
{
	[JsonRequiredAndNotEmpty]
	public int? Count { get; set; }
}

/// <summary>A decorated member renamed in the payload.</summary>
public sealed class NotEmptyRenamedConfig
{
	[JsonPropertyName("tuning_name")]
	[JsonRequiredAndNotEmpty]
	public string? Tuning { get; set; }
}

/// <summary>Both attributes on one member, to pin deduplication of an absent path.</summary>
public sealed class NotEmptyAndConditionalConfig
{
	public Kind Kind { get; set; }

	[JsonRequiredIfSiblingIs(nameof(Kind), Kind.Advanced)]
	[JsonRequiredAndNotEmpty]
	public string? Tuning { get; set; }
}

/// <summary>A holder whose only decorated member sits one level down.</summary>
public sealed class NotEmptyHolder
{
	public NotEmptyStringConfig? Child { get; set; }
}

/// <summary>A holder whose decorated member is reachable only through a collection.</summary>
public sealed class NotEmptySequenceHolder
{
	public List<NotEmptyStringConfig>? Children { get; set; }
}

/// <summary>A holder whose decorated member is reachable only through a dictionary.</summary>
public sealed class NotEmptyDictionaryHolder
{
	public Dictionary<string, NotEmptyStringConfig>? Lookup { get; set; }
}

/// <summary>A holder whose decorated member is reachable only through nested collections.</summary>
public sealed class NotEmptyNestedSequenceHolder
{
	public List<List<NotEmptyStringConfig>>? Grid { get; set; }

	public NotEmptyStringConfig[][]? Jagged { get; set; }
}

/// <summary>A string-like type behind its own converter, whose CLR shape the walk cannot see.</summary>
public sealed record class Label(string Value);

/// <summary>Serializes <see cref="Label"/> as a bare JSON string.</summary>
public sealed class LabelJsonConverter : JsonConverter<Label>
{
	public override Label Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
		new(reader.GetString() ?? string.Empty);

	public override void Write(Utf8JsonWriter writer, Label value, JsonSerializerOptions options)
	{
		ArgumentNullException.ThrowIfNull(writer);
		ArgumentNullException.ThrowIfNull(value);

		writer.WriteStringValue(value.Value);
	}
}

/// <summary>A decorated member whose type carries its own converter.</summary>
public sealed class NotEmptyConvertedConfig
{
	[JsonConverter(typeof(LabelJsonConverter))]
	[JsonRequiredAndNotEmpty]
	public Label? Label { get; set; }
}
```

`TestModels.cs` already has `using System.Text.Json.Serialization;` inside the namespace. Add `using System.Text.Json;` for `Utf8JsonReader` and `JsonSerializerOptions` if it is not already there.

- [ ] **Step 2: Write the failing test**

Append to `JsonRequiredConditionally.Test/RequirementRuleCompilerTests.cs`, inside the existing `[TestClass]`:

```csharp
	private static JsonSerializerOptions PlainOptions() => new();

	[TestMethod]
	public void CompilesOneNonEmptyRulePerDecoratedMember()
	{
		NonEmptyRule[] rules = RequirementRuleCompiler.GetNonEmptyRules(typeof(NotEmptyStringConfig), PlainOptions());

		Assert.HasCount(1, rules);
		Assert.AreEqual("Name", rules[0].JsonName);
		Assert.AreEqual("Name", rules[0].MemberName);
	}

	[TestMethod]
	public void CompilesNoNonEmptyRulesForUndecoratedTypes()
	{
		Assert.IsEmpty(RequirementRuleCompiler.GetNonEmptyRules(typeof(PlainConfig), PlainOptions()));
	}

	[TestMethod]
	public void ConditionalAndNonEmptyRulesCompileIndependently()
	{
		JsonSerializerOptions options = PlainOptions();

		Assert.HasCount(1, RequirementRuleCompiler.GetRules(typeof(NotEmptyAndConditionalConfig), options));
		Assert.HasCount(1, RequirementRuleCompiler.GetNonEmptyRules(typeof(NotEmptyAndConditionalConfig), options));
	}

	[TestMethod]
	public void NonEmptyRuleUsesTheResolvedJsonName()
	{
		NonEmptyRule[] rules = RequirementRuleCompiler.GetNonEmptyRules(typeof(NotEmptyRenamedConfig), PlainOptions());

		Assert.HasCount(1, rules);
		Assert.AreEqual("tuning_name", rules[0].JsonName);
		Assert.AreEqual("Tuning", rules[0].MemberName);
	}

	[TestMethod]
	public void DecoratingANonNullableIntCompilesARuleAndThrowsNothing()
	{
		NonEmptyRule[] rules = RequirementRuleCompiler.GetNonEmptyRules(typeof(NotEmptyIntConfig), PlainOptions());

		Assert.HasCount(1, rules);
		Assert.AreEqual("Count", rules[0].JsonName);
	}

	[TestMethod]
	public void NonEmptyRulesAreCachedPerOptionsInstance()
	{
		JsonSerializerOptions options = PlainOptions();

		NonEmptyRule[] first = RequirementRuleCompiler.GetNonEmptyRules(typeof(NotEmptyStringConfig), options);
		NonEmptyRule[] second = RequirementRuleCompiler.GetNonEmptyRules(typeof(NotEmptyStringConfig), options);

		Assert.AreSame(first, second);
	}
```

- [ ] **Step 3: Run test to verify it fails**

Run: `dotnet test --filter "FullyQualifiedName~RequirementRuleCompilerTests"`
Expected: compile failure, `CS0103: The name 'NonEmptyRule' does not exist` and `CS0117: 'RequirementRuleCompiler' does not contain a definition for 'GetNonEmptyRules'`.

- [ ] **Step 4: Add `NonEmptyRule`**

Append to `JsonRequiredConditionally/RequirementRule.cs`:

```csharp
/// <summary>
/// One member that must be present in the payload and carry a non-empty value.
/// </summary>
/// <remarks>
/// Deliberately a separate type from <see cref="RequirementRule"/> rather than a mode flag on it.
/// <see cref="RequirementRule"/> is sibling-conditional and evaluates against a materialized instance,
/// whereas this rule is unconditional and evaluates against a <see cref="System.Text.Json.JsonElement"/>.
/// A single type carrying both would leave half its state unused on every instance.
/// </remarks>
internal sealed class NonEmptyRule(string jsonName, string memberName)
{
	/// <summary>
	/// Gets the name this member carries in the JSON payload.
	/// </summary>
	internal string JsonName { get; } = jsonName;

	/// <summary>
	/// Gets the CLR name of the decorated member.
	/// </summary>
	internal string MemberName { get; } = memberName;
}
```

There is deliberately no `IsSatisfiedBy` here. Satisfaction depends on the payload element, which the rule does not hold, so `GraphValidator` applies `EmptinessInspector` to the element it already has.

- [ ] **Step 5: Add the cache and the compilation methods**

In `JsonRequiredConditionally/RequirementRuleCompiler.cs`, immediately after the existing `RuleCache` field declaration, add:

```csharp
	[SuppressMessage("Style", "IDE0028:Collection initialization can be simplified", Justification = "A collection expression does not compile for ConditionalWeakTable on netstandard2.0.")]
	private static readonly ConditionalWeakTable<JsonSerializerOptions, ConcurrentDictionary<Type, NonEmptyRule[]>> NonEmptyRuleCache = new();
```

Then, immediately after the existing `GetRules` method, add:

```csharp
	/// <summary>
	/// Builds the non-empty rules for a type by reflecting over its members decorated with
	/// <see cref="JsonRequiredAndNotEmptyAttribute"/>.
	/// </summary>
	/// <param name="type">The type to compile rules for.</param>
	/// <param name="options">The options to resolve the type's member model through.</param>
	/// <returns>One rule per decorated member.</returns>
	internal static NonEmptyRule[] CompileNonEmpty(Type type, JsonSerializerOptions options)
	{
		JsonTypeInfo? typeInfo = TryGetTypeInfo(options, type);

		if (typeInfo is null)
		{
			return [];
		}

		List<NonEmptyRule> rules = [];

		foreach (JsonPropertyInfo property in typeInfo.Properties)
		{
			if (property.AttributeProvider is not MemberInfo member)
			{
				continue;
			}

			if (!member.IsDefined(typeof(JsonRequiredAndNotEmptyAttribute), inherit: true))
			{
				continue;
			}

			// property.Name is System.Text.Json's own resolved JSON name, so an explicit
			// [JsonPropertyName] has already won over the naming policy.
			rules.Add(new NonEmptyRule(property.Name, member.Name));
		}

		return [.. rules];
	}

	/// <summary>
	/// Gets the cached non-empty rules for a type under a given set of options.
	/// </summary>
	/// <param name="type">The type to get rules for.</param>
	/// <param name="options">
	/// The options to resolve the type's member model through. Must be factory-free, for the same
	/// reason documented on <see cref="GetRules"/>: System.Text.Json leaves
	/// <see cref="JsonTypeInfo.Properties"/> empty for a type carrying its own converter, so
	/// factory-carrying options silently yield no rules at all.
	/// </param>
	/// <returns>One rule per decorated member.</returns>
	internal static NonEmptyRule[] GetNonEmptyRules(Type type, JsonSerializerOptions options)
	{
		ConcurrentDictionary<Type, NonEmptyRule[]> perType =
			NonEmptyRuleCache.GetValue(options, static _ => new ConcurrentDictionary<Type, NonEmptyRule[]>());

		return perType.GetOrAdd(type, t => CompileNonEmpty(t, options));
	}
```

- [ ] **Step 6: Run test to verify it passes**

Run: `dotnet test --filter "FullyQualifiedName~RequirementRuleCompilerTests"`
Expected: PASS, including all pre-existing tests in the class.

- [ ] **Step 7: Commit**

```bash
git add JsonRequiredConditionally/RequirementRule.cs JsonRequiredConditionally/RequirementRuleCompiler.cs JsonRequiredConditionally.Test/TestModels.cs JsonRequiredConditionally.Test/RequirementRuleCompilerTests.cs
git commit -m "feat: compile NonEmptyRule from JsonRequiredAndNotEmptyAttribute"
```

---

### Task 4: Eligibility

Depends on Task 2. Independent of Task 3: this decides whether the factory *claims* a type, which is a separate question from what rules compile for it.

**Files:**
- Modify: `JsonRequiredConditionally/RequirementRuleCompiler.cs` (`HasDirectlyDecoratedMember`)
- Modify: `JsonRequiredConditionally.Test/EligibilityTests.cs` (append)

**Interfaces:**
- Consumes: `JsonRequiredAndNotEmptyAttribute` from Task 2.
- Produces: no new members. `RequirementRuleCompiler.HasRules(Type)` starts returning `true` for types decorated with the new attribute, and for types that reach one transitively. Reachability needs no change of its own, because `EnumerateReachableMemberTypes` already routes through `HasDirectlyDecoratedMember`.

- [ ] **Step 1: Write the failing test**

Append to `JsonRequiredConditionally.Test/EligibilityTests.cs`, inside the existing `[TestClass]`:

```csharp
	[TestMethod]
	public void TypeWithOnlyTheNotEmptyAttributeIsClaimed()
	{
		Assert.IsTrue(RequirementRuleCompiler.HasRules(typeof(NotEmptyStringConfig)));
	}

	[TestMethod]
	public void HolderReachingANotEmptyMemberIsClaimed()
	{
		Assert.IsTrue(RequirementRuleCompiler.HasRules(typeof(NotEmptyHolder)));
	}

	[TestMethod]
	public void HolderReachingANotEmptyMemberThroughACollectionIsClaimed()
	{
		Assert.IsTrue(RequirementRuleCompiler.HasRules(typeof(NotEmptySequenceHolder)));
		Assert.IsTrue(RequirementRuleCompiler.HasRules(typeof(NotEmptyDictionaryHolder)));
	}

	[TestMethod]
	public void HolderReachingANotEmptyMemberThroughNestedCollectionsIsClaimed()
	{
		Assert.IsTrue(RequirementRuleCompiler.HasRules(typeof(NotEmptyNestedSequenceHolder)));
	}

	[TestMethod]
	public void BareCollectionsAreStillNotClaimed()
	{
		// IsExcludedFromEligibility rejects anything assignable to IEnumerable, so a bare collection
		// is never claimed at its own top however decorated its element type is. The holder that owns
		// the collection is what gets claimed, which is what roots the reported path at the outermost
		// container. The new attribute must not change this.
		Assert.IsFalse(RequirementRuleCompiler.HasRules(typeof(List<NotEmptyStringConfig>)));
		Assert.IsFalse(RequirementRuleCompiler.HasRules(typeof(NotEmptyStringConfig[])));
	}

	[TestMethod]
	public void UndecoratedTypesAreStillNotClaimed()
	{
		Assert.IsFalse(RequirementRuleCompiler.HasRules(typeof(PlainConfig)));
	}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test --filter "FullyQualifiedName~EligibilityTests"`
Expected: FAIL. `TypeWithOnlyTheNotEmptyAttributeIsClaimed` and the three holder tests fail with `Assert.IsTrue failed`, because `HasDirectlyDecoratedMember` still looks only for `JsonRequiredIfSiblingIsAttribute`. `BareCollectionsAreStillNotClaimed` and `UndecoratedTypesAreStillNotClaimed` pass from the start, and must keep passing after Step 3.

- [ ] **Step 3: Widen the probe**

In `JsonRequiredConditionally/RequirementRuleCompiler.cs`, in `HasDirectlyDecoratedMember`, replace:

```csharp
			if (property.AttributeProvider is MemberInfo member &&
				member.IsDefined(typeof(JsonRequiredIfSiblingIsAttribute), inherit: true))
			{
				return true;
			}
```

with:

```csharp
			if (property.AttributeProvider is MemberInfo member &&
				(member.IsDefined(typeof(JsonRequiredIfSiblingIsAttribute), inherit: true) ||
					member.IsDefined(typeof(JsonRequiredAndNotEmptyAttribute), inherit: true)))
			{
				return true;
			}
```

Then update the method's `<remarks>` so it no longer implies a single attribute. Find the sentence beginning "the rule is about the member's <em>presence in the payload</em>" and add after that paragraph:

```csharp
	/// <para>
	/// Two attributes claim a type: <see cref="JsonRequiredIfSiblingIsAttribute"/> and
	/// <see cref="JsonRequiredAndNotEmptyAttribute"/>. Either is sufficient. The probe still runs
	/// with <c>IncludeFields = true</c> so a decorated plain field claims its type, and rule
	/// compilation still runs against the caller's real options, so with <c>IncludeFields = false</c>
	/// no rule of either kind is produced and nothing is enforced.
	/// </para>
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test --filter "FullyQualifiedName~EligibilityTests"`
Expected: PASS, including all pre-existing tests in the class.

- [ ] **Step 5: Run the whole suite**

Run: `dotnet test`
Expected: PASS. Claiming more types must not change any existing behavior. A failure here means a previously unclaimed type is now being buffered and validated.

- [ ] **Step 6: Commit**

```bash
git add JsonRequiredConditionally/RequirementRuleCompiler.cs JsonRequiredConditionally.Test/EligibilityTests.cs
git commit -m "feat: claim types decorated with JsonRequiredAndNotEmptyAttribute"
```

---

### Task 5: `EmptyProperties` on the exception

Depends on nothing. Needed by Task 6.

**Files:**
- Modify: `JsonRequiredConditionally/JsonRequiredConditionallyException.cs`
- Modify: `JsonRequiredConditionally.Test/ExceptionTests.cs` (append)

**Interfaces:**
- Consumes: nothing.
- Produces:
  - `public IReadOnlyList<string> EmptyProperties { get; }` — empty on every pre-existing constructor.
  - `public JsonRequiredConditionallyException(IReadOnlyList<string> missingProperties, IReadOnlyList<string> emptyProperties)` — consumed by Task 6. Both lists are copied.
  - The single-list constructor's message text is **unchanged**, byte for byte.

- [ ] **Step 1: Write the failing test**

Append to `JsonRequiredConditionally.Test/ExceptionTests.cs`, inside the existing `[TestClass]`:

```csharp
	[TestMethod]
	public void ExceptionExposesEmptyProperties()
	{
		JsonRequiredConditionallyException exception = new(["tuning"], ["items", "tags"]);

		CollectionAssert.AreEqual(new List<string> { "items", "tags" }, exception.EmptyProperties.ToList());
	}

	[TestMethod]
	public void SingleListConstructorLeavesEmptyPropertiesEmpty()
	{
		JsonRequiredConditionallyException exception = new(["tuning"]);

		Assert.IsEmpty(exception.EmptyProperties);
	}

	[TestMethod]
	public void DefaultConstructorProducesEmptyEmptyProperties()
	{
		JsonRequiredConditionallyException exception = new();

		Assert.IsEmpty(exception.EmptyProperties);
	}

	[TestMethod]
	public void MessageNamesBothCategoriesWhenBothArePopulated()
	{
		JsonRequiredConditionallyException exception = new(["tuning"], ["items"]);

		StringAssert.Contains(exception.Message, "tuning");
		StringAssert.Contains(exception.Message, "items");
	}

	[TestMethod]
	public void MessageForEmptyOnlyDoesNotClaimAnythingWasAbsent()
	{
		JsonRequiredConditionallyException exception = new([], ["items"]);

		StringAssert.Contains(exception.Message, "items");
		Assert.IsFalse(exception.Message.Contains("absent", StringComparison.Ordinal));
	}

	[TestMethod]
	public void TwoListConstructorWithNoEmptiesMatchesTheSingleListMessage()
	{
		JsonRequiredConditionallyException single = new(["tuning"]);
		JsonRequiredConditionallyException both = new(["tuning"], []);

		Assert.AreEqual(single.Message, both.Message);
	}

	[TestMethod]
	public void EmptyPropertiesAreCopiedNotAliased()
	{
		List<string> empties = ["items"];
		JsonRequiredConditionallyException exception = new([], empties);

		empties.Add("tags");

		Assert.HasCount(1, exception.EmptyProperties);
	}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test --filter "FullyQualifiedName~ExceptionTests"`
Expected: compile failure, `CS1061: 'JsonRequiredConditionallyException' does not contain a definition for 'EmptyProperties'` and `CS1729: does not contain a constructor that takes 2 arguments`.

- [ ] **Step 3: Write the implementation**

In `JsonRequiredConditionally/JsonRequiredConditionallyException.cs`, rewrite the three simple constructors from expression bodies to block bodies so both lists are initialized, add the two-list constructor and the property, and add the `BuildMessage` overload.

Update the type's `<summary>`:

```csharp
/// <summary>
/// Thrown when one or more properties failed a requirement declared by this library: absent from the
/// JSON payload when a sibling value required them, or present but empty when
/// <see cref="JsonRequiredAndNotEmptyAttribute"/> required content.
/// </summary>
```

Replace the three simple constructors:

```csharp
	/// <summary>
	/// Initializes a new instance of the <see cref="JsonRequiredConditionallyException"/> class.
	/// </summary>
	public JsonRequiredConditionallyException()
		: base()
	{
		MissingProperties = [];
		EmptyProperties = [];
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="JsonRequiredConditionallyException"/> class.
	/// </summary>
	/// <param name="message">The message that describes the error.</param>
	public JsonRequiredConditionallyException(string message)
		: base(message)
	{
		MissingProperties = [];
		EmptyProperties = [];
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="JsonRequiredConditionallyException"/> class.
	/// </summary>
	/// <param name="message">The message that describes the error.</param>
	/// <param name="innerException">The exception that caused this exception.</param>
	public JsonRequiredConditionallyException(string message, Exception innerException)
		: base(message, innerException)
	{
		MissingProperties = [];
		EmptyProperties = [];
	}
```

Change the single-list constructor's body so it also initializes `EmptyProperties`, leaving its `base(...)` call untouched:

```csharp
	public JsonRequiredConditionallyException(IReadOnlyList<string> missingProperties)
		: base(BuildMessage(missingProperties))
	{
		MissingProperties = [.. missingProperties];
		EmptyProperties = [];
	}
```

Add the two-list constructor immediately after it:

```csharp
	/// <summary>
	/// Initializes a new instance of the <see cref="JsonRequiredConditionallyException"/> class.
	/// </summary>
	/// <param name="missingProperties">The JSON names of the properties that were required but absent.</param>
	/// <param name="emptyProperties">The JSON names of the properties that were present but empty.</param>
	/// <remarks>
	/// Both lists are copied rather than stored, for the reason documented on the single-list
	/// constructor.
	/// </remarks>
	public JsonRequiredConditionallyException(IReadOnlyList<string> missingProperties, IReadOnlyList<string> emptyProperties)
		: base(BuildMessage(missingProperties, emptyProperties))
	{
		MissingProperties = [.. missingProperties];
		EmptyProperties = [.. emptyProperties];
	}
```

Add the property after `MissingProperties`:

```csharp
	/// <summary>
	/// Gets the JSON names of the properties that were present in the payload but carried an empty
	/// value.
	/// </summary>
	/// <remarks>
	/// A property that was absent entirely is reported in <see cref="MissingProperties"/>, not here,
	/// even when it was decorated with <see cref="JsonRequiredAndNotEmptyAttribute"/>.
	/// </remarks>
	public IReadOnlyList<string> EmptyProperties { get; }
```

Add the `BuildMessage` overload after the existing one:

```csharp
	private static string BuildMessage(IReadOnlyList<string> missingProperties, IReadOnlyList<string> emptyProperties)
	{
		Ensure.NotNull(missingProperties);
		Ensure.NotNull(emptyProperties);

		// Delegating when there is nothing empty keeps the single-category message byte-identical to
		// what the single-list constructor has always produced, so callers asserting on it keep working.
		if (emptyProperties.Count == 0)
		{
			return BuildMessage(missingProperties);
		}

		string names = string.Join("', '", emptyProperties);
		string emptyClause = $"The following properties were required to be non-empty but were empty: '{names}'.";

		return missingProperties.Count == 0
			? emptyClause
			: BuildMessage(missingProperties) + " " + emptyClause;
	}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test --filter "FullyQualifiedName~ExceptionTests"`
Expected: PASS, including all pre-existing tests in the class. If `ExceptionMessageNamesEveryMissingProperty` or any other pre-existing message assertion fails, the `BuildMessage` delegation is wrong and must be fixed rather than the test.

- [ ] **Step 5: Commit**

```bash
git add JsonRequiredConditionally/JsonRequiredConditionallyException.cs JsonRequiredConditionally.Test/ExceptionTests.cs
git commit -m "feat: add EmptyProperties to JsonRequiredConditionallyException"
```

---

### Task 6: Wire the walk

Depends on Tasks 1, 3, 4 and 5. This is where the feature becomes end-to-end.

**Files:**
- Modify: `JsonRequiredConditionally/GraphValidator.cs`
- Create: `JsonRequiredConditionally.Test/NotEmptyTests.cs`

**Interfaces:**
- Consumes: `EmptinessInspector.IsEmpty` (Task 1), `RequirementRuleCompiler.GetNonEmptyRules` (Task 3), `HasRules` widening (Task 4), the two-list exception constructor (Task 5).
- Produces: `internal sealed class ViolationCollector` with `internal List<string> Missing { get; }`, `internal List<string> Empty { get; }` and `internal bool Any { get; }`. Used only within `GraphValidator`. `GraphValidator.Validate` keeps its existing four-parameter signature.

- [ ] **Step 1: Write the failing test**

Create `JsonRequiredConditionally.Test/NotEmptyTests.cs`:

```csharp
// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.JsonRequiredConditionally.Tests;

using System.Text.Json;
using System.Text.Json.Serialization;

/// <summary>
/// Covers <see cref="JsonRequiredAndNotEmptyAttribute"/> end to end: every payload shape against
/// every member shape.
/// </summary>
[TestClass]
public class NotEmptyTests
{
	private static JsonSerializerOptions CreateOptions() =>
		new() { Converters = { new JsonStringEnumConverter(), new JsonRequiredConditionallyConverterFactory() } };

	private static JsonRequiredConditionallyException Throws<T>(string json) =>
		Assert.ThrowsExactly<JsonRequiredConditionallyException>(
			() => JsonSerializer.Deserialize<T>(json, CreateOptions()));

	[TestMethod]
	public void AbsentStringIsReportedAsMissing()
	{
		JsonRequiredConditionallyException exception = Throws<NotEmptyStringConfig>(/*lang=json,strict*/ "{}");

		CollectionAssert.AreEqual(new List<string> { "Name" }, exception.MissingProperties.ToList());
		Assert.IsEmpty(exception.EmptyProperties);
	}

	[TestMethod]
	public void NullStringIsReportedAsEmpty()
	{
		JsonRequiredConditionallyException exception = Throws<NotEmptyStringConfig>(/*lang=json,strict*/ """{"Name":null}""");

		Assert.IsEmpty(exception.MissingProperties);
		CollectionAssert.AreEqual(new List<string> { "Name" }, exception.EmptyProperties.ToList());
	}

	[TestMethod]
	public void ZeroLengthStringIsReportedAsEmpty()
	{
		JsonRequiredConditionallyException exception = Throws<NotEmptyStringConfig>(/*lang=json,strict*/ """{"Name":""}""");

		CollectionAssert.AreEqual(new List<string> { "Name" }, exception.EmptyProperties.ToList());
	}

	[TestMethod]
	public void WhitespaceStringIsAccepted()
	{
		NotEmptyStringConfig? config = JsonSerializer.Deserialize<NotEmptyStringConfig>(
			/*lang=json,strict*/ """{"Name":"   "}""", CreateOptions());

		Assert.IsNotNull(config);
		Assert.AreEqual("   ", config.Name);
	}

	[TestMethod]
	public void PopulatedStringIsAccepted()
	{
		NotEmptyStringConfig? config = JsonSerializer.Deserialize<NotEmptyStringConfig>(
			/*lang=json,strict*/ """{"Name":"x"}""", CreateOptions());

		Assert.IsNotNull(config);
		Assert.AreEqual("x", config.Name);
	}

	[TestMethod]
	public void EmptyListIsReportedAsEmpty()
	{
		JsonRequiredConditionallyException exception = Throws<NotEmptyListConfig>(/*lang=json,strict*/ """{"Items":[]}""");

		CollectionAssert.AreEqual(new List<string> { "Items" }, exception.EmptyProperties.ToList());
	}

	[TestMethod]
	public void PopulatedListIsAccepted()
	{
		NotEmptyListConfig? config = JsonSerializer.Deserialize<NotEmptyListConfig>(
			/*lang=json,strict*/ """{"Items":["a"]}""", CreateOptions());

		Assert.IsNotNull(config);
		Assert.HasCount(1, config.Items!);
	}

	[TestMethod]
	public void ListOfOneEmptyStringIsAccepted()
	{
		NotEmptyListConfig? config = JsonSerializer.Deserialize<NotEmptyListConfig>(
			/*lang=json,strict*/ """{"Items":[""]}""", CreateOptions());

		Assert.IsNotNull(config);
		Assert.HasCount(1, config.Items!);
	}

	[TestMethod]
	public void EmptySetIsReportedAsEmpty()
	{
		JsonRequiredConditionallyException exception = Throws<NotEmptySetConfig>(/*lang=json,strict*/ """{"Tags":[]}""");

		CollectionAssert.AreEqual(new List<string> { "Tags" }, exception.EmptyProperties.ToList());
	}

	[TestMethod]
	public void EmptyArrayMemberIsReportedAsEmpty()
	{
		JsonRequiredConditionallyException exception = Throws<NotEmptyArrayConfig>(/*lang=json,strict*/ """{"Values":[]}""");

		CollectionAssert.AreEqual(new List<string> { "Values" }, exception.EmptyProperties.ToList());
	}

	[TestMethod]
	public void EmptyDictionaryIsReportedAsEmpty()
	{
		JsonRequiredConditionallyException exception = Throws<NotEmptyDictionaryConfig>(/*lang=json,strict*/ """{"Lookup":{}}""");

		CollectionAssert.AreEqual(new List<string> { "Lookup" }, exception.EmptyProperties.ToList());
	}

	[TestMethod]
	public void PopulatedDictionaryIsAccepted()
	{
		NotEmptyDictionaryConfig? config = JsonSerializer.Deserialize<NotEmptyDictionaryConfig>(
			/*lang=json,strict*/ """{"Lookup":{"a":"b"}}""", CreateOptions());

		Assert.IsNotNull(config);
		Assert.HasCount(1, config.Lookup!);
	}

	[TestMethod]
	public void EmptyStringBehindACustomConverterIsReportedAsEmpty()
	{
		JsonRequiredConditionallyException exception = Throws<NotEmptyConvertedConfig>(/*lang=json,strict*/ """{"Label":""}""");

		CollectionAssert.AreEqual(new List<string> { "Label" }, exception.EmptyProperties.ToList());
	}

	[TestMethod]
	public void PopulatedStringBehindACustomConverterIsAccepted()
	{
		NotEmptyConvertedConfig? config = JsonSerializer.Deserialize<NotEmptyConvertedConfig>(
			/*lang=json,strict*/ """{"Label":"x"}""", CreateOptions());

		Assert.IsNotNull(config);
		Assert.AreEqual("x", config.Label!.Value);
	}

	[TestMethod]
	public void PresentNumberIsAlwaysAccepted()
	{
		NotEmptyIntConfig? config = JsonSerializer.Deserialize<NotEmptyIntConfig>(
			/*lang=json,strict*/ """{"Count":0}""", CreateOptions());

		Assert.IsNotNull(config);
		Assert.AreEqual(0, config.Count);
	}

	[TestMethod]
	public void AbsentNumberIsStillReportedAsMissing()
	{
		JsonRequiredConditionallyException exception = Throws<NotEmptyIntConfig>(/*lang=json,strict*/ "{}");

		CollectionAssert.AreEqual(new List<string> { "Count" }, exception.MissingProperties.ToList());
	}

	[TestMethod]
	public void NullNullableNumberIsReportedAsEmpty()
	{
		JsonRequiredConditionallyException exception = Throws<NotEmptyNullableIntConfig>(/*lang=json,strict*/ """{"Count":null}""");

		CollectionAssert.AreEqual(new List<string> { "Count" }, exception.EmptyProperties.ToList());
	}

	[TestMethod]
	public void PresentNullableNumberIsAccepted()
	{
		NotEmptyNullableIntConfig? config = JsonSerializer.Deserialize<NotEmptyNullableIntConfig>(
			/*lang=json,strict*/ """{"Count":0}""", CreateOptions());

		Assert.IsNotNull(config);
		Assert.AreEqual(0, config.Count);
	}
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test --filter "FullyQualifiedName~NotEmptyTests"`
Expected: FAIL. Every test that expects a throw fails with "Expected exception JsonRequiredConditionallyException but no exception was thrown", because the walk does not yet evaluate non-empty rules. The four acceptance tests already pass.

- [ ] **Step 3: Add `ViolationCollector`**

Append to `JsonRequiredConditionally/GraphValidator.cs`, after the `GraphValidator` class:

```csharp
/// <summary>
/// Accumulates the violations found during one walk, in their two categories.
/// </summary>
/// <remarks>
/// A single parameter carrying both lists, rather than one parameter per category: the walk methods
/// already take seven parameters each, and the two categories are always produced and consumed
/// together.
/// </remarks>
internal sealed class ViolationCollector
{
	/// <summary>
	/// Gets the paths of properties that were required but absent from the payload.
	/// </summary>
	internal List<string> Missing { get; } = [];

	/// <summary>
	/// Gets the paths of properties that were present but carried an empty value.
	/// </summary>
	internal List<string> Empty { get; } = [];

	/// <summary>
	/// Gets a value indicating whether any violation was collected.
	/// </summary>
	internal bool Any => Missing.Count > 0 || Empty.Count > 0;
}
```

- [ ] **Step 4: Thread the collector through the walk**

In `JsonRequiredConditionally/GraphValidator.cs`, replace the `List<string> missing` parameter with `ViolationCollector violations` in all four methods: `Walk`, `Descend`, `DescendDictionary` and `DescendSequence`. Update every call site to pass `violations` instead of `missing`. No other logic in those methods changes.

Rewrite `Validate`:

```csharp
	internal static void Validate(JsonElement element, object instance, JsonSerializerOptions plainOptions, JsonSerializerOptions userOptions)
	{
		StringComparer comparer = userOptions.PropertyNameCaseInsensitive
			? StringComparer.OrdinalIgnoreCase
			: StringComparer.Ordinal;

		ViolationCollector violations = new();

		Walk(element, instance, plainOptions, userOptions, comparer, string.Empty, violations);

		if (violations.Any)
		{
			throw new JsonRequiredConditionallyException(violations.Missing, violations.Empty);
		}
	}
```

In `Walk`, the existing conditional-rule loop becomes:

```csharp
			foreach (RequirementRule rule in RequirementRuleCompiler.GetRules(type, plainOptions))
			{
				if (!present.Contains(rule.JsonName) && rule.IsRequiredFor(instance))
				{
					violations.Missing.Add(Combine(path, rule.JsonName));
				}
			}
```

- [ ] **Step 5: Add the non-empty loop**

In `Walk`, immediately after the conditional-rule loop and still inside the `if (RequirementRuleCompiler.HasRules(type))` block, add:

```csharp
			foreach (NonEmptyRule rule in RequirementRuleCompiler.GetNonEmptyRules(type, plainOptions))
			{
				string fullPath = Combine(path, rule.JsonName);

				if (!TryGetProperty(element, rule.JsonName, comparer, userOptions.PropertyNameCaseInsensitive, out JsonElement child))
				{
					// The loop above may already have reported this exact path, when the same member
					// carries both attributes and its sibling condition was satisfied. Violation lists
					// hold only actual failures and are therefore short, so a linear scan costs less
					// than building a set would.
					if (!violations.Missing.Contains(fullPath))
					{
						violations.Missing.Add(fullPath);
					}
				}
				else if (EmptinessInspector.IsEmpty(child))
				{
					violations.Empty.Add(fullPath);
				}
			}
```

`TryGetProperty` is used rather than the `present` set from `PresenceScanner`, because this rule needs the value and not merely the name.

- [ ] **Step 6: Run test to verify it passes**

Run: `dotnet test --filter "FullyQualifiedName~NotEmptyTests"`
Expected: PASS, 18 tests per framework.

- [ ] **Step 7: Run the whole suite**

Run: `dotnet test`
Expected: PASS, every test, every framework. No existing test may be edited to achieve this.

- [ ] **Step 8: Commit**

```bash
git add JsonRequiredConditionally/GraphValidator.cs JsonRequiredConditionally.Test/NotEmptyTests.cs
git commit -m "feat: enforce JsonRequiredAndNotEmpty during the graph walk"
```

---

### Task 7: Cross-cutting behavior

Depends on Task 6. Pins the claims the spec makes that the core tests do not reach: nested paths, naming policies, self-sufficiency and deduplication.

**Files:**
- Modify: `JsonRequiredConditionally.Test/NestingTests.cs` (append)
- Modify: `JsonRequiredConditionally.Test/NamingTests.cs` (append)
- Modify: `JsonRequiredConditionally.Test/ConverterTests.cs` (append)
- Modify: `JsonRequiredConditionally.Test/SemanticsTests.cs` (append)

**Interfaces:**
- Consumes: everything from Tasks 1 to 6.
- Produces: no production interfaces. Test-only, except that the deduplication behavior it pins was implemented in Task 6 Step 5.

- [ ] **Step 1: Write the nesting tests**

Append to `JsonRequiredConditionally.Test/NestingTests.cs`, inside the existing `[TestClass]`. If the class has its own options factory with a different name, use that instead of `CreateOptions`.

```csharp
	[TestMethod]
	public void EmptyMemberInsideANestedObjectReportsADottedPath()
	{
		JsonRequiredConditionallyException exception = Assert.ThrowsExactly<JsonRequiredConditionallyException>(
			() => JsonSerializer.Deserialize<NotEmptyHolder>(
				/*lang=json,strict*/ """{"Child":{"Name":""}}""", CreateOptions()));

		CollectionAssert.AreEqual(new List<string> { "Child.Name" }, exception.EmptyProperties.ToList());
	}

	[TestMethod]
	public void EmptyMemberInsideASequenceReportsAnIndexedPath()
	{
		JsonRequiredConditionallyException exception = Assert.ThrowsExactly<JsonRequiredConditionallyException>(
			() => JsonSerializer.Deserialize<NotEmptySequenceHolder>(
				/*lang=json,strict*/ """{"Children":[{"Name":"ok"},{"Name":""}]}""", CreateOptions()));

		CollectionAssert.AreEqual(new List<string> { "Children[1].Name" }, exception.EmptyProperties.ToList());
	}

	[TestMethod]
	public void EmptyMemberInsideADictionaryReportsAKeyedPath()
	{
		JsonRequiredConditionallyException exception = Assert.ThrowsExactly<JsonRequiredConditionallyException>(
			() => JsonSerializer.Deserialize<NotEmptyDictionaryHolder>(
				/*lang=json,strict*/ """{"Lookup":{"a":{"Name":""}}}""", CreateOptions()));

		CollectionAssert.AreEqual(new List<string> { "Lookup.a.Name" }, exception.EmptyProperties.ToList());
	}

	[TestMethod]
	public void EveryEmptyMemberInASequenceIsReportedInOnePass()
	{
		JsonRequiredConditionallyException exception = Assert.ThrowsExactly<JsonRequiredConditionallyException>(
			() => JsonSerializer.Deserialize<NotEmptySequenceHolder>(
				/*lang=json,strict*/ """{"Children":[{"Name":""},{"Name":""}]}""", CreateOptions()));

		CollectionAssert.AreEqual(
			new List<string> { "Children[0].Name", "Children[1].Name" },
			exception.EmptyProperties.ToList());
	}

	[TestMethod]
	public void MissingAndEmptyViolationsAreCollectedTogether()
	{
		JsonRequiredConditionallyException exception = Assert.ThrowsExactly<JsonRequiredConditionallyException>(
			() => JsonSerializer.Deserialize<NotEmptySequenceHolder>(
				/*lang=json,strict*/ """{"Children":[{},{"Name":""}]}""", CreateOptions()));

		CollectionAssert.AreEqual(new List<string> { "Children[0].Name" }, exception.MissingProperties.ToList());
		CollectionAssert.AreEqual(new List<string> { "Children[1].Name" }, exception.EmptyProperties.ToList());
	}
```

- [ ] **Step 2: Write the naming tests**

Append to `JsonRequiredConditionally.Test/NamingTests.cs`, inside the existing `[TestClass]`:

```csharp
	[TestMethod]
	public void NotEmptyRuleHonorsJsonPropertyName()
	{
		JsonSerializerOptions options = new() { Converters = { new JsonRequiredConditionallyConverterFactory() } };

		JsonRequiredConditionallyException exception = Assert.ThrowsExactly<JsonRequiredConditionallyException>(
			() => JsonSerializer.Deserialize<NotEmptyRenamedConfig>(/*lang=json,strict*/ """{"tuning_name":""}""", options));

		CollectionAssert.AreEqual(new List<string> { "tuning_name" }, exception.EmptyProperties.ToList());
	}

	[TestMethod]
	public void NotEmptyRuleHonorsACamelCaseNamingPolicy()
	{
		JsonSerializerOptions options = new()
		{
			PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
			Converters = { new JsonRequiredConditionallyConverterFactory() },
		};

		JsonRequiredConditionallyException exception = Assert.ThrowsExactly<JsonRequiredConditionallyException>(
			() => JsonSerializer.Deserialize<NotEmptyStringConfig>(/*lang=json,strict*/ """{"name":""}""", options));

		CollectionAssert.AreEqual(new List<string> { "name" }, exception.EmptyProperties.ToList());
	}

	[TestMethod]
	public void NotEmptyRuleHonorsCaseInsensitiveMatching()
	{
		JsonSerializerOptions options = new()
		{
			PropertyNameCaseInsensitive = true,
			Converters = { new JsonRequiredConditionallyConverterFactory() },
		};

		JsonRequiredConditionallyException exception = Assert.ThrowsExactly<JsonRequiredConditionallyException>(
			() => JsonSerializer.Deserialize<NotEmptyStringConfig>(/*lang=json,strict*/ """{"NAME":""}""", options));

		CollectionAssert.AreEqual(new List<string> { "Name" }, exception.EmptyProperties.ToList());
	}
```

- [ ] **Step 3: Write the self-sufficiency and deduplication tests**

Append to `JsonRequiredConditionally.Test/ConverterTests.cs`, inside the existing `[TestClass]`:

```csharp
	[TestMethod]
	public void NotEmptyAttributeAloneReportsAnAbsentProperty()
	{
		JsonRequiredConditionallyException exception = Assert.ThrowsExactly<JsonRequiredConditionallyException>(
			() => JsonSerializer.Deserialize<NotEmptyStringConfig>(/*lang=json,strict*/ "{}", CreateOptions()));

		CollectionAssert.AreEqual(new List<string> { "Name" }, exception.MissingProperties.ToList());
	}

	[TestMethod]
	public void BothAttributesOnOneAbsentMemberReportItOnce()
	{
		JsonRequiredConditionallyException exception = Assert.ThrowsExactly<JsonRequiredConditionallyException>(
			() => JsonSerializer.Deserialize<NotEmptyAndConditionalConfig>(
				"""{"Kind":"Advanced"}""", CreateOptions()));

		CollectionAssert.AreEqual(new List<string> { "Tuning" }, exception.MissingProperties.ToList());
		Assert.IsEmpty(exception.EmptyProperties);
	}

	[TestMethod]
	public void BothAttributesReportEmptinessEvenWhenTheSiblingConditionIsUnmet()
	{
		JsonRequiredConditionallyException exception = Assert.ThrowsExactly<JsonRequiredConditionallyException>(
			() => JsonSerializer.Deserialize<NotEmptyAndConditionalConfig>(
				"""{"Kind":"Basic","Tuning":""}""", CreateOptions()));

		Assert.IsEmpty(exception.MissingProperties);
		CollectionAssert.AreEqual(new List<string> { "Tuning" }, exception.EmptyProperties.ToList());
	}

	[TestMethod]
	public void BothAttributesStillReportAbsenceWhenTheSiblingConditionIsUnmet()
	{
		JsonRequiredConditionallyException exception = Assert.ThrowsExactly<JsonRequiredConditionallyException>(
			() => JsonSerializer.Deserialize<NotEmptyAndConditionalConfig>(
				"""{"Kind":"Basic"}""", CreateOptions()));

		CollectionAssert.AreEqual(new List<string> { "Tuning" }, exception.MissingProperties.ToList());
	}
```

That last test is the one that distinguishes the two rule kinds: the conditional rule does not fire because `Kind` is `Basic`, but the unconditional non-empty rule does.

- [ ] **Step 4: Write the semantics tests**

Append to `JsonRequiredConditionally.Test/SemanticsTests.cs`, inside the existing `[TestClass]`. If the class has its own options factory with a different name, use that.

```csharp
	[TestMethod]
	public void PresenceAloneNoLongerSatisfiesANotEmptyMember()
	{
		JsonRequiredConditionallyException exception = Assert.ThrowsExactly<JsonRequiredConditionallyException>(
			() => JsonSerializer.Deserialize<NotEmptyStringConfig>(/*lang=json,strict*/ """{"Name":null}""", CreateOptions()));

		CollectionAssert.AreEqual(new List<string> { "Name" }, exception.EmptyProperties.ToList());
	}

	[TestMethod]
	public void NotEmptyDoesNothingWhenTheFactoryIsNotRegistered()
	{
		JsonSerializerOptions bare = new();

		NotEmptyStringConfig? config = JsonSerializer.Deserialize<NotEmptyStringConfig>(
			/*lang=json,strict*/ "{}", bare);

		Assert.IsNotNull(config);
		Assert.IsNull(config.Name);
	}

	[TestMethod]
	public void WriteIsNotValidated()
	{
		NotEmptyStringConfig config = new() { Name = string.Empty };

		string json = JsonSerializer.Serialize(config, CreateOptions());

		StringAssert.Contains(json, "Name");
	}
```

`NotEmptyDoesNothingWhenTheFactoryIsNotRegistered` pins the caveat the README states: the attribute is inert without the factory, which is the one honest argument for also applying `[JsonRequired]`.

- [ ] **Step 5: Run the whole suite**

Run: `dotnet test`
Expected: PASS, every test, every framework.

- [ ] **Step 6: Commit**

```bash
git add JsonRequiredConditionally.Test/NestingTests.cs JsonRequiredConditionally.Test/NamingTests.cs JsonRequiredConditionally.Test/ConverterTests.cs JsonRequiredConditionally.Test/SemanticsTests.cs
git commit -m "test: cover nesting, naming, self-sufficiency and dedup for JsonRequiredAndNotEmpty"
```

---

### Task 8: Documentation and release verification

Depends on Tasks 1 to 7.

**Files:**
- Modify: `README.md`, `CLAUDE.md`, `DESCRIPTION.md`, `TAGS.md`

**Interfaces:**
- Consumes: the finished feature.
- Produces: nothing consumed by code.

- [ ] **Step 1: Update `README.md`**

Add a section for the new attribute covering, in this order:

1. A usage example on a collection member, showing the attribute used **alone**.
2. The emptiness table from the spec's Semantics section, verbatim.
3. That whitespace is not empty, and that this diverges deliberately from `System.ComponentModel.DataAnnotations.RequiredAttribute`.
4. That `[JsonRequired]` should **not** be paired with it, that pairing is redundant, and that pairing costs the aggregated violation list because System.Text.Json's own required check runs first and throws before the walk.
5. The two narrow reasons someone might still pair them: the attribute is inert if the factory is not registered, and `[JsonRequired]` sets `JsonPropertyInfo.IsRequired`, which schema and OpenAPI generators read.
6. A "why not `[MinLength(1)]`" subsection citing the three measured blockers from the spec: non-public members are invisible to `Validator.TryValidateObject`, it does not recurse into nested objects or collections, and `[Required]` is non-null rather than non-empty for anything that is not a `string`.
7. That `EmptyProperties` is where present-but-empty violations land, and `MissingProperties` is where absent ones land, including for this attribute.

- [ ] **Step 2: Update `CLAUDE.md`**

Three edits:

1. **Source organization table**: add rows for `JsonRequiredAndNotEmptyAttribute.cs` and `EmptinessInspector.cs`. Widen the `RequirementRule.cs` row to mention `NonEmptyRule`, the `RequirementRuleCompiler.cs` row to mention `CompileNonEmpty`/`GetNonEmptyRules` and the widened `HasDirectlyDecoratedMember`, the `GraphValidator.cs` row to mention `ViolationCollector` and the two categories, and the `JsonRequiredConditionallyException.cs` row to mention `EmptyProperties`.
2. **Key patterns**: the "Presence, not non-nullness" paragraph now needs its exception stated. Add that `[JsonRequiredAndNotEmpty]` deliberately departs from it, treating `null` as empty, and that emptiness is judged from the payload element rather than the CLR value specifically so it sees through custom converters.
3. **Test structure list**: add `EmptinessInspectorTests.cs` and `NotEmptyTests.cs`.

- [ ] **Step 3: Update `DESCRIPTION.md` and `TAGS.md`**

Widen the description from conditional presence to declarative requirement and emptiness validation over the System.Text.Json contract model. Add tags for non-empty validation.

- [ ] **Step 4: Verify the full matrix**

Run: `dotnet build --configuration Release`
Expected: succeeds for all six target frameworks with no warnings, since warnings are errors.

Run: `dotnet test`
Expected: PASS, every test, all four test frameworks.

- [ ] **Step 5: Verify with `ktsubuild`**

```powershell
dotnet tool install ktsu.KtsuBuild.Tool --tool-path "$env:TEMP\ktsubuild"
& "$env:TEMP\ktsubuild\ktsubuild" build --workspace "C:\dev\ktsu-dev\JsonRequiredConditionally" --configuration Release --verbose
```

Pass `--workspace` explicitly. It defaults to the current directory, and omitting it risks operating on the wrong repository. Do **not** run `ktsubuild ci` locally: it rewrites the generated metadata files and can tag and publish.

- [ ] **Step 6: Commit**

```bash
git add README.md CLAUDE.md DESCRIPTION.md TAGS.md
git commit -m "docs: document JsonRequiredAndNotEmpty [minor]"
```

---

## Self-Review

**Spec coverage.** Every spec section maps to a task: Semantics to Tasks 1 and 6, Combining with other attributes to Task 7, Members that can never be empty to Tasks 3 and 6, The payload element as the source of truth to Task 1, each Components subsection to Tasks 1 to 6, `SerializerFeatureGuard` unchanged so no task, Testing spread across Tasks 1 to 7, Documentation to Task 8, Versioning to Task 8 Step 6.

**Type consistency.** `EmptinessInspector.IsEmpty(JsonElement)`, `NonEmptyRule.JsonName`/`MemberName`, `RequirementRuleCompiler.CompileNonEmpty`/`GetNonEmptyRules`, `ViolationCollector.Missing`/`Empty`/`Any` and `JsonRequiredConditionallyException.EmptyProperties` are each named identically everywhere they appear.

**Correction made during review.** An earlier draft of Task 4 asserted that `List<List<NotEmptyStringConfig>>` and `NotEmptyStringConfig[][]` are claimed directly. They are not. `IsExcludedFromEligibility` rejects anything assignable to `IEnumerable`, and the existing test `HasRulesRejectsPrimitivesStringsAndEnums` already pins that for `List<SimpleConfig>`. Claiming happens on the *holder* that owns the collection, which is what roots a reported path at the outermost container. Task 4 now asserts the holder is claimed and adds `BareCollectionsAreStillNotClaimed` to pin that the new attribute did not change the exclusion.

**Known gap, deliberate.** The spec notes that `netstandard2.0` and `netstandard2.1` assets are not covered by the test matrix, which needs a `net472` leg. That predates this feature and stays out of scope.
