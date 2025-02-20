// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Threading.Tasks;
using Microsoft.Build.Execution;
using ElementLocation = Microsoft.Build.Construction.ElementLocation;

#nullable disable

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
