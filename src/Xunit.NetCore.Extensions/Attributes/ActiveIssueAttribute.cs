// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System;
using System.Collections.Generic;
using Xunit.v3;

namespace Xunit
{
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Class | AttributeTargets.Assembly, AllowMultiple = true)]
    public class ActiveIssueAttribute : Attribute, ITraitAttribute
    {
        public ActiveIssueAttribute(string issue)
        {
        }

        public ActiveIssueAttribute(string issue, TestPlatforms platforms)
        {
        }

        public ActiveIssueAttribute(string issue, TargetFrameworkMonikers frameworks)
        {
        }

        public ActiveIssueAttribute(string issue, TestPlatforms platforms, TargetFrameworkMonikers frameworks)
        {
        }

        public IReadOnlyCollection<KeyValuePair<string, string>> GetTraits()
        {
            return new[] { new KeyValuePair<string, string>("Category", "failing") };
        }
    }
}
