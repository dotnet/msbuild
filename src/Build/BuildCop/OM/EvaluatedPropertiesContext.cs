// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using Microsoft.Build.BackEnd.Logging;

namespace Microsoft.Build.Experimental.BuildCop;
public class EvaluatedPropertiesContext : BuildAnalysisContext
{
    internal EvaluatedPropertiesContext(
        LoggingContext loggingContext,
        IReadOnlyDictionary<string, string> evaluatedProperties,
        string projectFilePath) :
        base(loggingContext) => (EvaluatedProperties, ProjectFilePath) =
        (evaluatedProperties, projectFilePath);

    public IReadOnlyDictionary<string, string> EvaluatedProperties { get; }

    public string ProjectFilePath { get; }
}
