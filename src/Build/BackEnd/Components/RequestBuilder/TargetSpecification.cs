// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//-----------------------------------------------------------------------
// </copyright>
// <summary>Implements the TargetSpecification class</summary>
//-----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Text;
using ElementLocation = Microsoft.Build.Construction.ElementLocation;
using Microsoft.Build.Shared;
using System.Diagnostics;

namespace Microsoft.Build.BackEnd
{
    /// <summary>
    /// Contains information about a target name and reference location.
    /// </summary>
    [DebuggerDisplay("Name={TargetName}")]
    internal class TargetSpecification
    {
        /// <summary>
        /// Construct a target specification.
        /// </summary>
        /// <param name="targetName">The name of the target</param>
        /// <param name="referenceLocation">The location from which it was referred.</param>
        internal TargetSpecification(string targetName, ElementLocation referenceLocation)
        {
            ErrorUtilities.VerifyThrowArgumentLength(targetName, "targetName");
            ErrorUtilities.VerifyThrowArgumentNull(referenceLocation, "referenceLocation");

            this.TargetName = targetName;
            this.ReferenceLocation = referenceLocation;
        }

        /// <summary>
        /// Gets or sets the target name            
        /// </summary>
        public string TargetName
        {
            get;
            private set;
        }

        /// <summary>
        /// Gets or sets the reference location
        /// </summary>
        public ElementLocation ReferenceLocation
        {
            get;
            private set;
        }
    }
}
