// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.TemplateFiltering;

namespace Microsoft.TemplateEngine.Cli.TemplateResolution
{
    internal class ParameterMatchInfo : MatchInfo
    {
        internal ParameterMatchInfo(string name, string? value, MatchKind kind, MismatchKind mismatchKind = MismatchKind.NoMismatch, string? inputFormat = null) : base(name, value, kind)
        {
            InputFormat = inputFormat;
            ParameterMismatchKind = mismatchKind;
        }

        internal enum MismatchKind
        {
            NoMismatch,

            /// <summary>
            /// The parameter name is not defined in <see cref="ITemplateMetadata.ParameterDefinitions"/>.
            /// </summary>
            InvalidName,

            /// <summary>
            ///  The parameter value is different format that is supported by <see cref="ITemplateMetadata.ParameterDefinitions"/> parameter.
            /// </summary>
            InvalidValue,
        }

        public MismatchKind ParameterMismatchKind { get; }

        public string? InputFormat { get; }
    }
}
