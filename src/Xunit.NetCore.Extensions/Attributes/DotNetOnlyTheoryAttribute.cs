// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using Xunit.NetCore.Extensions;

namespace Xunit
{
    public class DotNetOnlyTheoryAttribute : TheoryAttribute
    {
        public DotNetOnlyTheoryAttribute(string additionalMessage = null)
        {
            if (!CustomXunitAttributesUtilities.IsBuiltAgainstDotNet)
            {
                Skip = "This test requires .NET Core to run.".AppendAdditionalMessage(additionalMessage);
            }
        }
    }
}
