// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using Microsoft.Build.Framework;

namespace Microsoft.Build.Tasks.ResolveAssemblyReferences.Contract
{
    internal sealed class ResolveAssemblyReferenceResult
    {
        public ResolveAssemblyReferenceResult()
        {
        }

        internal ResolveAssemblyReferenceResult(bool taskResult, ResolveAssemblyReferenceResponse response)
        {
            TaskResult = taskResult;
            Response = response;
        }

        public bool TaskResult { get; set; }

        public ResolveAssemblyReferenceResponse Response { get; set; }

        public List<BuildEventArgs> BuildEvents { get; set; }
    }
}
