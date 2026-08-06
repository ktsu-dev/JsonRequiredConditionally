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
	private readonly JsonSerializerOptions innerOptions;
	private readonly RequirementRule[] rules;
	private readonly StringComparer nameComparer;

	/// <summary>
	/// Initializes a new instance of the <see cref="JsonRequiredConditionallyConverter{T}"/> class.
	/// </summary>
	/// <param name="options">The options this converter was created for.</param>
	/// <param name="factory">The factory that created this converter.</param>
	internal JsonRequiredConditionallyConverter(JsonSerializerOptions options, JsonRequiredConditionallyConverterFactory factory)
	{
		Ensure.NotNull(options);
		Ensure.NotNull(factory);

		rules = RequirementRuleCompiler.Compile(typeof(T), options);
		nameComparer = options.PropertyNameCaseInsensitive ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
		innerOptions = InnerOptionsCache.Get(InnerOptionsCache.FindRoot(options), typeof(T), factory);
	}

	/// <inheritdoc/>
	public override T? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
	{
		if (reader.TokenType == JsonTokenType.Null)
		{
			return default;
		}

		HashSet<string> present = PresenceScanner.ScanPropertyNames(reader, nameComparer);

		T? value = JsonSerializer.Deserialize<T>(ref reader, innerOptions);

		if (value is not null)
		{
			Validate(value, present);
		}

		return value;
	}

	/// <inheritdoc/>
	public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options) =>
		JsonSerializer.Serialize(writer, value, innerOptions);

	private void Validate(T value, HashSet<string> present)
	{
		List<string>? missing = null;

		foreach (RequirementRule rule in rules)
		{
			if (present.Contains(rule.JsonName))
			{
				continue;
			}

			if (!rule.IsRequiredFor(value!))
			{
				continue;
			}

			missing ??= [];
			missing.Add(rule.JsonName);
		}

		if (missing is not null)
		{
			throw new JsonRequiredConditionallyException(missing);
		}
	}
}
