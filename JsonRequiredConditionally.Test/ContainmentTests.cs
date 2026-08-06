// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace ktsu.JsonRequiredConditionally.Tests;

using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

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
