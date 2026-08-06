// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace ktsu.JsonRequiredConditionally;

using System.Text.Json;

/// <summary>
/// Thrown when one or more properties were required by their sibling values but were absent
/// from the JSON payload.
/// </summary>
public sealed class JsonRequiredConditionallyException : JsonException
{
	/// <summary>
	/// Initializes a new instance of the <see cref="JsonRequiredConditionallyException"/> class.
	/// </summary>
	public JsonRequiredConditionallyException()
		: base() => MissingProperties = [];

	/// <summary>
	/// Initializes a new instance of the <see cref="JsonRequiredConditionallyException"/> class.
	/// </summary>
	/// <param name="message">The message that describes the error.</param>
	public JsonRequiredConditionallyException(string message)
		: base(message) => MissingProperties = [];

	/// <summary>
	/// Initializes a new instance of the <see cref="JsonRequiredConditionallyException"/> class.
	/// </summary>
	/// <param name="message">The message that describes the error.</param>
	/// <param name="innerException">The exception that caused this exception.</param>
	public JsonRequiredConditionallyException(string message, Exception innerException)
		: base(message, innerException) => MissingProperties = [];

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
		: base(BuildMessage(missingProperties)) => MissingProperties = [.. missingProperties];

	/// <summary>
	/// Gets the JSON names of the properties that were required but absent from the payload.
	/// </summary>
	public IReadOnlyList<string> MissingProperties { get; }

	private static string BuildMessage(IReadOnlyList<string> missingProperties)
	{
		Ensure.NotNull(missingProperties);

		string names = string.Join("', '", missingProperties);

		return $"The following properties are required by their sibling values but were absent from the JSON payload: '{names}'.";
	}
}
