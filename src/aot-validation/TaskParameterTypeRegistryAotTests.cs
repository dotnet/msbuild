// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Evaluation;
using Microsoft.Build.Exceptions;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.Build.AotValidation;

/// <summary>
/// Validates the reflection-free task-parameter-type resolution under Native AOT - the path a
/// <c>&lt;UsingTask&gt;</c> <c>&lt;ParameterGroup&gt;</c> takes when MSBuild turns a declared
/// <c>ParameterType</c> name into a <see cref="System.Type"/>.
///
/// This harness bakes <c>EnableReflectiveTaskParameterTypes=false</c>, so the by-name
/// <c>Type.GetType</c> fallback is trimmed away. Only types in <c>TaskParameterTypeRegistry</c> (the
/// intrinsic value types, <see cref="string"/>, and the MSBuild <see cref="ITaskItem"/> types, plus any
/// a host registers through <see cref="TaskItem.RegisterTaskParameterValueType{T}"/> /
/// <see cref="TaskItem.RegisterTaskParameterItemType{T}"/>) resolve. Parsing the <c>&lt;ParameterGroup&gt;</c>
/// happens during evaluation (registration), independent of any task factory, so simply evaluating the
/// project exercises the registry. The inline task is never executed.
/// </summary>
[TestClass]
public sealed class TaskParameterTypeRegistryAotTests
{
    [TestMethod]
    public void PreRegisteredParameterTypes_ResolveWithReflectionOff()
    {
        // Every one of these is pre-registered (string, the intrinsic value types and their arrays, and
        // the ITaskItem family), so the <ParameterGroup> parses with no Type.GetType even though the
        // reflective fallback is trimmed away in this image. Evaluation completing is the proof.
        Project project = Evaluate(
            """
              <AString ParameterType="System.String" />
              <ABool ParameterType="System.Boolean" />
              <AnInt ParameterType="System.Int32" />
              <ADateTime ParameterType="System.DateTime" />
              <AnIntArray ParameterType="System.Int32[]" />
              <AStringArray ParameterType="System.String[]" Output="true" />
              <AnItem ParameterType="Microsoft.Build.Framework.ITaskItem" />
              <AnItemArray ParameterType="Microsoft.Build.Framework.ITaskItem[]" Output="true" />
            """);

        Assert.AreEqual("ok", project.GetPropertyValue("Sentinel"));
    }

    [TestMethod]
    public void HostRegisteredValueType_ResolvesWithReflectionOff()
    {
        // A value type that is NOT pre-registered. The host registers it through the public seam; after
        // that the name resolves from the registry with no reflection, which is the whole point of the
        // registration API under AOT. The [DynamicallyAccessedMembers] on the register method roots the
        // struct so the trimmer preserves it.
        TaskItem.RegisterTaskParameterValueType<HarnessParameterStruct>();

        Project project = Evaluate(
            $"""
              <Custom ParameterType="{typeof(HarnessParameterStruct).FullName}" />
            """);

        Assert.AreEqual("ok", project.GetPropertyValue("Sentinel"));
    }

    [TestMethod]
    public void HostRegisteredConcreteItemType_ResolvesWithReflectionOff()
    {
        // Microsoft.Build.Utilities.TaskItem is the public concrete ITaskItem a task author constructs. It
        // is a higher-layer type the engine does not pre-register (Microsoft.Build does not reference
        // Microsoft.Build.Utilities); a host that declares it registers it through the public API, after
        // which it resolves from the registry with no reflection. (Concrete item types are legal only as
        // outputs, so this is declared Output.)
        TaskItem.RegisterTaskParameterItemType<TaskItem>();

        Project project = Evaluate(
            """
              <Item ParameterType="Microsoft.Build.Utilities.TaskItem" Output="true" />
            """);

        Assert.AreEqual("ok", project.GetPropertyValue("Sentinel"));
    }

    [TestMethod]
    public void UnregisteredType_WithReflectionOff_FailsObservably()
    {
        // System.Guid is a valid value-type parameter but is intentionally NOT pre-registered. With the
        // reflective fallback trimmed away, the name does not resolve and evaluation fails with a reported
        // project error (InvalidProjectFileException) - the observable-failure contract this switch
        // promises, not a reflection crash.
        Assert.ThrowsException<InvalidProjectFileException>(
            () => Evaluate(
                """
                  <Unknown ParameterType="System.Guid" />
                """));
    }

    /// <summary>
    /// Writes a project whose single <c>&lt;UsingTask&gt;</c> declares the given parameters in a
    /// <c>&lt;ParameterGroup&gt;</c> and evaluates it. Evaluation parses the parameter group (resolving
    /// each declared type) but never executes the task, so an unresolvable assembly/factory is irrelevant.
    /// </summary>
    private static Project Evaluate(string parameterGroupInner)
    {
        string projectXml =
            $$"""
            <Project>
              <PropertyGroup><Sentinel>ok</Sentinel></PropertyGroup>
              <UsingTask TaskName="HarnessInlineTask" AssemblyName="Unused" TaskFactory="RoslynCodeTaskFactory">
                <ParameterGroup>
            {{parameterGroupInner}}
                </ParameterGroup>
                <Task><Code>// never executed</Code></Task>
              </UsingTask>
            </Project>
            """;

        using TempDirectory dir = new();
        string projectPath = Path.Combine(dir.Path, "App.proj");
        File.WriteAllText(projectPath, projectXml);

        using ProjectCollection collection = new();
        return new Project(projectPath, globalProperties: null, toolsVersion: null, collection);
    }
}

/// <summary>
/// A value type the product does not pre-register, used to prove host registration of a custom task
/// parameter value type resolves under Native AOT.
/// </summary>
internal struct HarnessParameterStruct
{
    public int Value { get; set; }
}
