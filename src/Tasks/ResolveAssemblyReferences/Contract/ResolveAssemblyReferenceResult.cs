// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using MessagePack;
using Microsoft.Build.Framework;

namespace Microsoft.Build.Tasks.ResolveAssemblyReferences.Contract
{
    internal sealed class ResolveAssemblyReferenceResult
    {
        public ResolveAssemblyReferenceResult()
        {
        }

        internal ResolveAssemblyReferenceResult(bool taskResult, ResolveAssemblyReferenceTaskOutput output)
        {
            TaskResult = taskResult;
            Response = new ResolveAssemblyReferenceResponse(output);
        }

        public bool TaskResult { get; set; }

        public ResolveAssemblyReferenceResponse Response { get; set; }

        public List<BuildEventArgs> BuildEvents { get; set; }

        internal ResolveAssemblyReferenceTaskOutput Output => new ResolveAssemblyReferenceTaskOutput(Response);
    }
}
