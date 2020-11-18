// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Build.BuildEngine
{
    /// <summary>
    /// Just an empty interface that is "implemented" by BuildPropertyGroup, BuildItemGroup, and Choose.
    /// It's just so we can pass these objects around as similar things.  The other alternative would
    /// have been just to use "Object", but that's even less strongly typed.
    /// </summary>
    /// <owner>DavidLe, RGoel</owner>
    internal interface IItemPropertyGrouping
    {
    }
}
