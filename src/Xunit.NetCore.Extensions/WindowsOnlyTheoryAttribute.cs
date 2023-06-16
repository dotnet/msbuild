// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.InteropServices;

namespace Xunit.NetCore.Extensions
{
    /// <summary>
    /// This test should be run only on Windows.
    /// </summary>
    public class WindowsOnlyTheoryAttribute : TheoryAttribute
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="WindowsOnlyTheoryAttribute"/> class.
        /// </summary>
        /// <param name="additionalMessage">The additional message that is appended to skip reason, when test is skipped.</param>
        public WindowsOnlyTheoryAttribute(string? additionalMessage = null)
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                this.Skip = "This test requires Windows to run.".AppendAdditionalMessage(additionalMessage);
            }
        }
    }
}
