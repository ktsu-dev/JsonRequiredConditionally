// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.JsonRequiredConditionally.Tests;

using System.Diagnostics.CodeAnalysis;
using System.Reflection;

/// <summary>
/// The library is built on <c>Activator.CreateInstance</c>, <c>MakeGenericType</c>,
/// <c>DefaultJsonTypeInfoResolver</c> and reflective member lookup, so it cannot survive trimming or
/// ahead-of-time compilation. A consumer must get a build warning rather than a runtime failure.
/// </summary>
[TestClass]
public class TrimAnnotationTests
{
	private static ConstructorInfo FactoryConstructor =>
		typeof(JsonRequiredConditionallyConverterFactory).GetConstructor(Type.EmptyTypes)
			?? throw new InvalidOperationException("The factory no longer has a public parameterless constructor.");

	[TestMethod]
	public void FactoryConstructorIsAnnotatedAsRequiringUnreferencedCode()
	{
		RequiresUnreferencedCodeAttribute? attribute =
			FactoryConstructor.GetCustomAttribute<RequiresUnreferencedCodeAttribute>();

		Assert.IsNotNull(attribute);
		StringAssert.Contains(attribute.Message, "trimming");
	}

	[TestMethod]
	public void FactoryConstructorIsAnnotatedAsRequiringDynamicCode()
	{
		RequiresDynamicCodeAttribute? attribute =
			FactoryConstructor.GetCustomAttribute<RequiresDynamicCodeAttribute>();

		Assert.IsNotNull(attribute);
		StringAssert.Contains(attribute.Message, "ahead-of-time");
	}
}
