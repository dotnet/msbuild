// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Cli.PostActionProcessors;

namespace Microsoft.TemplateEngine.Cli
{
    public static class Components
    {
        public static IReadOnlyList<(Type Type, IIdentifiedComponent Instance)> AllComponents { get; } =
            new (Type Type, IIdentifiedComponent Instance)[]
            {
                (typeof(IPostActionProcessor), new AddProjectsToSolutionPostAction()),
                (typeof(IPostActionProcessor), new AddReferencePostActionProcessor()),
                (typeof(IPostActionProcessor), new DotnetRestorePostActionProcessor()),
                (typeof(IPostActionProcessor), new ChmodPostActionProcessor()),
                (typeof(IPostActionProcessor), new InstructionDisplayPostActionProcessor()),
                (typeof(IPostActionProcessor), new ProcessStartPostActionProcessor()),
            };
    }
}
