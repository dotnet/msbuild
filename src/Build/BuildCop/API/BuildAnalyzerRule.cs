// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Build.Experimental;

public class BuildAnalyzerRule
{
    public BuildAnalyzerRule(string id, string title, string description, string category, string messageFormat,
        BuildAnalyzerConfiguration defaultConfiguration)
    {
        Id = id;
        Title = title;
        Description = description;
        Category = category;
        MessageFormat = messageFormat;
        DefaultConfiguration = defaultConfiguration;
    }

    public string Id { get; }
    public string Title { get; }
    public string Description { get; }

    // or maybe enum? eval, syntax, etc
    public string Category { get; }
    public string MessageFormat { get; }
    public BuildAnalyzerConfiguration DefaultConfiguration { get; }
}
