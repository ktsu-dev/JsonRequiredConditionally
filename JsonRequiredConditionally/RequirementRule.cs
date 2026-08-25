// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.JsonRequiredConditionally;

/// <summary>
/// One sibling and the set of values that make the decorated member required. Values are OR-ed.
/// </summary>
internal sealed class SiblingCondition(string siblingName, Func<object, object?> accessor, object?[] acceptedValues)
{
	/// <summary>
	/// Gets the CLR name of the sibling this condition inspects.
	/// </summary>
	internal string SiblingName { get; } = siblingName;

	/// <summary>
	/// Gets the values that satisfy this condition.
	/// </summary>
	internal object?[] AcceptedValues { get; } = acceptedValues;

	/// <summary>
	/// Determines whether the instance's sibling value matches any accepted value.
	/// </summary>
	/// <param name="instance">The materialized object.</param>
	/// <returns>True when any accepted value matches.</returns>
	internal bool IsSatisfiedBy(object instance)
	{
		object? actual = accessor(instance);

		foreach (object? expected in AcceptedValues)
		{
			if (ValueMatcher.Matches(actual, expected))
			{
				return true;
			}
		}

		return false;
	}
}

/// <summary>
/// One decorated member and the conditions that make it required. Conditions are AND-ed.
/// </summary>
internal sealed class RequirementRule(string jsonName, string memberName, SiblingCondition[] conditions)
{
	/// <summary>
	/// Gets the name this member carries in the JSON payload.
	/// </summary>
	internal string JsonName { get; } = jsonName;

	/// <summary>
	/// Gets the CLR name of the decorated member.
	/// </summary>
	internal string MemberName { get; } = memberName;

	/// <summary>
	/// Gets the conditions that must all hold for the member to be required.
	/// </summary>
	internal SiblingCondition[] Conditions { get; } = conditions;

	/// <summary>
	/// Determines whether the member is required for the given instance.
	/// </summary>
	/// <param name="instance">The materialized object.</param>
	/// <returns>True when every condition is satisfied.</returns>
	internal bool IsRequiredFor(object instance)
	{
		foreach (SiblingCondition condition in Conditions)
		{
			if (!condition.IsSatisfiedBy(instance))
			{
				return false;
			}
		}

		return true;
	}
}

/// <summary>
/// One member that must be present in the payload and carry a non-empty value.
/// </summary>
/// <remarks>
/// Deliberately a separate type from <see cref="RequirementRule"/> rather than a mode flag on it.
/// <see cref="RequirementRule"/> is sibling-conditional and evaluates against a materialized instance,
/// whereas this rule is unconditional and evaluates against a <see cref="System.Text.Json.JsonElement"/>.
/// A single type carrying both would leave half its state unused on every instance.
/// </remarks>
internal sealed class NonEmptyRule(string jsonName, string memberName)
{
	/// <summary>
	/// Gets the name this member carries in the JSON payload.
	/// </summary>
	internal string JsonName { get; } = jsonName;

	/// <summary>
	/// Gets the CLR name of the decorated member.
	/// </summary>
	internal string MemberName { get; } = memberName;
}
