// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.InteropServices;
using Microsoft.Build.Shared;
using Xunit;

namespace Microsoft.Build.UnitTests
{
    /// <summary>
    /// A custom <see cref="FactAttribute"/> that skips the test if not running on macOS.
    /// </summary>
    public sealed class OSXOnlyFactAttribute : FactAttribute
    {
        public OSXOnlyFactAttribute(string? additionalMessage = null)
        {
            if (!NativeMethodsShared.IsOSX)
            {
                Skip = additionalMessage != null 
                    ? $"This test only runs on macOS. {additionalMessage}"
                    : "This test only runs on macOS.";
            }
        }
    }
}
