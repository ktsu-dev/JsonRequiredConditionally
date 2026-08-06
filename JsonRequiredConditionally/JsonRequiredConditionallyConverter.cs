// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace ktsu.JsonRequiredConditionally;

using System.Text.Json;
using System.Text.Json.Serialization;

/// <summary>
/// Validates conditional requirements for <typeparamref name="T"/> during deserialization.
/// </summary>
/// <typeparam name="T">The type being converted.</typeparam>
internal sealed class JsonRequiredConditionallyConverter<T> : JsonConverter<T>
{
	private readonly JsonSerializerOptions plainOptions;
	private readonly JsonSerializerOptions userOptions;

	/// <summary>
	/// Initializes a new instance of the <see cref="JsonRequiredConditionallyConverter{T}"/> class.
	/// </summary>
	/// <param name="options">The options this converter was created for.</param>
	/// <param name="factory">The factory that created this converter.</param>
	internal JsonRequiredConditionallyConverter(JsonSerializerOptions options, JsonRequiredConditionallyConverterFactory factory)
	{
		Ensure.NotNull(options);
		Ensure.NotNull(factory);

		userOptions = options;
		plainOptions = PlainOptionsCache.Get(options);
	}

	/// <inheritdoc/>
	public override T? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
	{
		if (reader.TokenType == JsonTokenType.Null)
		{
			return default;
		}

		using JsonDocument document = JsonDocument.ParseValue(ref reader);

		T? value = JsonSerializer.Deserialize<T>(document.RootElement.GetRawText(), plainOptions);

		if (value is not null)
		{
			GraphValidator.Validate(document.RootElement, value, plainOptions, userOptions);
		}

		return value;
	}

	/// <inheritdoc/>
	public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options) =>
		JsonSerializer.Serialize(writer, value, plainOptions);
}
