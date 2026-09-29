// Copyright (c) 2023-2026 ktsu-dev contributors

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

	/// <summary>
	/// Gets a value indicating whether a JSON <c>null</c> cannot stand for <typeparamref name="T"/>,
	/// because it is a value type that is not <see cref="Nullable{T}"/>.
	/// </summary>
	private static bool NullIsInvalid { get; } = typeof(T).IsValueType && Nullable.GetUnderlyingType(typeof(T)) is null;

	/// <inheritdoc/>
	/// <exception cref="JsonException">
	/// The JSON is <c>null</c> and <typeparamref name="T"/> is a non-nullable value type.
	/// </exception>
	/// <exception cref="NotSupportedException">
	/// <paramref name="options"/> or <typeparamref name="T"/> configure
	/// <c>JsonObjectCreationHandling.Populate</c>, which this library cannot validate through.
	/// </exception>
	public override T? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
	{
		// Populate affects deserialization only, so it is refused here rather than at claim time:
		// an options instance used purely for serialization must keep working. System.Text.Json
		// calls Read directly, so this exception reaches the caller unwrapped.
		SerializerFeatureGuard.EnsureCanRead(typeof(T), userOptions);

		if (reader.TokenType == JsonTokenType.Null)
		{
			// System.Text.Json hands null to a value-type converter rather than rejecting it first,
			// so returning default here would accept what it rejects without this library. A
			// message-less JsonException gets System.Text.Json's own "could not be converted" text
			// and path, which keeps the failure identical to the unregistered case.
			if (NullIsInvalid)
			{
				throw new JsonException();
			}

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
