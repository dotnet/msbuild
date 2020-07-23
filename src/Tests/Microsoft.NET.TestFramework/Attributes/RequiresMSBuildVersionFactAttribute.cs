// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using Xunit;

namespace Microsoft.NET.TestFramework
{
    public class RequiresMSBuildVersionFactAttribute : FactAttribute
    {
        public RequiresMSBuildVersionFactAttribute(string version)
        {
            RequiresMSBuildVersionTheoryAttribute.CheckForRequiredMSBuildVersion(this, version);
        }
    }
}
