// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Xunit.NetCore.Extensions
{
    /// <summary>
    ///  This test should be run only on .NET (or .NET Core).
    /// </summary>
    public class DotNetOnlyFactAttribute : FactAttribute
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="DotNetOnlyFactAttribute"/> class.
        /// </summary>
        /// <param name="additionalMessage">The additional message that is appended to skip reason, when test is skipped.</param>
        public DotNetOnlyFactAttribute(string? additionalMessage = null)
        {
            if (!CustomXunitAttributesUtilities.IsBuiltAgainstDotNet)
            {
                this.Skip = "This test only runs on .NET.".AppendAdditionalMessage(additionalMessage);
            }
        }
    }
}
