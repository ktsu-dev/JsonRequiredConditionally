// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.JsonRequiredConditionally.Tests;

using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

/// <summary>
/// Covers input that only the caller's own reader settings accept: comments under
/// <see cref="JsonCommentHandling.Skip"/> and trailing commas under
/// <see cref="JsonReaderOptions.AllowTrailingCommas"/>. Registering the factory must not start
/// rejecting it.
/// </summary>
[TestClass]
public class ReaderOptionsTests
{
	private static readonly JsonReaderOptions SkipComments = new() { CommentHandling = JsonCommentHandling.Skip };
	private static readonly JsonReaderOptions TrailingCommas = new() { AllowTrailingCommas = true };
	private static readonly JsonSerializerOptions UnregisteredOptions = new() { Converters = { new JsonStringEnumConverter() } };

	private static T? DeserializeThroughReader<T>(string json, JsonReaderOptions readerOptions)
	{
		Utf8JsonReader reader = new(Encoding.UTF8.GetBytes(json), readerOptions);
		return JsonSerializer.Deserialize<T>(ref reader, ConverterTests.CreateOptions());
	}

	/// <summary>
	/// Deserializes <paramref name="json"/> through a <see cref="JsonElement"/> parsed with
	/// <paramref name="readerOptions"/>, and describes the outcome as the bound value or the error.
	/// </summary>
	private static string DeserializeThroughElement(string json, JsonReaderOptions readerOptions, JsonSerializerOptions options)
	{
		using JsonDocument document = JsonDocument.Parse(json, new JsonDocumentOptions
		{
			CommentHandling = readerOptions.CommentHandling,
			AllowTrailingCommas = readerOptions.AllowTrailingCommas,
		});

		try
		{
			return $"value: {document.RootElement.Deserialize<SimpleConfig>(options)?.Tuning}";
		}
		catch (JsonException exception)
		{
			return $"{exception.GetType().Name}: {exception.Message}";
		}
	}

	/// <summary>
	/// System.Text.Json re-reads an element's raw text under the serializer's own reader settings,
	/// so whether this input is accepted depends on its version, not on this library. Registering
	/// the factory must not change the outcome either way.
	/// </summary>
	private static void AssertElementOutcomeUnchangedByFactory(string json, JsonReaderOptions readerOptions) =>
		Assert.AreEqual(
			DeserializeThroughElement(json, readerOptions, UnregisteredOptions),
			DeserializeThroughElement(json, readerOptions, ConverterTests.CreateOptions()));

	[TestMethod]
	public void CommentInDecoratedRootIsSkippedThroughReader()
	{
		SimpleConfig? config = DeserializeThroughReader<SimpleConfig>("""{"Kind":"Advanced","Tuning":"t" /* c */}""", SkipComments);

		Assert.AreEqual("t", config?.Tuning);
	}

	[TestMethod]
	public void TrailingCommaInDecoratedRootIsAcceptedThroughReader()
	{
		SimpleConfig? config = DeserializeThroughReader<SimpleConfig>("""{"Kind":"Advanced","Tuning":"t",}""", TrailingCommas);

		Assert.AreEqual("t", config?.Tuning);
	}

	[TestMethod]
	public void CommentInDecoratedRootThroughElementMatchesUnregistered() =>
		AssertElementOutcomeUnchangedByFactory("""{"Kind":"Advanced","Tuning":"t" /* c */}""", SkipComments);

	[TestMethod]
	public void TrailingCommaInDecoratedRootThroughElementMatchesUnregistered() =>
		AssertElementOutcomeUnchangedByFactory("""{"Kind":"Advanced","Tuning":"t",}""", TrailingCommas);

	[TestMethod]
	public void CommentInsideNestedDecoratedChildIsSkipped()
	{
		OuterConfig? outer = DeserializeThroughReader<OuterConfig>("""{"Label":"x","Child":{"Kind":"Advanced","Tuning":"t" /* c */}}""", SkipComments);

		Assert.AreEqual("t", outer?.Child?.Tuning);
	}

	[TestMethod]
	public void TrailingCommaInsideNestedDecoratedChildIsAccepted()
	{
		OuterConfig? outer = DeserializeThroughReader<OuterConfig>("""{"Label":"x","Child":{"Kind":"Advanced","Tuning":"t",},}""", TrailingCommas);

		Assert.AreEqual("t", outer?.Child?.Tuning);
	}

	[TestMethod]
	public void CommentInsideElementOfUnclaimedRootListIsSkipped()
	{
		List<SimpleConfig>? configs = DeserializeThroughReader<List<SimpleConfig>>("""[{"Kind":"Advanced","Tuning":"t" /* c */}]""", SkipComments);

		Assert.AreEqual("t", configs?[0].Tuning);
	}

	[TestMethod]
	public void TrailingCommaInsideElementOfUnclaimedRootListIsAccepted()
	{
		List<SimpleConfig>? configs = DeserializeThroughReader<List<SimpleConfig>>("""[{"Kind":"Advanced","Tuning":"t",},]""", TrailingCommas);

		Assert.AreEqual("t", configs?[0].Tuning);
	}

	[TestMethod]
	public void ViolationIsStillReportedThroughACommentSkippingReader()
	{
		JsonRequiredConditionallyException exception = Assert.ThrowsExactly<JsonRequiredConditionallyException>(
			() => DeserializeThroughReader<OuterConfig>("""{"Child":{"Kind":"Advanced" /* no Tuning */}}""", SkipComments));

		Assert.AreSequenceEqual(new List<string> { "Child.Tuning" }, [.. exception.MissingProperties]);
	}

	[TestMethod]
	public void ViolationIsStillReportedThroughATrailingCommaReader()
	{
		JsonRequiredConditionallyException exception = Assert.ThrowsExactly<JsonRequiredConditionallyException>(
			() => DeserializeThroughReader<OuterConfig>("""{"Child":{"Kind":"Advanced",},}""", TrailingCommas));

		Assert.AreSequenceEqual(new List<string> { "Child.Tuning" }, [.. exception.MissingProperties]);
	}
}
