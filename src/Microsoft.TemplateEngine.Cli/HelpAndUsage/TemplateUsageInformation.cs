// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

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
