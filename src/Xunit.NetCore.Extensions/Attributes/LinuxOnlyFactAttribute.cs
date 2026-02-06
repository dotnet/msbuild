// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.Runtime.InteropServices;
using Xunit.NetCore.Extensions;

namespace Xunit
{
    public class LinuxOnlyFactAttribute : FactAttribute
    {
        public LinuxOnlyFactAttribute(string additionalMessage = null)
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                Skip = "This test requires Linux to run.".AppendAdditionalMessage(additionalMessage);
            }
        }
    }
}
