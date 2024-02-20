// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Build.Experimental;

namespace Microsoft.Build.Analyzers.Infrastructure;

/// <summary>
/// Counterpart type for BuildAnalyzerConfiguration - with all properties non-nullable
/// </summary>
internal sealed class BuildAnalyzerConfigurationInternal
{
    public LifeTimeScope LifeTimeScope { get; internal init; }
    public EvaluationAnalysisScope EvaluationAnalysisScope { get; internal init; }
    public BuildAnalyzerResultSeverity Severity { get; internal init; }
    public bool IsEnabled { get; internal init; }
}
