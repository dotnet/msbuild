// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Cli.PostActionProcessors;

namespace Microsoft.TemplateEngine.Cli
{
    public static class Components
    {
        public static IReadOnlyList<(Type Type, IIdentifiedComponent Instance)> AllComponents { get; } =
            new (Type Type, IIdentifiedComponent Instance)[]
            {
                (typeof(IPostActionProcessor), new ChmodPostActionProcessor()),
                (typeof(IPostActionProcessor), new InstructionDisplayPostActionProcessor()),
                (typeof(IPostActionProcessor), new ProcessStartPostActionProcessor()),
                (typeof(IPostActionProcessor), new AddJsonPropertyPostActionProcessor())
            };
    }
}
