// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace ktsu.JsonRequiredConditionally;

using System.Reflection;
using System.Runtime.ExceptionServices;
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
	/// <inheritdoc/>
	public override bool CanConvert(Type typeToConvert)
	{
		Ensure.NotNull(typeToConvert);

		return RequirementRuleCompiler.HasRules(typeToConvert);
	}

	/// <inheritdoc/>
	public override JsonConverter? CreateConverter(Type typeToConvert, JsonSerializerOptions options)
	{
		Ensure.NotNull(typeToConvert);
		Ensure.NotNull(options);

		Type converterType = typeof(JsonRequiredConditionallyConverter<>).MakeGenericType(typeToConvert);

		try
		{
			return (JsonConverter?)Activator.CreateInstance(
				converterType,
				BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public,
				binder: null,
				args: [options, this],
				culture: null);
		}
		catch (TargetInvocationException exception) when (exception.InnerException is not null)
		{
			ExceptionDispatchInfo.Capture(exception.InnerException).Throw();
			throw;
		}
	}
}
