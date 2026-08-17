// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.InteropServices;
using Xunit;
using Xunit.NetCore.Extensions;

namespace Microsoft.Build.UnitTests
{
    /// <summary>
    ///  This test should be run only on Windows, and when long path support is enabled.
    ///  It is possible to conditionally restrict the fact to be run only on full .NET Framework.
    /// </summary>
    public class LongPathSupportDisabledFactAttribute : FactAttribute
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="LongPathSupportDisabledFactAttribute"/> class.
        /// </summary>
        /// <param name="additionalMessage">The additional message that is appended to skip reason, when test is skipped.</param>
        /// <param name="fullFrameworkOnly"><see langword="true"/> if the test can be run only on full framework. The default value is <see langword="false"/>.</param>
        public LongPathSupportDisabledFactAttribute(string? additionalMessage = null, bool fullFrameworkOnly = false)
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                this.Skip = "This test only runs on Windows and when long path support is disabled.".AppendAdditionalMessage(additionalMessage);
                return;
            }

            if (fullFrameworkOnly && !CustomXunitAttributesUtilities.IsBuiltAgainstNetFramework)
            {
                this.Skip = "This test only runs on full .NET Framework and when long path support is disabled.".AppendAdditionalMessage(additionalMessage);
                return;
            }

            if (!NativeMethodsShared.IsMaxPathLegacyWindows())
            {
                this.Skip = "This test only runs when long path support is disabled.".AppendAdditionalMessage(additionalMessage);
            }
        }
    }
}
