// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Framework;
using FluentAssertions;
using FluentAssertions.Execution;
using FluentAssertions.Primitives;
using Microsoft.Build.Execution;

namespace Microsoft.NET.Build.Containers.IntegrationTests;

public class ProjectInstanceAssertions: ReferenceTypeAssertions<ProjectInstance, ProjectInstanceAssertions>
{

    protected override string Identifier => "project instance";

    public ProjectInstanceAssertions(ProjectInstance instance) : base(instance)
    {
    }

    public AndConstraint<ProjectInstanceAssertions> BuildSuccessfully(string target, CapturingLogger logger, string because = "", params object[] becauseArgs)
    {
        IDictionary<string, TargetResult>? targetOutputs = null;
        Execute.Assertion.BecauseOf(because, becauseArgs)
        .Given(() => Subject.Build(new [] { target }, new [] { logger }, null, out targetOutputs))
        .FailWith("Expected {context:project instance} to build successfully{reason}, but failed with {0} errors and {1} warnings.\n{2}\n{3}",
            _ => logger.Errors.Count,
            _ => logger.Warnings.Count,
            _ => logger.Errors.Select(x => new { x.Code, x.Subcategory, x.Message, x.File, x.LineNumber, x.ColumnNumber }),
            _ => logger.Warnings.Select(x => new { x.Code, x.Subcategory, x.Message, x.File, x.LineNumber, x.ColumnNumber }));

        return new AndConstraint<ProjectInstanceAssertions>(this);
    }
}

public static class ProjectInstanceExtensions
{
    public static ProjectInstanceAssertions Should(this ProjectInstance instance)
    {
        return new ProjectInstanceAssertions(instance);
    }
}
