// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace ktsu.JsonRequiredConditionally;

using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

/// <summary>
/// Rejects the serializer configurations this library cannot model, before it claims anything.
/// </summary>
/// <remarks>
/// Because the converter materializes its subtree through factory-free options, the graph walk is
/// the <em>sole</em> validator inside that subtree. Any serializer feature that changes what the
/// materialized graph means -- and which <see cref="JsonTypeInfo.Properties"/> does not describe --
/// therefore turns into either a silent non-validation or a false positive. These checks convert
/// that into a loud, named error instead.
/// <para>
/// They run in <see cref="JsonRequiredConditionallyConverterFactory.CreateConverter"/> rather than in
/// the converter's constructor on purpose: <see cref="Activator.CreateInstance(Type, BindingFlags, Binder, object[], System.Globalization.CultureInfo)"/>
/// wraps anything a constructor throws in a <see cref="TargetInvocationException"/>, and a caller
/// should see the <see cref="NotSupportedException"/> that was actually thrown.
/// </para>
/// </remarks>
internal static class SerializerFeatureGuard
{
	private const string LibraryName = "ktsu.JsonRequiredConditionally";

	private const string Remedy =
		"Remove JsonRequiredConditionallyConverterFactory from JsonSerializerOptions.Converters, or stop using this feature.";

	/// <summary>
	/// The name System.Text.Json gives the "populate the existing instance" object-creation mode.
	/// Matched by name rather than by numeric value so the check does not depend on the enum's
	/// layout, which this library cannot reference directly on every target framework.
	/// </summary>
	private const string PopulateHandlingName = "Populate";

	/// <summary>
	/// <c>JsonSerializerOptions.PreferredObjectCreationHandling</c>, resolved reflectively.
	/// </summary>
	/// <remarks>
	/// The property and the <c>JsonObjectCreationHandling</c> enum it returns were introduced after
	/// net7.0's in-box System.Text.Json, and this library targets net7.0 without conditional
	/// compilation. Reflection is the portable route: on a framework where the property genuinely
	/// does not exist, the configuration it guards cannot be expressed either, so skipping the check
	/// there is correct rather than merely convenient.
	/// </remarks>
	private static readonly PropertyInfo? PreferredObjectCreationHandling =
		typeof(JsonSerializerOptions).GetProperty("PreferredObjectCreationHandling");

	/// <summary>
	/// <c>JsonTypeInfo.PreferredPropertyObjectCreationHandling</c>, resolved reflectively for the
	/// same reason as <see cref="PreferredObjectCreationHandling"/>.
	/// </summary>
	private static readonly PropertyInfo? PreferredPropertyObjectCreationHandling =
		typeof(JsonTypeInfo).GetProperty("PreferredPropertyObjectCreationHandling");

	/// <summary>
	/// Throws if the given options configure a feature this library cannot validate through.
	/// </summary>
	/// <param name="typeToConvert">The type about to be claimed.</param>
	/// <param name="options">The caller's own options.</param>
	/// <exception cref="NotSupportedException">The options configure an unsupported feature.</exception>
	internal static void EnsureSupported(Type typeToConvert, JsonSerializerOptions options)
	{
		if (options.ReferenceHandler is not null)
		{
			throw new NotSupportedException(
				$"{LibraryName} does not support JsonSerializerOptions.ReferenceHandler. The converter buffers each claimed subtree and re-materializes it, and the buffered JSON of a '$ref' node carries none of the referenced object's properties -- so every armed requirement on it is reported missing, while a '$values' array arrives as a JSON object the walk cannot descend into at all. {Remedy}");
		}

		if (PrefersPopulate(PreferredObjectCreationHandling, options))
		{
			throw new NotSupportedException(BuildPopulateMessage("JsonSerializerOptions.PreferredObjectCreationHandling"));
		}

		if (PreferredPropertyObjectCreationHandling is not null &&
			PrefersPopulate(
				PreferredPropertyObjectCreationHandling,
				RequirementRuleCompiler.TryGetTypeInfo(PlainOptionsCache.Get(options), typeToConvert)))
		{
			throw new NotSupportedException(
				BuildPopulateMessage($"JsonTypeInfo.PreferredPropertyObjectCreationHandling for '{typeToConvert.Name}'"));
		}
	}

	private static string BuildPopulateMessage(string source) =>
		$"{LibraryName} does not support JsonObjectCreationHandling.Populate, configured here by {source}. The converter re-materializes each claimed subtree into a fresh instance, so the object System.Text.Json intended to populate is discarded together with anything already in it, and a get-only property that Populate does fill is skipped by the walk as if it were never populated. {Remedy}";

	/// <summary>
	/// Reads a reflectively-resolved object-creation-handling property and reports whether it is set
	/// to <c>Populate</c>.
	/// </summary>
	/// <param name="property">The property to read, or null when this framework does not have it.</param>
	/// <param name="target">The instance to read it from, or null when there is none.</param>
	/// <returns>True when the property exists and reads as <c>Populate</c>.</returns>
	private static bool PrefersPopulate(PropertyInfo? property, object? target)
	{
		if (property is null || target is null)
		{
			return false;
		}

		object? value = property.GetValue(target);

		return value is not null && string.Equals(value.ToString(), PopulateHandlingName, StringComparison.Ordinal);
	}
}
