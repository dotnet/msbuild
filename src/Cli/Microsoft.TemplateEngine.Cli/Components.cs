// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

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
            };
    }
}
