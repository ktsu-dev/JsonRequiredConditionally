// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.JsonRequiredConditionally;

/// <summary>
/// Marks a property or field as required during JSON deserialization and additionally requires that
/// the value it carries is not empty.
/// </summary>
/// <remarks>
/// <para>
/// The member is satisfied only when its JSON property is physically present in the payload
/// <em>and</em> its value is non-empty. Emptiness is judged from the payload:
/// </para>
/// <list type="bullet">
/// <item><description>The property being absent is a violation, reported in <see cref="JsonRequiredConditionallyException.MissingProperties"/>.</description></item>
/// <item><description><c>null</c>, <c>""</c>, <c>[]</c> and <c>{}</c> are violations, reported in JsonRequiredConditionallyException.EmptyProperties.</description></item>
/// <item><description>A whitespace-only string is <em>not</em> empty. A string is empty when its length is zero.</description></item>
/// <item><description>A number or boolean can never be empty, so decorating such a member produces a rule that is always satisfied.</description></item>
/// </list>
/// <para>
/// This attribute is self-sufficient and is meant to be used alone. Pairing it with
/// <see cref="System.Text.Json.Serialization.JsonRequiredAttribute"/> is redundant, because this
/// attribute already reports an absent property. Pairing also costs diagnostics: System.Text.Json
/// enforces its own required-property check during deserialization, strictly before this library's
/// walk runs, so an absent property raises that exception and the walk never gets to collect the
/// remaining violations in the payload.
/// </para>
/// </remarks>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false, Inherited = true)]
public sealed class JsonRequiredAndNotEmptyAttribute : Attribute
{
}
