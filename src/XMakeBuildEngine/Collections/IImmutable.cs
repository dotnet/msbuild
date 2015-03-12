// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//-----------------------------------------------------------------------
// </copyright>
// <summary>Interface indicating a type is immutable, to constrain generic types.</summary>
//-----------------------------------------------------------------------

namespace Microsoft.Build.Collections
{
    /// <summary>
    /// Interface indicating a type is immutable, to constrain generic types.
    /// </summary>
    /// <remarks>
    /// This can be internal as it is a constraint only on internal collections.
    /// </remarks>
    internal interface IImmutable
    {
    }
}
