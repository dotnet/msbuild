// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

// THE ASSEMBLY BUILT FROM THIS SOURCE FILE HAS BEEN DEPRECATED FOR YEARS. IT IS BUILT ONLY TO PROVIDE
// BACKWARD COMPATIBILITY FOR API USERS WHO HAVE NOT YET MOVED TO UPDATED APIS. PLEASE DO NOT SEND PULL
// REQUESTS THAT CHANGE THIS FILE WITHOUT FIRST CHECKING WITH THE MAINTAINERS THAT THE FIX IS REQUIRED.

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
