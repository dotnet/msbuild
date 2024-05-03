// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Construction;

namespace Microsoft.Build.Execution
{
    internal interface IElementWithLocation
    {
        string Name { get; }
        string Value { get; }
        ElementLocation Location { get; }
    }
}
