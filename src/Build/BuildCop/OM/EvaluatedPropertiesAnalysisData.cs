// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using Microsoft.Build.BackEnd.Logging;

namespace Microsoft.Build.Experimental.BuildCop;
public class EvaluatedPropertiesAnalysisData : AnalysisData
{
    internal EvaluatedPropertiesAnalysisData(
        string projectFilePath,
        IReadOnlyDictionary<string, string> evaluatedProperties) :
        base(projectFilePath) => EvaluatedProperties = evaluatedProperties;

    public IReadOnlyDictionary<string, string> EvaluatedProperties { get; }
}
