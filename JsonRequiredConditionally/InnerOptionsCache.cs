// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace ktsu.JsonRequiredConditionally;

using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;

/// <summary>
/// Caches the per-type options clones used to materialize objects without converter re-entrancy.
/// </summary>
internal static class InnerOptionsCache
{
#pragma warning disable IDE0028 // Collection expression not constructible for ConditionalWeakTable on netstandard2.0
	private static readonly ConditionalWeakTable<JsonSerializerOptions, ConcurrentDictionary<Type, JsonSerializerOptions>> Cache = new();
#pragma warning restore IDE0028

	/// <summary>
	/// Gets the options used to materialize <paramref name="excludedType"/> without re-entering its own converter.
	/// </summary>
	/// <param name="rootOptions">The user's original options, shared by every frame.</param>
	/// <param name="excludedType">The type whose converter must be bypassed.</param>
	/// <param name="factory">The root factory, used for every other type.</param>
	/// <returns>A cached options instance.</returns>
	internal static JsonSerializerOptions Get(
		JsonSerializerOptions rootOptions,
		Type excludedType,
		JsonRequiredConditionallyConverterFactory factory)
	{
		ConcurrentDictionary<Type, JsonSerializerOptions> perType =
			Cache.GetValue(rootOptions, static _ => new ConcurrentDictionary<Type, JsonSerializerOptions>());

		return perType.GetOrAdd(excludedType, type => Build(rootOptions, type, factory));
	}

	/// <summary>
	/// Finds the root options for a frame by looking for a marker factory in the current options.
	/// </summary>
	/// <param name="options">The options a converter was created with.</param>
	/// <returns>The user's original options.</returns>
	internal static JsonSerializerOptions FindRoot(JsonSerializerOptions options)
	{
		foreach (JsonConverter converter in options.Converters)
		{
			if (converter is ExcludingFactory excluding)
			{
				return excluding.RootOptions;
			}
		}

		return options;
	}

	private static JsonSerializerOptions Build(
		JsonSerializerOptions rootOptions,
		Type excludedType,
		JsonRequiredConditionallyConverterFactory factory)
	{
		JsonSerializerOptions inner = new(rootOptions);

		for (int i = inner.Converters.Count - 1; i >= 0; i--)
		{
			if (inner.Converters[i] is JsonRequiredConditionallyConverterFactory or ExcludingFactory)
			{
				inner.Converters.RemoveAt(i);
			}
		}

		inner.Converters.Add(new ExcludingFactory(excludedType, factory, rootOptions));

		return inner;
	}
}
