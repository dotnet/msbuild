// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Build.Experimental.BuildCop;

public class CustomConfigurationData(string ruleId)
{
    public static CustomConfigurationData Null { get; } = new(string.Empty);

    public static bool NotNull(CustomConfigurationData data) => !Null.Equals(data);

    public string RuleId { get; init; } = ruleId;
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
