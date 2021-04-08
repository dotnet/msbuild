// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using Microsoft.TemplateEngine.Abstractions;

namespace Microsoft.TemplateEngine.Cli.HelpAndUsage
{
    internal struct TemplateUsageInformation
    {
        internal IReadOnlyList<InvalidParameterInfo> InvalidParameters;
        internal IParameterSet AllParameters;
        internal IReadOnlyList<string> UserParametersWithInvalidValues;
        internal HashSet<string> UserParametersWithDefaultValues;
        internal IReadOnlyDictionary<string, IReadOnlyList<string>> VariantsForCanonicals;
        internal bool HasPostActionScriptRunner;
    }
}
