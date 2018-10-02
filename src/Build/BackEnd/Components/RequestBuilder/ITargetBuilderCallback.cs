// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Text;
using ElementLocation = Microsoft.Build.Construction.ElementLocation;
using Microsoft.Build.Shared;
using Microsoft.Build.Execution;
using System.Threading.Tasks;

namespace Microsoft.Build.BackEnd
{
    /// <summary>
    /// Interface implemented by the Target Builder which allows tasks to invoke targets and build projects.
    /// </summary>
    internal interface ITargetBuilderCallback : IRequestBuilderCallback
    {
        /// <summary>
        /// Invokes the specified targets using Dev9 behavior.  
        /// </summary>
        /// <param name="targets">The targets to build.</param>
        /// <param name="continueOnError">True to continue building the remaining targets if one fails.</param>
        /// <param name="referenceLocation">The <see cref="ElementLocation"/> of the reference.</param>
        /// <returns>The results for each target.</returns>
        /// <remarks>
        /// The target is run using the data context of the Project, rather than the data context 
        /// of the current target.  This has the following effects:
        /// 1. Data visible to the CALLING target at the time it was first invoked is the only
        ///    data which the CALLED target can see.  No changes made between the time the CALLING
        ///    target starts and the CALLED target starts are visible to the CALLED target.
        /// 2. Items and Properties modified by the CALLED target are not visible to the CALLING
        ///    target, even after the CALLED target returns.  However, any changes made to
        ///    items and properties by the CALLING target will override any changes made by the
        ///    CALLED target.
        /// </remarks>
        Task<ITargetResult[]> LegacyCallTarget(string[] targets, bool continueOnError, ElementLocation referenceLocation);
    }
}
