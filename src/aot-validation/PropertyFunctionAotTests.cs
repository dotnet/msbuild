// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Construction;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Exceptions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.Build.AotValidation;

/// <summary>
/// Pins the consumer-facing error an MSBuild author sees when a property function tries to reach a
/// reflective, dynamic-code member - here <c>System.Enum.GetValues(Type)</c>, which carries
/// <c>[RequiresDynamicCode]</c> and is the source of the IL3050 the engine suppresses - under Native AOT.
///
/// Finding (captured under Native AOT, both with the default allowlist and with the
/// <c>MSBUILDENABLEALLPROPERTYFUNCTIONS=1</c> escape hatch): a property function can <b>never</b> reach
/// <c>Enum.GetValues(Type)</c> (or any reflective method that takes a <c>System.Type</c>), because there is
/// no way to produce a <c>Type</c> argument:
/// <list type="bullet">
/// <item><c>string</c> never coerces to <c>Type</c>, so the overload does not bind - the author gets
/// <b>MSB4186</b> ("Method '...' not found ... Check that all parameters are ... of the correct type").</item>
/// <item><c>[System.Type]::GetType(...)</c> is not an available property function - the author gets
/// <b>MSB4185</b> ("The function 'GetType' ... is not available for execution as an MSBuild property
/// function"); with the escape hatch on it still fails to bind (MSB4186).</item>
/// </list>
/// So the failure is always a normal, AOT-independent property-function diagnostic that points at the
/// author's own expression - never an AOT crash, and never a <c>NotSupportedException</c> leaking out of the
/// trimmed dynamic-code path. There is therefore nothing to special-case for <c>Enum.GetValues</c>: the
/// existing MSB4185/MSB4186 errors already block it cleanly and identically on JIT and AOT, which is also why
/// the engine's IL3050 suppression is a static-reachability false positive rather than a hidden failure.
/// </summary>
[TestClass]
public sealed class PropertyFunctionAotTests
{
    [TestMethod]
    public void EnumGetValues_WithStringArgument_FailsWithUnderstandableMethodNotFound()
    {
        // string -> Type does not coerce, so Enum.GetValues(Type) never binds and is never invoked.
        InvalidProjectFileException ex = EvaluateExpectingFailure("$([System.Enum]::GetValues('System.DayOfWeek'))");

        Assert.AreEqual("MSB4186", ex.ErrorCode);
        StringAssert.Contains(ex.Message, "System.Enum.GetValues");
        // The author sees a property-function diagnostic, not a leaked AOT/dynamic-code exception.
        Assert.IsNull(ex.InnerException);
    }

    [TestMethod]
    public void TypeGetType_IsNotAnAvailablePropertyFunction()
    {
        // There is no allowed property function that produces a System.Type, so a Type argument cannot be
        // built to feed a reflective Type-taking method.
        InvalidProjectFileException ex = EvaluateExpectingFailure("$([System.Type]::GetType('System.DayOfWeek'))");

        Assert.AreEqual("MSB4185", ex.ErrorCode);
    }

    [TestMethod]
    public void EnumGetValues_WithNestedGetType_FailsBeforeReachingTheReflectiveInvoke()
    {
        // The inner [System.Type]::GetType is rejected first (MSB4185), so the outer
        // Enum.GetValues(Type) - the [RequiresDynamicCode] member - is never reached under AOT.
        InvalidProjectFileException ex = EvaluateExpectingFailure(
            "$([System.Enum]::GetValues($([System.Type]::GetType('System.DayOfWeek'))))");

        Assert.AreEqual("MSB4185", ex.ErrorCode);
    }

    [TestMethod]
    public void AllowlistedReceiverStaticFunctions_DispatchByReflectionUnderAot()
    {
        // The positive counterpart to the blocked cases above: a property function over a curated BCL
        // receiver type (System.IO.Path, System.String) dispatches over that type's public members by
        // reflection - the ReceiverType path whose [DynamicallyAccessedMembers] annotation keeps those
        // members under trimming. This confirms reflective property-function evaluation still works under AOT.
        Assert.AreEqual("HelloWorld", EvaluateToValue("$([System.IO.Path]::GetFileName('x/HelloWorld'))"));
        Assert.AreEqual("ab", EvaluateToValue("$([System.String]::Concat('a', 'b'))"));
    }

    /// <summary>
    /// Evaluates a single property-function expression as a project property and returns the resolved value.
    /// </summary>
    private static string EvaluateToValue(string expression)
    {
        ProjectRootElement root = ProjectRootElement.Create();
        root.AddProperty("Result", expression);
        using var collection = new ProjectCollection();

        return new Project(root, globalProperties: null, toolsVersion: null, collection).GetPropertyValue("Result");
    }

    /// <summary>
    /// Evaluates a single property-function expression as a project property (evaluation happens in the
    /// <see cref="Project"/> constructor) and returns the <see cref="InvalidProjectFileException"/> the
    /// author would see. Fails the test if evaluation unexpectedly succeeds or throws a different type.
    /// </summary>
    private static InvalidProjectFileException EvaluateExpectingFailure(string expression)
    {
        ProjectRootElement root = ProjectRootElement.Create();
        root.AddProperty("Result", expression);
        using var collection = new ProjectCollection();

        return Assert.ThrowsException<InvalidProjectFileException>(
            () => new Project(root, globalProperties: null, toolsVersion: null, collection));
    }
}
