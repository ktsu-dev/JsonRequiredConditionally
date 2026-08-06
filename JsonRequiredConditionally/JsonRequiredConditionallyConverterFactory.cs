// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace ktsu.JsonRequiredConditionally;

using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

/// <summary>
/// Creates converters that enforce <see cref="JsonRequiredIfSiblingIsAttribute"/> during deserialization.
/// </summary>
/// <remarks>
/// Add one instance to <see cref="JsonSerializerOptions.Converters"/>. Types with no decorated
/// members are not claimed and keep the serializer's normal fast path.
/// </remarks>
public sealed class JsonRequiredConditionallyConverterFactory : JsonConverterFactory
{
	/// <summary>
	/// Initializes a new instance of the <see cref="JsonRequiredConditionallyConverterFactory"/> class.
	/// </summary>
	/// <remarks>
	/// The trim and ahead-of-time annotations sit on the constructor rather than on the type or on
	/// the overridden members: <c>CanConvert</c> and <c>CreateConverter</c> are overrides whose base
	/// declarations on <see cref="JsonConverterFactory"/> carry no such annotation, so annotating
	/// them would itself be a mismatch. Constructing the factory is the one thing a consumer does
	/// explicitly, so it is the smallest place that still produces the warning.
	/// </remarks>
	[SuppressMessage("Style", "IDE0290:Use primary constructor", Justification = "The constructor exists solely to carry the trim and ahead-of-time annotations below; a primary constructor could only express them through obscure [method:] targeting.")]
	[RequiresUnreferencedCode("ktsu.JsonRequiredConditionally discovers members reflectively, through DefaultJsonTypeInfoResolver and Type.GetProperty/GetField, so trimming can remove members it needs to validate and it cannot be used in a trimmed application.")]
	[RequiresDynamicCode("ktsu.JsonRequiredConditionally builds its converter with Type.MakeGenericType and Activator.CreateInstance, which need runtime code generation and are not available under ahead-of-time compilation.")]
	public JsonRequiredConditionallyConverterFactory()
	{
	}

	/// <inheritdoc/>
	public override bool CanConvert(Type typeToConvert)
	{
		Ensure.NotNull(typeToConvert);

		return RequirementRuleCompiler.HasRules(typeToConvert);
	}

	/// <inheritdoc/>
	/// <exception cref="NotSupportedException">
	/// <paramref name="options"/> configures a serializer feature this library cannot validate
	/// through -- see <see cref="SerializerFeatureGuard"/>.
	/// </exception>
	public override JsonConverter? CreateConverter(Type typeToConvert, JsonSerializerOptions options)
	{
		Ensure.NotNull(typeToConvert);
		Ensure.NotNull(options);

		// Deliberately before Activator.CreateInstance rather than inside the converter's
		// constructor: anything a constructor throws from there arrives wrapped in a
		// TargetInvocationException, and this exception is meant for the caller to read.
		// Only the features that break writing as well as reading are checked here; the
		// deserialization-only ones are checked in the converter's Read.
		SerializerFeatureGuard.EnsureCanClaim(typeToConvert, options);

		Type converterType = typeof(JsonRequiredConditionallyConverter<>).MakeGenericType(typeToConvert);

		return (JsonConverter?)Activator.CreateInstance(
			converterType,
			BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public,
			binder: null,
			args: [options, this],
			culture: null);
	}
}
