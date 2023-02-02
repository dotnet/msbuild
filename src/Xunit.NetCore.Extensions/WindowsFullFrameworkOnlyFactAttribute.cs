// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.InteropServices;

namespace Xunit.NetCore.Extensions
{
    /// <summary>
    ///  This test should be run only on Windows on full .NET Framework.
    /// </summary>
    public class WindowsFullFrameworkOnlyFactAttribute : FactAttribute
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="WindowsFullFrameworkOnlyFactAttribute"/> class.
        /// </summary>
        /// <param name="additionalMessage">The additional message that is appended to skip reason, when test is skipped.</param>
        public WindowsFullFrameworkOnlyFactAttribute(string? additionalMessage = null)
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                this.Skip = "This test only runs on Windows on full framework.".AppendAdditionalMessage(additionalMessage);
                return;
            }
            if (!CustomXunitAttributesUtilities.IsBuiltAgainstNetFramework)
            {
                this.Skip = "This test only runs on full framework.".AppendAdditionalMessage(additionalMessage);
            }
        }
    }
}
