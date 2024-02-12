// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;

namespace Microsoft.Build.Experimental;

public abstract class BuildAnalyzer
{
    public abstract string FriendlyName { get; }
    public abstract ImmutableArray<BuildAnalysisRule> SupportedRules { get; }
    public abstract void Initialize(ConfigurationContext configurationContext);

    public abstract void RegisterActions(IBuildAnalyzerContext context);
}
