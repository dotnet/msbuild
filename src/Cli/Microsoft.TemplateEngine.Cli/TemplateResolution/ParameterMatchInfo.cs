// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

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
            /// The parameter name is not defined in <see cref="ITemplateInfo.ParameterDefinitions"/>.
            /// </summary>
            InvalidName,

            /// <summary>
            ///  The parameter value is different format that is supported by <see cref="ITemplateInfo.ParameterDefinitions"/> parameter.
            /// </summary>
            InvalidValue,
        }

        public MismatchKind ParameterMismatchKind { get; }

        public string? InputFormat { get; }
    }
}
