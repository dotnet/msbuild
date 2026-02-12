// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

namespace Xunit
{
    public sealed class ConditionalFactAttribute : FactAttribute
    {
        public ConditionalFactAttribute(params string[] conditionMemberNames)
        {
            if (conditionMemberNames.Length > 0)
            {
                Skip = $"Condition '{conditionMemberNames[0]}' not met";
                SkipUnless = conditionMemberNames[0];
            }
        }
    }
}
