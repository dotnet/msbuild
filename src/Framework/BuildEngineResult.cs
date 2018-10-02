// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace Microsoft.Build.Framework
{
    /// <summary>
    /// This structure is used to return the result of the build and the target outputs.
    /// </summary>
    [Serializable]
    [SuppressMessage("Microsoft.Performance", "CA1815:OverrideEqualsAndOperatorEqualsOnValueTypes", Justification = "Would require a public API change -- currently we're trying to keep our surface area static.")]
    public struct BuildEngineResult
    {
        /// <summary>
        /// Did the build pass or fail
        /// </summary>
        private bool buildResult;

        /// <summary>
        /// Target outputs by project
        /// </summary>
        private List<IDictionary<string, ITaskItem[]>> targetOutputsPerProject;

        /// <summary>
        /// The constructor takes the result of the build and a list of the target outputs per project
        /// </summary>
        public BuildEngineResult(bool result, List<IDictionary<string, ITaskItem[]>> targetOutputsPerProject)
        {
            buildResult = result;
            this.targetOutputsPerProject = targetOutputsPerProject;
            if (this.targetOutputsPerProject == null)
            {
                this.targetOutputsPerProject = new List<IDictionary<string, ITaskItem[]>>();
            }
        }

        /// <summary>
        /// Did the build pass or fail. True means the build succeeded, False means the build failed.
        /// </summary>
        public bool Result
        {
            get
            {
                return buildResult;
            }
        }

        /// <summary>
        /// Outputs of the targets per project.
        /// </summary>
        public IList<IDictionary<string, ITaskItem[]>> TargetOutputsPerProject
        {
            get
            {
                return targetOutputsPerProject;
            }
        }
    }
}
