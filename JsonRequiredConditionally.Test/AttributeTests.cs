// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.JsonRequiredConditionally.Tests;

using System.Linq;
using System.Reflection;

[TestClass]
public class AttributeTests
{
	public enum Kind
	{
		Basic = 0,
		Advanced = 1,
	}

	public sealed class Target
	{
		public Kind Kind { get; set; }

		[JsonRequiredIfSiblingIs(nameof(Kind), Kind.Advanced)]
		public string? Tuning { get; set; }
	}

	[TestMethod]
	public void AttributeStoresSiblingNameAndEnumValue()
	{
		PropertyInfo property = typeof(Target).GetProperty(nameof(Target.Tuning))!;

		JsonRequiredIfSiblingIsAttribute attribute = property
			.GetCustomAttributes<JsonRequiredIfSiblingIsAttribute>()
			.Single();

		Assert.AreEqual("Kind", attribute.SiblingName);
		Assert.AreEqual(Kind.Advanced, attribute.Value);
	}

	[TestMethod]
	public void AttributeAllowsMultipleOnOneMember()
	{
		PropertyInfo property = typeof(MultiTarget).GetProperty(nameof(MultiTarget.Tuning))!;

		JsonRequiredIfSiblingIsAttribute[] attributes = [.. property.GetCustomAttributes<JsonRequiredIfSiblingIsAttribute>()];

		Assert.HasCount(2, attributes);
	}

	[TestMethod]
	public void AttributeAcceptsNullValue()
	{
		JsonRequiredIfSiblingIsAttribute attribute = new("Sibling", null);

		Assert.IsNull(attribute.Value);
	}

	[TestMethod]
	public void AttributeRejectsNullSiblingName()
	{
		Assert.ThrowsExactly<ArgumentNullException>(() => new JsonRequiredIfSiblingIsAttribute(null!, 1));
	}

	public sealed class MultiTarget
	{
		public Kind Kind { get; set; }

		[JsonRequiredIfSiblingIs(nameof(Kind), Kind.Basic)]
		[JsonRequiredIfSiblingIs(nameof(Kind), Kind.Advanced)]
		public string? Tuning { get; set; }
	}

	[TestMethod]
	public void NotEmptyAttributeTargetsPropertiesAndFields()
	{
		AttributeUsageAttribute? usage = typeof(JsonRequiredAndNotEmptyAttribute)
			.GetCustomAttributes(typeof(AttributeUsageAttribute), inherit: false)
			.Cast<AttributeUsageAttribute>()
			.SingleOrDefault();

		Assert.IsNotNull(usage);
		Assert.AreEqual(AttributeTargets.Property | AttributeTargets.Field, usage.ValidOn);
	}

	[TestMethod]
	public void NotEmptyAttributeIsNotRepeatable()
	{
		AttributeUsageAttribute? usage = typeof(JsonRequiredAndNotEmptyAttribute)
			.GetCustomAttributes(typeof(AttributeUsageAttribute), inherit: false)
			.Cast<AttributeUsageAttribute>()
			.SingleOrDefault();

		Assert.IsNotNull(usage);
		Assert.IsFalse(usage.AllowMultiple);
		Assert.IsTrue(usage.Inherited);
	}

	[TestMethod]
	public void NotEmptyAttributeIsSealed()
	{
		Assert.IsTrue(typeof(JsonRequiredAndNotEmptyAttribute).IsSealed);
	}
}
