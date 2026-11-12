// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using System.Reflection;
using Microsoft.Build.Construction;
using Microsoft.Build.Exceptions;
using Microsoft.Build.Framework;
using Microsoft.Build.Internal;
using Microsoft.Build.Shared;
using Shouldly;
using Xunit;

namespace Microsoft.Build.UnitTests;

public sealed class ProjectErrorUtilities_Tests
{
    [Fact]
    public void ThrowPreservesDiagnosticDetailsAndArgumentArray()
    {
        ElementLocation location = ElementLocation.Create(Path.Combine(AppContext.BaseDirectory, "project.sln"), 12, 34);
        const string resourceName = "SolutionParseProjectDepNotFoundError";
        object?[] args = ["project-guid", "dependency-guid"];
        string message = ResourceUtilities.FormatResourceStringStripCodeAndKeyword(
            out string? errorCode, out string? helpKeyword, resourceName, args);

        InvalidProjectFileException exception = Should.Throw<InvalidProjectFileException>(
            () => ProjectErrorUtilities.ThrowInvalidProject(DiagnosticSubcategory.SolutionFile, location, resourceName, args));

        exception.ProjectFile.ShouldBe(location.File);
        exception.LineNumber.ShouldBe(location.Line);
        exception.ColumnNumber.ShouldBe(location.Column);
        exception.EndLineNumber.ShouldBe(0);
        exception.EndColumnNumber.ShouldBe(0);
        exception.BaseMessage.ShouldBe(message);
        exception.ErrorSubcategory.ShouldBe(AssemblyResources.GetString("SubCategoryForSolutionParsingErrors"));
        exception.ErrorCode.ShouldBe(errorCode);
        exception.HelpKeyword.ShouldBe(helpKeyword);
        exception.InnerException.ShouldBeNull();
    }

    [Fact]
    public void ThrowWithoutFormatArgumentsPreservesMessage()
    {
        const string resourceName = "SolutionParseNoHeaderError";
        string message = ResourceUtilities.FormatResourceStringStripCodeAndKeyword(out _, out _, resourceName);

        InvalidProjectFileException exception = Should.Throw<InvalidProjectFileException>(
            () => ProjectErrorUtilities.ThrowInvalidProject(ElementLocation.Empty, resourceName));

        exception.BaseMessage.ShouldBe(message);
        exception.ErrorSubcategory.ShouldBeNull();

        exception = Should.Throw<InvalidProjectFileException>(
            () => ProjectErrorUtilities.ThrowInvalidProject(DiagnosticSubcategory.SolutionFile, ElementLocation.Empty, resourceName));

        exception.BaseMessage.ShouldBe(message);
        exception.ErrorSubcategory.ShouldBe(AssemblyResources.GetString("SubCategoryForSolutionParsingErrors"));
    }

    [Fact]
    public void ThrowFormatsNullArguments()
    {
        const string resourceName = "InvalidProjectFile";
        object?[] args = [null];
        string message = ResourceUtilities.FormatResourceStringStripCodeAndKeyword(out _, out _, resourceName, args);

        InvalidProjectFileException exception = Should.Throw<InvalidProjectFileException>(
            () => ProjectErrorUtilities.ThrowInvalidProject(ElementLocation.Empty, resourceName, args));

        exception.BaseMessage.ShouldBe(message);
    }

    [Fact]
    public void ThrowRejectsNullLocation()
        => Should.Throw<InternalErrorException>(
            () => ProjectErrorUtilities.ThrowInvalidProject(null!, "SolutionParseNoHeaderError"));

    [Fact]
    public void ThrowPreservesInnerExceptionWithoutSubcategory()
    {
        var innerException = new InvalidOperationException("inner error");

        InvalidProjectFileException exception = Should.Throw<InvalidProjectFileException>(
            () => ProjectErrorUtilities.ThrowInvalidProject(
                ElementLocation.Empty, innerException, "InvalidProjectFile", innerException.Message));

        exception.InnerException.ShouldBeSameAs(innerException);
        exception.ErrorSubcategory.ShouldBeNull();
        exception.ErrorCode.ShouldBe("MSB4025");
        exception.BaseMessage.ShouldContain(innerException.Message);
    }

    [Fact]
    public void ThrowPreservesSubcategoryAndInnerException()
    {
        var innerException = new InvalidOperationException("inner error");

        InvalidProjectFileException exception = Should.Throw<InvalidProjectFileException>(
            () => ProjectErrorUtilities.ThrowInvalidProject(
                DiagnosticSubcategory.SchemaValidation,
                ElementLocation.Empty,
                innerException,
                "InvalidProjectFile",
                innerException.Message));

        exception.InnerException.ShouldBeSameAs(innerException);
        exception.ErrorSubcategory.ShouldBe(AssemblyResources.GetString("SubCategoryForSchemaValidationErrors"));
        exception.ErrorCode.ShouldBe("MSB4025");
        exception.BaseMessage.ShouldContain(innerException.Message);
    }

    [Fact]
    public void VerifyPreservesSubcategoryAndInnerException()
    {
        var innerException = new InvalidOperationException("inner error");

        InvalidProjectFileException exception = Should.Throw<InvalidProjectFileException>(
            () => ProjectErrorUtilities.VerifyThrowInvalidProject(
                false,
                DiagnosticSubcategory.SchemaValidation,
                ElementLocation.Empty,
                innerException,
                "InvalidProjectFile",
                innerException.Message));

        exception.InnerException.ShouldBeSameAs(innerException);
        exception.ErrorSubcategory.ShouldBe(AssemblyResources.GetString("SubCategoryForSchemaValidationErrors"));
        exception.ErrorCode.ShouldBe("MSB4025");
        exception.BaseMessage.ShouldContain(innerException.Message);
    }

    [Fact]
    public void VerifyPreservesGenericFormatArguments()
    {
        const string resourceName = "SolutionParseNestedProjectErrorWithNameAndGuid";
        string message = ResourceUtilities.FormatResourceStringStripCodeAndKeyword(
            out _, out _, resourceName, "project-name", "project-guid", "parent-guid");

        InvalidProjectFileException exception = Should.Throw<InvalidProjectFileException>(
            () => ProjectErrorUtilities.VerifyThrowInvalidProject(
                false,
                DiagnosticSubcategory.SolutionFile,
                ElementLocation.Empty,
                resourceName,
                "project-name",
                "project-guid",
                "parent-guid"));

        exception.BaseMessage.ShouldBe(message);
        exception.ErrorSubcategory.ShouldBe(AssemblyResources.GetString("SubCategoryForSolutionParsingErrors"));

        exception = Should.Throw<InvalidProjectFileException>(
            () => ProjectErrorUtilities.VerifyThrowInvalidProject(
                false, ElementLocation.Empty, resourceName, "project-name", "project-guid", "parent-guid"));

        exception.BaseMessage.ShouldBe(message);
        exception.ErrorSubcategory.ShouldBeNull();
    }

    [Fact]
    public void VerifyDoesNotResolveResourcesWhenConditionSucceeds()
    {
        const string resourceName = "MissingResource";
        ElementLocation location = ElementLocation.Empty;

        ProjectErrorUtilities.VerifyThrowInvalidProject(true, location, resourceName);
        ProjectErrorUtilities.VerifyThrowInvalidProject(true, location, resourceName, 1);
        ProjectErrorUtilities.VerifyThrowInvalidProject(true, location, resourceName, 1, 2);
        ProjectErrorUtilities.VerifyThrowInvalidProject(true, location, resourceName, 1, 2, 3);
        ProjectErrorUtilities.VerifyThrowInvalidProject(true, location, resourceName, 1, 2, 3, 4);
        ProjectErrorUtilities.VerifyThrowInvalidProject(true, location, resourceName, 1, 2, 3, 4, 5);
        ProjectErrorUtilities.VerifyThrowInvalidProject(true, DiagnosticSubcategory.SolutionFile, location, resourceName);
        ProjectErrorUtilities.VerifyThrowInvalidProject(true, DiagnosticSubcategory.SolutionFile, location, resourceName, 1);
        ProjectErrorUtilities.VerifyThrowInvalidProject(true, DiagnosticSubcategory.SolutionFile, location, resourceName, 1, 2);
        ProjectErrorUtilities.VerifyThrowInvalidProject(true, DiagnosticSubcategory.SolutionFile, location, resourceName, 1, 2, 3);
        ProjectErrorUtilities.VerifyThrowInvalidProject(true, DiagnosticSubcategory.SolutionFile, location, resourceName, 1, 2, 3, 4);
        ProjectErrorUtilities.VerifyThrowInvalidProject(true, DiagnosticSubcategory.SolutionFile, location, resourceName, 1, 2, 3, 4, 5);
        ProjectErrorUtilities.VerifyThrowInvalidProject(true, location, innerException: null, resourceName);
        ProjectErrorUtilities.VerifyThrowInvalidProject(true, location, innerException: null, resourceName, 1);
        ProjectErrorUtilities.VerifyThrowInvalidProject(true, location, innerException: null, resourceName, 1, 2);
        ProjectErrorUtilities.VerifyThrowInvalidProject(true, location, innerException: null, resourceName, 1, 2, 3);
        ProjectErrorUtilities.VerifyThrowInvalidProject(true, location, innerException: null, resourceName, 1, 2, 3, 4);
        ProjectErrorUtilities.VerifyThrowInvalidProject(true, location, innerException: null, resourceName, 1, 2, 3, 4, 5);
        ProjectErrorUtilities.VerifyThrowInvalidProject(
            true, DiagnosticSubcategory.SolutionFile, location, innerException: null, resourceName);
        ProjectErrorUtilities.VerifyThrowInvalidProject(
            true, DiagnosticSubcategory.SolutionFile, location, innerException: null, resourceName, 1);
        ProjectErrorUtilities.VerifyThrowInvalidProject(
            true, DiagnosticSubcategory.SolutionFile, location, innerException: null, resourceName, 1, 2);
        ProjectErrorUtilities.VerifyThrowInvalidProject(
            true, DiagnosticSubcategory.SolutionFile, location, innerException: null, resourceName, 1, 2, 3);
        ProjectErrorUtilities.VerifyThrowInvalidProject(
            true, DiagnosticSubcategory.SolutionFile, location, innerException: null, resourceName, 1, 2, 3, 4);
        ProjectErrorUtilities.VerifyThrowInvalidProject(
            true, DiagnosticSubcategory.SolutionFile, location, innerException: null, resourceName, 1, 2, 3, 4, 5);
    }

    [Fact]
    public void AllOverloadsHaveControlFlowAnnotations()
    {
        // Attribute polyfills on .NET Framework can have different assembly identities.
        foreach (MethodInfo method in typeof(ProjectErrorUtilities).GetMethods(
            BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
        {
            if (method.Name.StartsWith(nameof(ProjectErrorUtilities.ThrowInvalidProject), StringComparison.Ordinal))
            {
                method.IsGenericMethod.ShouldBeFalse();
                method.GetCustomAttributesData().ShouldContain(
                    attribute => attribute.AttributeType.FullName == "System.Diagnostics.CodeAnalysis.DoesNotReturnAttribute",
                    method.ToString());
            }
            else if (method.Name == nameof(ProjectErrorUtilities.VerifyThrowInvalidProject))
            {
                method.GetParameters()[0].GetCustomAttributesData().ShouldContain(
                    attribute => attribute.AttributeType.FullName == "System.Diagnostics.CodeAnalysis.DoesNotReturnIfAttribute"
                        && Equals(attribute.ConstructorArguments[0].Value, false),
                    method.ToString());
            }
        }
    }
}
