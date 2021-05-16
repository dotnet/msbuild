// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System;
using System.Collections.Generic;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Cli.PostActionProcessors;
using Microsoft.TemplateEngine.Cli.TemplateSearch;
using Microsoft.TemplateSearch.Common;

namespace Microsoft.TemplateEngine.Cli
{
    public static class Components
    {
        private static readonly AddProjectsToSolutionPostAction AddProjectsToSolutionPostAction = new AddProjectsToSolutionPostAction();
        private static readonly AddReferencePostActionProcessor AddReferencePostActionProcessor = new AddReferencePostActionProcessor();
        private static readonly DotnetRestorePostActionProcessor DotnetRestorePostActionProcessor = new DotnetRestorePostActionProcessor();

        public static IReadOnlyList<(Type Type, IIdentifiedComponent Instance)> AllComponents { get; } =
            new (Type Type, IIdentifiedComponent Instance)[]
            {
                (typeof(IPostActionProcessor), AddProjectsToSolutionPostAction),
                (typeof(IPostActionProcessor2), AddProjectsToSolutionPostAction),
                (typeof(IPostActionProcessor), AddReferencePostActionProcessor),
                (typeof(IPostActionProcessor2), AddReferencePostActionProcessor),
                (typeof(IPostActionProcessor), DotnetRestorePostActionProcessor),
                (typeof(IPostActionProcessor2), DotnetRestorePostActionProcessor),
                (typeof(IPostActionProcessor), new ChmodPostActionProcessor()),
                (typeof(IPostActionProcessor), new InstructionDisplayPostActionProcessor()),
                (typeof(IPostActionProcessor), new ProcessStartPostActionProcessor()),

                (typeof(ITemplateSearchSource), new CliNuGetMetadataSearchSource()),
            };
    }
}
