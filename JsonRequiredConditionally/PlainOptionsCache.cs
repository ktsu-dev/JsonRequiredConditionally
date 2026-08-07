// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.JsonRequiredConditionally;

using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

/// <summary>
/// Caches, per options instance, a clone with this library's factory removed. Materializing through
/// the clone is what stops the converter re-entering itself.
/// </summary>
internal static class PlainOptionsCache
{
	[SuppressMessage("Style", "IDE0028:Collection initialization can be simplified", Justification = "A collection expression does not compile for ConditionalWeakTable on netstandard2.0.")]
	private static readonly ConditionalWeakTable<JsonSerializerOptions, JsonSerializerOptions> Cache = new();

	/// <summary>
	/// Gets the factory-free clone of the given options.
	/// </summary>
	/// <param name="options">The options a converter was created with.</param>
	/// <returns>A cached clone carrying every setting except this library's factory.</returns>
	internal static JsonSerializerOptions Get(JsonSerializerOptions options)
	{
		Ensure.NotNull(options);

		return Cache.GetValue(options, Build);
	}

	private static JsonSerializerOptions Build(JsonSerializerOptions options)
	{
		JsonSerializerOptions plain = new(options);

		for (int i = plain.Converters.Count - 1; i >= 0; i--)
		{
			if (plain.Converters[i] is JsonRequiredConditionallyConverterFactory)
			{
				plain.Converters.RemoveAt(i);
			}
		}

		// GraphValidator asks this clone for its own JsonTypeInfo model. An options instance whose
		// resolver has never been consulted throws when asked directly (as opposed to going through
		// JsonSerializer.Deserialize, which resolves one implicitly on first use) -- setting this
		// eagerly makes that available immediately rather than depending on Read's own Deserialize
		// call happening to run first. ??= preserves any resolver (e.g. a source-generated context)
		// the caller already configured.
		plain.TypeInfoResolver ??= new DefaultJsonTypeInfoResolver();

		return plain;
	}
}
