// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.DotNet.Tools.Test.Utilities;
using System;
using System.Collections.Generic;
using System.Text;
using Xunit;

namespace Microsoft.NET.TestFramework
{
    public class RequiresSpecificFrameworkTheoryAttribute : TheoryAttribute
    {
        public RequiresSpecificFrameworkTheoryAttribute(string framework)
        {
            if (!EnvironmentInfo.SupportsTargetFramework(framework))
            {
                this.Skip = $"This test requires a shared framework that isn't present: {framework}";
            }
        }
    }
}
