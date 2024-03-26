// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Build.Experimental.BuildCheck;

/// <summary>
/// Holder for the key-value pairs of unstructured data from .editorconfig file,
///  that were attribute to a particular rule, but were not recognized by the infrastructure.
/// The configuration data that is recognized by the infrastructure is passed as <see cref="BuildAnalyzerConfiguration"/>.
/// </summary>
/// <param name="ruleId"></param>
public class CustomConfigurationData(string ruleId)
{
    public static CustomConfigurationData Null { get; } = new(string.Empty);

    public static bool NotNull(CustomConfigurationData data) => !Null.Equals(data);

    /// <summary>
    /// Identifier of the rule that the configuration data is for.
    /// </summary>
    public string RuleId { get; init; } = ruleId;

    /// <summary>
    /// Key-value pairs of unstructured data from .editorconfig file.
    /// E.g. if in editorconfig file we'd have:
    /// [*.csrpoj]
    /// build_analyzer.microsoft.BC0101.name_of_targets_to_restrict = "Build,CoreCompile,ResolveAssemblyReferences"
    ///
    /// the ConfigurationData would be:
    /// "name_of_targets_to_restrict" -> "Build,CoreCompile,ResolveAssemblyReferences"
    /// </summary>
    public IReadOnlyDictionary<string, string>? ConfigurationData { get; init; }

    public override bool Equals(object? obj)
    {
        if (ReferenceEquals(null, obj))
        {
            return false;
        }

        if (ReferenceEquals(this, obj))
        {
            return true;
        }

        if (obj.GetType() != this.GetType())
        {
            return false;
        }

        return Equals((CustomConfigurationData)obj);
    }

    protected bool Equals(CustomConfigurationData other) => Equals(ConfigurationData, other.ConfigurationData);

    public override int GetHashCode() => (ConfigurationData != null ? ConfigurationData.GetHashCode() : 0);
}
