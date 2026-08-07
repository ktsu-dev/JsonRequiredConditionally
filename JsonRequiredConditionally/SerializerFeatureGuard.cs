// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.JsonRequiredConditionally;

using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

/// <summary>
/// Rejects the serializer configurations this library cannot model.
/// </summary>
/// <remarks>
/// Because the converter materializes its subtree through factory-free options, the graph walk is
/// the <em>sole</em> validator inside that subtree. Any serializer feature that changes what the
/// materialized graph means -- and which <see cref="JsonTypeInfo.Properties"/> does not describe --
/// therefore turns into either a silent non-validation or a false positive. These checks convert
/// that into a loud, named error instead.
/// <para>
/// The checks are split by the path they actually affect. <see cref="EnsureCanClaim"/> runs in
/// <see cref="JsonRequiredConditionallyConverterFactory.CreateConverter"/> and covers the features
/// that break both directions; <see cref="EnsureCanRead"/> runs in the converter's <c>Read</c> and
/// covers the deserialization-only ones, so an options instance used purely for serialization is
/// not broken by a feature that cannot affect it.
/// </para>
/// <para>
/// Neither runs in the converter's constructor on purpose: <see cref="Activator.CreateInstance(Type, BindingFlags, Binder, object[], System.Globalization.CultureInfo)"/>
/// wraps anything a constructor throws in a <see cref="TargetInvocationException"/>, and a caller
/// should see the <see cref="NotSupportedException"/> that was actually thrown. System.Text.Json
/// calls <c>Read</c> directly rather than through reflection, so throwing from there is unwrapped.
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
	/// This property, <c>JsonTypeInfo.PreferredPropertyObjectCreationHandling</c>,
	/// <c>JsonPropertyInfo.ObjectCreationHandling</c> and the <c>JsonObjectCreationHandling</c> enum
	/// they use were all introduced after net7.0's in-box System.Text.Json, and this library targets
	/// net7.0 without conditional compilation. Reflection is the portable route: on a framework where
	/// the property genuinely does not exist, the configuration it guards cannot be expressed either,
	/// so skipping the check there is correct rather than merely convenient.
	/// </remarks>
	private static readonly PropertyInfo? PreferredObjectCreationHandling =
		typeof(JsonSerializerOptions).GetProperty("PreferredObjectCreationHandling");

	/// <summary>
	/// <c>JsonTypeInfo.PreferredPropertyObjectCreationHandling</c>, resolved reflectively for the
	/// same reason as <see cref="PreferredObjectCreationHandling"/>. Set by a type-level
	/// <c>[JsonObjectCreationHandling]</c> attribute or by a resolver modifier.
	/// </summary>
	private static readonly PropertyInfo? PreferredPropertyObjectCreationHandling =
		typeof(JsonTypeInfo).GetProperty("PreferredPropertyObjectCreationHandling");

	/// <summary>
	/// <c>JsonPropertyInfo.ObjectCreationHandling</c>, resolved reflectively for the same reason as
	/// <see cref="PreferredObjectCreationHandling"/>. Set by a property-level
	/// <c>[JsonObjectCreationHandling]</c> attribute.
	/// </summary>
	private static readonly PropertyInfo? ObjectCreationHandling =
		typeof(JsonPropertyInfo).GetProperty("ObjectCreationHandling");

	/// <summary>
	/// Throws if claiming this type would break it on the write path as well as the read path.
	/// </summary>
	/// <param name="typeToConvert">The type about to be claimed.</param>
	/// <param name="options">The caller's own options.</param>
	/// <exception cref="NotSupportedException">The options or the type configure an unsupported feature.</exception>
	/// <remarks>
	/// <see cref="JsonSerializerOptions.ReferenceHandler"/> is checked here, on both directions,
	/// rather than on read alone. Writing is in fact correct on its own -- <c>Write</c> delegates
	/// through options that retain the handler -- but a document this library emitted with
	/// <c>$id</c>/<c>$ref</c>/<c>$values</c> is one it would then reject or mis-validate on read.
	/// Refusing to write it is what stops the library producing input it cannot itself consume.
	/// <para>
	/// A <em>type-level</em> <c>[JsonObjectCreationHandling]</c> is checked here too, unlike the
	/// options-level and property-level routes which are deserialization-only and live in
	/// <see cref="EnsureCanRead"/>. Claiming such a type gives it a converter, which makes its
	/// <c>JsonTypeInfoKind</c> <c>None</c>, and System.Text.Json then refuses to apply the
	/// attribute to a contract that has no property model -- <c>InvalidOperationException: Invalid
	/// JsonTypeInfo operation for JsonTypeInfoKind 'None'</c>, on read and on write alike, an error
	/// that names neither this library nor the real cause. The converter is resolved before the
	/// attribute is applied, so throwing here happens first and the caller sees an explanation
	/// instead. There is no working write path being sacrificed: claiming the type is itself what
	/// breaks it.
	/// </para>
	/// </remarks>
	internal static void EnsureCanClaim(Type typeToConvert, JsonSerializerOptions options)
	{
		if (options.ReferenceHandler is not null)
		{
			throw new NotSupportedException(
				$"{LibraryName} does not support JsonSerializerOptions.ReferenceHandler. The converter buffers each claimed subtree and re-materializes it, and the buffered JSON of a '$ref' node carries none of the referenced object's properties -- so every armed requirement on it is reported missing, while a '$values' array arrives as a JSON object the walk cannot descend into at all. Serialization is refused as well as deserialization, so this library cannot emit a document it would then reject on read. {Remedy}");
		}

		if (PreferredPropertyObjectCreationHandling is null)
		{
			return;
		}

		JsonTypeInfo? typeInfo = RequirementRuleCompiler.TryGetTypeInfo(PlainOptionsCache.Get(options), typeToConvert);

		if (PrefersPopulate(PreferredPropertyObjectCreationHandling, typeInfo))
		{
			throw new NotSupportedException(
				BuildPopulateMessage($"a type-level [JsonObjectCreationHandling] on '{typeToConvert.Name}'"));
		}
	}

	/// <summary>
	/// Throws if the given options or type configure an object-creation mode this library cannot
	/// validate through. Deserialization only -- <c>JsonObjectCreationHandling.Populate</c> has no
	/// effect on writing.
	/// </summary>
	/// <param name="typeToConvert">The type being read.</param>
	/// <param name="options">The caller's own options.</param>
	/// <exception cref="NotSupportedException">Populate is configured at the options or property level.</exception>
	/// <remarks>
	/// Both routes throw, even though only the options-level one is strictly impossible to model.
	/// Eligibility is decided from a <see cref="Type"/> alone, with no options in hand, so a holder
	/// whose object-creation mode comes from the options can never be claimed and its data loss can
	/// never be prevented. Supporting the property-level route while refusing the options-level one
	/// would make the failure mode depend on which route the caller happened to choose, which is
	/// worse than refusing both consistently.
	/// </remarks>
	internal static void EnsureCanRead(Type typeToConvert, JsonSerializerOptions options)
	{
		if (PrefersPopulate(PreferredObjectCreationHandling, options))
		{
			throw new NotSupportedException(BuildPopulateMessage("JsonSerializerOptions.PreferredObjectCreationHandling"));
		}

		if (ObjectCreationHandling is null)
		{
			return;
		}

		JsonTypeInfo? typeInfo = RequirementRuleCompiler.TryGetTypeInfo(PlainOptionsCache.Get(options), typeToConvert);

		if (typeInfo is null)
		{
			return;
		}

		foreach (JsonPropertyInfo property in typeInfo.Properties)
		{
			if (PrefersPopulate(ObjectCreationHandling, property))
			{
				throw new NotSupportedException(
					BuildPopulateMessage($"a property-level [JsonObjectCreationHandling] on '{typeToConvert.Name}.{property.Name}'"));
			}
		}
	}

	/// <summary>
	/// Reports whether System.Text.Json would populate an existing instance through this property
	/// rather than assigning a newly constructed one, which makes a get-only property something
	/// deserialization does reach.
	/// </summary>
	/// <param name="declaringTypeInfo">The contract of the type declaring the property.</param>
	/// <param name="property">The property to check.</param>
	/// <returns>True when the property or its declaring type prefers <c>Populate</c>.</returns>
	/// <remarks>
	/// The options-level default is deliberately not consulted here. This question is asked during
	/// eligibility, which is decided from a <see cref="Type"/> with no options available, so
	/// consulting the options would make the answer depend on state the caller cannot see at that
	/// point. The options-level route is instead refused outright by <see cref="EnsureCanRead"/>.
	/// </remarks>
	internal static bool PopulatesInPlace(JsonTypeInfo declaringTypeInfo, JsonPropertyInfo property) =>
		PrefersPopulate(ObjectCreationHandling, property) ||
		PrefersPopulate(PreferredPropertyObjectCreationHandling, declaringTypeInfo);

	private static string BuildPopulateMessage(string source) =>
		$"{LibraryName} does not support JsonObjectCreationHandling.Populate, configured here by {source}. The converter re-materializes each claimed subtree into a fresh instance, so the object System.Text.Json intended to populate is discarded together with anything already in it. {Remedy}";

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
