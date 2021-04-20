// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using Microsoft.TemplateEngine.Abstractions.TemplateFiltering;

namespace Microsoft.TemplateEngine.Cli.TemplateResolution
{
    internal class ParameterMatchInfo : MatchInfo
    {
        internal ParameterMatchInfo(string name, string? value, MatchKind kind, string? inputFormat = null) : base(name, value, kind)
        {
            InputFormat = inputFormat;
        }

        public string? InputFormat { get; }
    }
}
