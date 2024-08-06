// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Build.Experimental.BuildCheck;

/// <summary>
/// For datasource events that can differentiate from where exactly they originate - e.g.
///  For a condition string or AST - was that directly in the checked project or imported?
///
/// Ignored by infrastructure if the current datasource doesn't support this level of setting.
/// </summary>
public enum EvaluationCheckScope
{
    /// <summary>
    /// Only the data from currently checked project will be sent to the check. Imports will be discarded.
    /// </summary>
    ProjectOnly,

    /// <summary>
    /// Only the data from currently checked project and imports from files under the entry project or solution will be sent to the checks. Other imports will be discarded.
    /// </summary>
    ProjectWithImportsFromCurrentWorkTree,

    /// <summary>
    /// Imports from SDKs will not be sent to the check. Other imports will be sent.
    /// </summary>
    ProjectWithImportsWithoutSdks,

    /// <summary>
    /// All data will be sent to the check.
    /// </summary>
    ProjectWithAllImports,
}
