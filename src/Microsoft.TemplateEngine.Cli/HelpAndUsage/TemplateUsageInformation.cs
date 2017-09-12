// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using Microsoft.TemplateEngine.Abstractions;

namespace Microsoft.TemplateEngine.Cli.HelpAndUsage
{
    internal struct TemplateUsageInformation
    {
        public IReadOnlyList<InvalidParameterInfo> InvalidParameters;
        public IParameterSet AllParameters;
        public IReadOnlyList<string> UserParametersWithInvalidValues;
        public HashSet<string> UserParametersWithDefaultValues;
        public IReadOnlyDictionary<string, IReadOnlyList<string>> VariantsForCanonicals;
        public bool HasPostActionScriptRunner;
    }
}
