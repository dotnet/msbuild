// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Shared;

namespace Microsoft.Build.Evaluation
{
    internal interface IProperty2 : IProperty
    {
        string GetEvaluatedValueEscaped(IElementLocation location);
    }
}
