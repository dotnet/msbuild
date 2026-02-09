// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Runtime.InteropServices;
using Microsoft.Build.Shared;
using Xunit;

namespace Microsoft.Build.UnitTests.Shared;

public class WindowsNet35OnlyFactAttribute : FactAttribute
{
    private const string Message = "This test only runs on Windows under .NET Framework when .NET Framework 3.5 is installed.";

    public WindowsNet35OnlyFactAttribute(string? additionalMessage = null)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ||
            !RuntimeInformation.FrameworkDescription.StartsWith(".NET Framework", StringComparison.OrdinalIgnoreCase) ||
            FrameworkLocationHelper.GetPathToDotNetFrameworkV35(DotNetFrameworkArchitecture.Current) == null)
        {
            Skip = SkipMessage(additionalMessage);
        }
    }

    private static string SkipMessage(string? additionalMessage = null)
        => !string.IsNullOrWhiteSpace(additionalMessage) ? $"{Message} {additionalMessage}" : Message;
}
