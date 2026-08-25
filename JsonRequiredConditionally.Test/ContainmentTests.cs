// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.JsonRequiredConditionally.Tests;

using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

/// <summary>
/// Covers the serializer features this library cannot model, which it must either refuse to claim
/// (so the feature keeps working) or reject loudly (so nothing is silently wrong).
/// </summary>
[TestClass]
public class ContainmentTests
{
	private static JsonSerializerOptions CreateOptions() =>
		new() { Converters = { new JsonStringEnumConverter(), new JsonRequiredConditionallyConverterFactory() } };

	[TestMethod]
	public void PolymorphicHierarchyWithDecoratedDerivedTypeSerializes()
	{
		ShapeBase shape = new DecoratedShape { Label = "a", Kind = Kind.Advanced, Tuning = "fast" };

		string json = JsonSerializer.Serialize(shape, CreateOptions());

		StringAssert.Contains(json, "decorated");
	}

	[TestMethod]
	public void PolymorphicHierarchyWithDecoratedDerivedTypeDeserializes()
	{
		ShapeBase? shape = JsonSerializer.Deserialize<ShapeBase>(
			"""{"$type":"decorated","Label":"a","Kind":"Advanced","Tuning":"fast"}""", CreateOptions());

		Assert.IsInstanceOfType<DecoratedShape>(shape);
		Assert.AreEqual("fast", ((DecoratedShape)shape).Tuning);
	}

	[TestMethod]
	public void PolymorphicTypesAreNotClaimedByTheFactory()
	{
		JsonRequiredConditionallyConverterFactory factory = new();

		Assert.IsFalse(factory.CanConvert(typeof(ShapeBase)));
		Assert.IsFalse(factory.CanConvert(typeof(DecoratedShape)));
	}

	[TestMethod]
	public void DecoratedNonPolymorphicTypeNestedInPolymorphicHolderIsStillValidated()
	{
		JsonRequiredConditionallyException exception = Assert.ThrowsExactly<JsonRequiredConditionallyException>(
			() => JsonSerializer.Deserialize<HolderShapeBase>(
				"""{"$type":"holder","Label":"a","Child":{"Kind":"Advanced"}}""", CreateOptions()));

		CollectionAssert.AreEquivalent(new List<string> { "Tuning" }, exception.MissingProperties.ToList());
	}

	[TestMethod]
	public void PopulateObjectCreationHandlingThrowsNamingThisLibrary()
	{
		JsonSerializerOptions options = CreateOptions();

		if (!TrySetPreferredObjectCreationHandlingToPopulate(options))
		{
			Assert.Inconclusive(
				"JsonSerializerOptions.PreferredObjectCreationHandling does not exist in this target framework's System.Text.Json, so the unsupported configuration it guards cannot be expressed here.");
			return;
		}

		NotSupportedException exception = Assert.ThrowsExactly<NotSupportedException>(
			() => JsonSerializer.Deserialize<SimpleConfig>("""{"Kind":"Basic"}""", options));

		StringAssert.Contains(exception.Message, "ktsu.JsonRequiredConditionally");
		StringAssert.Contains(exception.Message, "Populate");
	}

	[TestMethod]
	public void ReferenceHandlerThrowsNamingThisLibrary()
	{
		JsonSerializerOptions options = new()
		{
			ReferenceHandler = ReferenceHandler.Preserve,
			Converters = { new JsonStringEnumConverter(), new JsonRequiredConditionallyConverterFactory() },
		};

		NotSupportedException exception = Assert.ThrowsExactly<NotSupportedException>(
			() => JsonSerializer.Deserialize<SimpleConfig>("""{"Kind":"Basic"}""", options));

		StringAssert.Contains(exception.Message, "ktsu.JsonRequiredConditionally");
		StringAssert.Contains(exception.Message, "ReferenceHandler");
	}

	[TestMethod]
	public void ReferenceHandlerThrowsOnSerializationToo()
	{
		JsonSerializerOptions options = new()
		{
			ReferenceHandler = ReferenceHandler.Preserve,
			Converters = { new JsonRequiredConditionallyConverterFactory() },
		};

		Assert.ThrowsExactly<NotSupportedException>(
			() => JsonSerializer.Serialize(new SimpleConfig { Kind = Kind.Basic }, options));
	}

	[TestMethod]
	public void ReferenceHandlerThrowsForTypeClaimedOnlyByTheNotEmptyAttribute()
	{
		JsonSerializerOptions options = new()
		{
			ReferenceHandler = ReferenceHandler.Preserve,
			Converters = { new JsonRequiredConditionallyConverterFactory() },
		};

		NotSupportedException exception = Assert.ThrowsExactly<NotSupportedException>(
			() => JsonSerializer.Deserialize<NotEmptyStringConfig>("""{"Name":"a"}""", options));

		StringAssert.Contains(exception.Message, "ktsu.JsonRequiredConditionally");
		StringAssert.Contains(exception.Message, "ReferenceHandler");
	}

	[TestMethod]
	public void ReferenceHandlerThrowsOnSerializationTooForTypeClaimedOnlyByTheNotEmptyAttribute()
	{
		JsonSerializerOptions options = new()
		{
			ReferenceHandler = ReferenceHandler.Preserve,
			Converters = { new JsonRequiredConditionallyConverterFactory() },
		};

		Assert.ThrowsExactly<NotSupportedException>(
			() => JsonSerializer.Serialize(new NotEmptyStringConfig { Name = "a" }, options));
	}

	[TestMethod]
	public void PopulateObjectCreationHandlingThrowsForTypeClaimedOnlyByTheNotEmptyAttribute()
	{
		JsonSerializerOptions options = new() { Converters = { new JsonRequiredConditionallyConverterFactory() } };

		if (!TrySetPreferredObjectCreationHandlingToPopulate(options))
		{
			Assert.Inconclusive(PopulateUnavailable);
			return;
		}

		NotSupportedException exception = Assert.ThrowsExactly<NotSupportedException>(
			() => JsonSerializer.Deserialize<NotEmptyStringConfig>("""{"Name":"a"}""", options));

		StringAssert.Contains(exception.Message, "ktsu.JsonRequiredConditionally");
		StringAssert.Contains(exception.Message, "Populate");
	}

	[TestMethod]
	public void OptionsLevelPopulateDoesNotBreakSerialization()
	{
		JsonSerializerOptions options = CreateOptions();

		if (!TrySetPreferredObjectCreationHandlingToPopulate(options))
		{
			Assert.Inconclusive(PopulateUnavailable);
			return;
		}

		string json = JsonSerializer.Serialize(new SimpleConfig { Kind = Kind.Basic, Tuning = "x" }, options);

		StringAssert.Contains(json, "\"Tuning\":\"x\"");
	}

	[TestMethod]
	public void TypeLevelPopulateThrowsNamingThisLibrary()
	{
		if (!TryCreateTypeLevelPopulateOptions(out JsonSerializerOptions options))
		{
			Assert.Inconclusive(PopulateUnavailable);
			return;
		}

		NotSupportedException exception = Assert.ThrowsExactly<NotSupportedException>(
			() => JsonSerializer.Deserialize<PopulateGuardHolder>("""{"Kind":"Basic"}""", options));

		StringAssert.Contains(exception.Message, "ktsu.JsonRequiredConditionally");
		StringAssert.Contains(exception.Message, "Populate");
		StringAssert.Contains(exception.Message, "type-level");
	}

	[TestMethod]
	public void TypeLevelPopulateThrowsOnSerializationToo()
	{
		if (!TryCreateTypeLevelPopulateOptions(out JsonSerializerOptions options))
		{
			Assert.Inconclusive(PopulateUnavailable);
			return;
		}

		// Unlike the options-level and property-level routes, this one cannot spare the write path:
		// claiming the type makes its JsonTypeInfoKind None, and System.Text.Json then refuses to
		// apply the type-level attribute at all, in either direction. Throwing first is what
		// replaces its "Invalid JsonTypeInfo operation for JsonTypeInfoKind 'None'" with an
		// explanation.
		NotSupportedException exception = Assert.ThrowsExactly<NotSupportedException>(
			() => JsonSerializer.Serialize(new PopulateGuardHolder(), options));

		StringAssert.Contains(exception.Message, "ktsu.JsonRequiredConditionally");
	}

	[TestMethod]
	public void PropertyLevelPopulateThrowsNamingThisLibrary()
	{
		if (!TryCreatePropertyLevelPopulateOptions(out JsonSerializerOptions options))
		{
			Assert.Inconclusive(PopulateUnavailable);
			return;
		}

		NotSupportedException exception = Assert.ThrowsExactly<NotSupportedException>(
			() => JsonSerializer.Deserialize<PopulateGuardHolder>("""{"Kind":"Basic"}""", options));

		StringAssert.Contains(exception.Message, "ktsu.JsonRequiredConditionally");
		StringAssert.Contains(exception.Message, "property-level");
	}

	[TestMethod]
	public void PropertyLevelPopulateDoesNotBreakSerialization()
	{
		if (!TryCreatePropertyLevelPopulateOptions(out JsonSerializerOptions options))
		{
			Assert.Inconclusive(PopulateUnavailable);
			return;
		}

		string json = JsonSerializer.Serialize(new PopulateGuardHolder { Kind = Kind.Basic, Tuning = "x" }, options);

		StringAssert.Contains(json, "\"Tuning\":\"x\"");
	}

	[TestMethod]
	public void GetOnlyPropertyCountsAsPopulatedWhenItsDeclaringTypePrefersPopulate()
	{
		PropertyInfo? preferred = typeof(JsonTypeInfo).GetProperty("PreferredPropertyObjectCreationHandling");

		if (preferred is null)
		{
			Assert.Inconclusive(PopulateUnavailable);
			return;
		}

		JsonTypeInfo plain = new DefaultJsonTypeInfoResolver()
			.GetTypeInfo(typeof(GetOnlyChildHolder), new JsonSerializerOptions());
		JsonPropertyInfo child = plain.Properties.Single(property => property.Name == nameof(GetOnlyChildHolder.Child));

		// Without Populate the get-only property is not something deserialization reaches...
		Assert.IsFalse(RequirementRuleCompiler.IsPopulatedByDeserialization(plain, child));

		JsonTypeInfo populating = new DefaultJsonTypeInfoResolver()
			.GetTypeInfo(typeof(GetOnlyChildHolder), new JsonSerializerOptions());
		preferred.SetValue(populating, ParsePopulate(preferred));
		JsonPropertyInfo populatingChild =
			populating.Properties.Single(property => property.Name == nameof(GetOnlyChildHolder.Child));

		// ...but with it, System.Text.Json fills the instance the getter returns, so it is.
		Assert.IsTrue(RequirementRuleCompiler.IsPopulatedByDeserialization(populating, populatingChild));
	}

	private const string PopulateUnavailable =
		"JsonObjectCreationHandling does not exist in this target framework's System.Text.Json, so the unsupported configuration it guards cannot be expressed here.";

	/// <summary>
	/// Parses the <c>Populate</c> member of whatever <c>JsonObjectCreationHandling</c> enum this
	/// framework exposes, unwrapping the nullable the contract properties declare.
	/// </summary>
	/// <param name="property">The reflectively-resolved contract property.</param>
	/// <returns>The boxed <c>Populate</c> enum value.</returns>
	private static object ParsePopulate(PropertyInfo property) =>
		Enum.Parse(Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType, "Populate");

	/// <summary>
	/// Builds options whose resolver marks <see cref="PopulateGuardHolder"/> itself as preferring
	/// <c>Populate</c>, standing in for a type-level <c>[JsonObjectCreationHandling]</c>.
	/// </summary>
	/// <param name="options">The configured options, when this framework supports the feature.</param>
	/// <returns>True when the feature exists on this framework.</returns>
	private static bool TryCreateTypeLevelPopulateOptions(out JsonSerializerOptions options)
	{
		PropertyInfo? preferred = typeof(JsonTypeInfo).GetProperty("PreferredPropertyObjectCreationHandling");

		if (preferred is null)
		{
			options = new JsonSerializerOptions();
			return false;
		}

		object populate = ParsePopulate(preferred);
		DefaultJsonTypeInfoResolver resolver = new();

		// Kind is None for a type the factory has claimed, and System.Text.Json refuses the
		// assignment there -- which is precisely the breakage the guard exists to pre-empt, so the
		// modifier leaves that contract alone and lets the guard speak first.
		resolver.Modifiers.Add(info =>
		{
			if (info.Type == typeof(PopulateGuardHolder) && info.Kind != JsonTypeInfoKind.None)
			{
				preferred.SetValue(info, populate);
			}
		});

		options = BuildOptions(resolver);

		return true;
	}

	/// <summary>
	/// Builds options whose resolver marks <see cref="PopulateGuardHolder.Child"/> as preferring
	/// <c>Populate</c>, standing in for a property-level <c>[JsonObjectCreationHandling]</c>.
	/// </summary>
	/// <param name="options">The configured options, when this framework supports the feature.</param>
	/// <returns>True when the feature exists on this framework.</returns>
	private static bool TryCreatePropertyLevelPopulateOptions(out JsonSerializerOptions options)
	{
		PropertyInfo? handling = typeof(JsonPropertyInfo).GetProperty("ObjectCreationHandling");

		if (handling is null)
		{
			options = new JsonSerializerOptions();
			return false;
		}

		object populate = ParsePopulate(handling);
		DefaultJsonTypeInfoResolver resolver = new();

		resolver.Modifiers.Add(info =>
		{
			if (info.Type != typeof(PopulateGuardHolder))
			{
				return;
			}

			foreach (JsonPropertyInfo property in info.Properties)
			{
				if (property.Name == nameof(PopulateGuardHolder.Child))
				{
					handling.SetValue(property, populate);
				}
			}
		});

		options = BuildOptions(resolver);

		return true;
	}

	private static JsonSerializerOptions BuildOptions(DefaultJsonTypeInfoResolver resolver) =>
		new()
		{
			TypeInfoResolver = resolver,
			Converters = { new JsonStringEnumConverter(), new JsonRequiredConditionallyConverterFactory() },
		};

	/// <summary>
	/// Sets <c>PreferredObjectCreationHandling</c> reflectively, because the property was introduced
	/// after net7.0's in-box System.Text.Json and this test project is compiled once against every
	/// target framework in the matrix.
	/// </summary>
	/// <param name="options">The options to configure.</param>
	/// <returns>True when the property exists on this framework and was set.</returns>
	private static bool TrySetPreferredObjectCreationHandlingToPopulate(JsonSerializerOptions options)
	{
		PropertyInfo? property = typeof(JsonSerializerOptions).GetProperty("PreferredObjectCreationHandling");

		if (property is null || !property.CanWrite)
		{
			return false;
		}

		property.SetValue(options, Enum.Parse(property.PropertyType, "Populate"));

		return true;
	}
}
