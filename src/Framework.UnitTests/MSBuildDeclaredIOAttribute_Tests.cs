// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Linq;
using System.Reflection;
using Microsoft.Build.Framework;
using Shouldly;
using Xunit;

namespace Microsoft.Build.UnitTests;

public sealed class MSBuildDeclaredIOAttribute_Tests
{
    [Theory]
    [InlineData(typeof(MSBuildDeclaredIOTaskAttribute), false)]
    [InlineData(typeof(MSBuildDeclaredIOInputAttribute), true)]
    [InlineData(typeof(MSBuildDeclaredIOOutputAttribute), true)]
    public void AttributesApplyOnlyToClassesAndAreNotInherited(Type attributeType, bool allowMultiple)
    {
        AttributeUsageAttribute usage = attributeType.GetCustomAttribute<AttributeUsageAttribute>()!;
        usage.ValidOn.ShouldBe(AttributeTargets.Class);
        usage.Inherited.ShouldBeFalse();
        usage.AllowMultiple.ShouldBe(allowMultiple);
        attributeType.IsSealed.ShouldBeTrue();
    }

    [Fact]
    public void DeclarationsCanNameMultipleInheritedParameters()
    {
        typeof(DeclaredTask).GetCustomAttribute<MSBuildDeclaredIOTaskAttribute>().ShouldNotBeNull();
        typeof(DeclaredTask).GetCustomAttributes<MSBuildDeclaredIOInputAttribute>()
            .Select(attribute => attribute.ParameterName).ShouldBe([nameof(ParameterBase.Sources), nameof(ParameterBase.ConfigurationFile)], ignoreOrder: true);
        typeof(DeclaredTask).GetCustomAttributes<MSBuildDeclaredIOOutputAttribute>()
            .Select(attribute => attribute.ParameterName).ShouldBe([nameof(ParameterBase.DestinationFiles), nameof(ParameterBase.LogFile)], ignoreOrder: true);
        typeof(DeclaredTask).GetProperty(nameof(ParameterBase.Sources))!.DeclaringType.ShouldBe(typeof(ParameterBase));
        typeof(DeclaredTask).GetCustomAttributes<MSBuildDeclaredIOInputAttribute>()
            .Select(attribute => attribute.ParameterName)
            .Intersect(typeof(DeclaredTask).GetCustomAttributes<MSBuildDeclaredIOOutputAttribute>()
                .Select(attribute => attribute.ParameterName), StringComparer.OrdinalIgnoreCase)
            .ShouldBeEmpty();
    }

    [Theory]
    [InlineData(typeof(MSBuildDeclaredIOInputAttribute))]
    [InlineData(typeof(MSBuildDeclaredIOOutputAttribute))]
    public void DeclarationsHaveNoConditionalSettings(Type attributeType)
    {
        attributeType.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Select(property => property.Name).ShouldBe(["ParameterName"]);
    }

    [Fact]
    public void DerivedClassesMustRedeclareTheWholeContract()
    {
        typeof(UndeclaredDerivedTask).GetCustomAttribute<MSBuildDeclaredIOTaskAttribute>(inherit: true).ShouldBeNull();
        typeof(UndeclaredDerivedTask).GetCustomAttributes<MSBuildDeclaredIOInputAttribute>(inherit: true).ShouldBeEmpty();
        typeof(UndeclaredDerivedTask).GetCustomAttributes<MSBuildDeclaredIOOutputAttribute>(inherit: true).ShouldBeEmpty();
        typeof(RedeclaredDerivedTask).GetCustomAttribute<MSBuildDeclaredIOTaskAttribute>(inherit: true).ShouldNotBeNull();
        typeof(RedeclaredDerivedTask).GetCustomAttributes<MSBuildDeclaredIOInputAttribute>(inherit: true)
            .Select(attribute => attribute.ParameterName).ShouldBe([nameof(ParameterBase.Sources)]);
        typeof(RedeclaredDerivedTask).GetCustomAttributes<MSBuildDeclaredIOOutputAttribute>(inherit: true)
            .Select(attribute => attribute.ParameterName).ShouldBe([nameof(ParameterBase.LogFile)]);
    }

    [Fact]
    public void MarkerCanDeclareNoFileIO()
    {
        typeof(NoFileTask).GetCustomAttribute<MSBuildDeclaredIOTaskAttribute>().ShouldNotBeNull();
        typeof(NoFileTask).GetCustomAttributes<MSBuildDeclaredIOInputAttribute>().ShouldBeEmpty();
        typeof(NoFileTask).GetCustomAttributes<MSBuildDeclaredIOOutputAttribute>().ShouldBeEmpty();
    }

    [Fact]
    public void DeclarationsCanBeInspectedWithoutConstructingTheTaskOrReadingParameters()
    {
        CustomAttributeData declaration = typeof(UninstantiableTask).GetCustomAttributesData()
            .Single(attribute => attribute.AttributeType == typeof(MSBuildDeclaredIOInputAttribute));
        declaration.ConstructorArguments.Single().Value.ShouldBe(nameof(UninstantiableTask.Source));
    }

    [Fact]
    public void FileOutputsDoNotImplyTaskOutputBindings()
    {
        typeof(ParameterBase).GetProperty(nameof(ParameterBase.DestinationFiles))!.GetCustomAttribute<OutputAttribute>().ShouldBeNull();
        typeof(ParameterBase).GetProperty(nameof(ParameterBase.Result))!.GetCustomAttribute<OutputAttribute>().ShouldNotBeNull();
        typeof(DeclaredTask).GetCustomAttributes<MSBuildDeclaredIOOutputAttribute>()
            .Select(attribute => attribute.ParameterName).ShouldNotContain(nameof(ParameterBase.Result));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("\t\r\n")]
    public void EmptyParameterNamesAreRejected(string name)
    {
        Should.Throw<ArgumentException>(() => new MSBuildDeclaredIOInputAttribute(name)).ParamName.ShouldBe("parameterName");
        Should.Throw<ArgumentException>(() => new MSBuildDeclaredIOOutputAttribute(name)).ParamName.ShouldBe("parameterName");
    }

    [Fact]
    public void NullParameterNamesAreRejected()
    {
        Should.Throw<ArgumentNullException>(() => new MSBuildDeclaredIOInputAttribute(null!)).ParamName.ShouldBe("parameterName");
        Should.Throw<ArgumentNullException>(() => new MSBuildDeclaredIOOutputAttribute(null!)).ParamName.ShouldBe("parameterName");
    }

    private class ParameterBase
    {
        public ITaskItem[] Sources { get; set; } = [];
        public string ConfigurationFile { get; set; } = String.Empty;
        public string[] DestinationFiles { get; set; } = [];
        public string LogFile { get; set; } = String.Empty;
        [Output]
        public bool Result { get; set; }
    }

    [MSBuildDeclaredIOTask]
    [MSBuildDeclaredIOInput(nameof(Sources))]
    [MSBuildDeclaredIOInput(nameof(ConfigurationFile))]
    [MSBuildDeclaredIOOutput(nameof(DestinationFiles))]
    [MSBuildDeclaredIOOutput(nameof(LogFile))]
    private class DeclaredTask : ParameterBase
    {
    }

    private sealed class UndeclaredDerivedTask : DeclaredTask
    {
    }

    [MSBuildDeclaredIOTask]
    [MSBuildDeclaredIOInput(nameof(Sources))]
    [MSBuildDeclaredIOOutput(nameof(LogFile))]
    private sealed class RedeclaredDerivedTask : DeclaredTask
    {
    }

    [MSBuildDeclaredIOTask]
    private sealed class NoFileTask
    {
    }

    [MSBuildDeclaredIOTask]
    [MSBuildDeclaredIOInput(nameof(Source))]
    private sealed class UninstantiableTask
    {
        public UninstantiableTask() => throw new InvalidOperationException();
        public string Source => throw new InvalidOperationException();
    }
}
