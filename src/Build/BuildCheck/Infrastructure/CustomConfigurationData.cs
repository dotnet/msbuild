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
public class CustomConfigurationData
{
    public static CustomConfigurationData Null { get; } = new(string.Empty);

    public static bool NotNull(CustomConfigurationData data) => !Null.Equals(data);

    public CustomConfigurationData(string ruleId)
    {
        RuleId = ruleId;
    }

    public CustomConfigurationData(string ruleId, Dictionary<string, string> properties)
    {
        RuleId = ruleId;
        ConfigurationData = properties;
    }

    /// <summary>
    /// Identifier of the rule that the configuration data is for.
    /// </summary>
    public string RuleId { get; init; }

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

        var customConfigObj = (CustomConfigurationData) obj;

        if (customConfigObj.RuleId != RuleId)
        {
            return false;
        }

        // validate keys and values
        if (customConfigObj.ConfigurationData != null && ConfigurationData != null && ConfigurationData.Count == customConfigObj.ConfigurationData.Count)
        {
            foreach (var keyVal in customConfigObj.ConfigurationData)
            {
                if(!ConfigurationData.TryGetValue(keyVal.Key, out string value) || value != keyVal.Value)
                {
                    return false;
                }
            }
        }
        else if (customConfigObj.ConfigurationData == null && ConfigurationData == null)
        {
            return true;
        }
        else
        {
            return false;
        }

        return true;
    }

    public override int GetHashCode()
    {
        if (!NotNull(this))
        {
            return 0;
        }

        var hashCode = RuleId.GetHashCode();
        if (ConfigurationData != null)
        {
            foreach (var keyVal in ConfigurationData)
            {
                hashCode = hashCode + keyVal.Key.GetHashCode() + keyVal.Value.GetHashCode();
            }
        }

        return hashCode;
    }
}
