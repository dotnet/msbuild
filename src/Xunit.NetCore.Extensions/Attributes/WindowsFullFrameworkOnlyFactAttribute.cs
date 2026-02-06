// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.Runtime.InteropServices;
using Xunit.NetCore.Extensions;

namespace Xunit
{
    public class WindowsFullFrameworkOnlyFactAttribute : FactAttribute
    {
        public WindowsFullFrameworkOnlyFactAttribute(string additionalMessage = null)
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                Skip = "This test requires Windows to run.".AppendAdditionalMessage(additionalMessage);
            }
            else if (!CustomXunitAttributesUtilities.IsBuiltAgainstNetFramework)
            {
                Skip = "This test requires .NET Framework to run.".AppendAdditionalMessage(additionalMessage);
            }
        }
    }
}
