// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.JsonRequiredConditionally;

using System.Text.Json;

/// <summary>
/// Thrown when one or more properties failed a requirement declared by this library: absent from the
/// JSON payload when a sibling value required them, or present but empty when
/// <see cref="JsonRequiredAndNotEmptyAttribute"/> required content.
/// </summary>
public sealed class JsonRequiredConditionallyException : JsonException
{
	/// <summary>
	/// Initializes a new instance of the <see cref="JsonRequiredConditionallyException"/> class.
	/// </summary>
	public JsonRequiredConditionallyException()
		: base()
	{
		MissingProperties = [];
		EmptyProperties = [];
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="JsonRequiredConditionallyException"/> class.
	/// </summary>
	/// <param name="message">The message that describes the error.</param>
	public JsonRequiredConditionallyException(string message)
		: base(message)
	{
		MissingProperties = [];
		EmptyProperties = [];
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="JsonRequiredConditionallyException"/> class.
	/// </summary>
	/// <param name="message">The message that describes the error.</param>
	/// <param name="innerException">The exception that caused this exception.</param>
	public JsonRequiredConditionallyException(string message, Exception innerException)
		: base(message, innerException)
	{
		MissingProperties = [];
		EmptyProperties = [];
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="JsonRequiredConditionallyException"/> class.
	/// </summary>
	/// <param name="missingProperties">The JSON names of the properties that were required but absent.</param>
	/// <remarks>
	/// The list is copied rather than stored: <see cref="IReadOnlyList{T}"/> is read-only only as
	/// seen through this interface, so a caller passing a <see cref="List{T}"/> could otherwise keep
	/// mutating the exception's own state after it was thrown, leaving
	/// <see cref="MissingProperties"/> disagreeing with <see cref="Exception.Message"/>.
	/// </remarks>
	public JsonRequiredConditionallyException(IReadOnlyList<string> missingProperties)
		: base(BuildMessage(missingProperties))
	{
		MissingProperties = [.. missingProperties];
		EmptyProperties = [];
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="JsonRequiredConditionallyException"/> class.
	/// </summary>
	/// <param name="missingProperties">The JSON names of the properties that were required but absent.</param>
	/// <param name="emptyProperties">The JSON names of the properties that were present but empty.</param>
	/// <remarks>
	/// Both lists are copied rather than stored, for the reason documented on the single-list
	/// constructor.
	/// </remarks>
	public JsonRequiredConditionallyException(IReadOnlyList<string> missingProperties, IReadOnlyList<string> emptyProperties)
		: base(BuildMessage(missingProperties, emptyProperties))
	{
		MissingProperties = [.. missingProperties];
		EmptyProperties = [.. emptyProperties];
	}

	/// <summary>
	/// Gets the JSON names of the properties that were required but absent from the payload.
	/// </summary>
	public IReadOnlyList<string> MissingProperties { get; }

	/// <summary>
	/// Gets the JSON names of the properties that were present in the payload but carried an empty
	/// value.
	/// </summary>
	/// <remarks>
	/// A property that was absent entirely is reported in <see cref="MissingProperties"/>, not here,
	/// even when it was decorated with <see cref="JsonRequiredAndNotEmptyAttribute"/>.
	/// </remarks>
	public IReadOnlyList<string> EmptyProperties { get; }

	private static string BuildMessage(IReadOnlyList<string> missingProperties)
	{
		Ensure.NotNull(missingProperties);

		string names = string.Join("', '", missingProperties);

		return $"The following properties were required but were absent from the JSON payload: '{names}'.";
	}

	private static string BuildMessage(IReadOnlyList<string> missingProperties, IReadOnlyList<string> emptyProperties)
	{
		Ensure.NotNull(missingProperties);
		Ensure.NotNull(emptyProperties);

		// Delegating when there is nothing empty keeps the single-category message byte-identical to
		// what the single-list constructor has always produced, so callers asserting on it keep working.
		if (emptyProperties.Count == 0)
		{
			return BuildMessage(missingProperties);
		}

		string names = string.Join("', '", emptyProperties);
		string emptyClause = $"The following properties were required to be non-empty but were empty: '{names}'.";

		return missingProperties.Count == 0
			? emptyClause
			: BuildMessage(missingProperties) + " " + emptyClause;
	}
}
