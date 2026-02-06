// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.Runtime.InteropServices;
using Xunit.NetCore.Extensions;

namespace Xunit
{
    public class UnixOnlyFactAttribute : FactAttribute
    {
        public UnixOnlyFactAttribute(string additionalMessage = null)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                Skip = "This test requires Unix to run.".AppendAdditionalMessage(additionalMessage);
            }
        }
    }
}
