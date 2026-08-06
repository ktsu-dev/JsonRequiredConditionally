// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace ktsu.JsonRequiredConditionally;

/// <summary>
/// Marks a property or field as required during JSON deserialization only when a sibling
/// member of the same type has a particular value.
/// </summary>
/// <remarks>
/// Multiple attributes on one member group implicitly by <see cref="SiblingName"/>. Values within
/// a group are combined with OR; the groups themselves are combined with AND. The member is
/// considered satisfied when it is physically present in the payload, even if its value is null.
/// </remarks>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = true, Inherited = true)]
public sealed class JsonRequiredIfSiblingIsAttribute : Attribute
{
	/// <summary>
	/// Initializes a new instance of the <see cref="JsonRequiredIfSiblingIsAttribute"/> class.
	/// </summary>
	/// <param name="siblingName">The CLR name of the sibling member to inspect. Use <c>nameof</c>.</param>
	/// <param name="value">The value the sibling must have for this member to be required.</param>
	public JsonRequiredIfSiblingIsAttribute(string siblingName, object? value)
	{
		Ensure.NotNull(siblingName);

		SiblingName = siblingName;
		Value = value;
	}

	/// <summary>
	/// Gets the CLR name of the sibling member to inspect.
	/// </summary>
	public string SiblingName { get; }

	/// <summary>
	/// Gets the value the sibling must have for the decorated member to be required.
	/// </summary>
	public object? Value { get; }
}
