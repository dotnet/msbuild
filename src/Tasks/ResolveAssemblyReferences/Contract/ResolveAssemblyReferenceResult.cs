// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using MessagePack;
using Microsoft.Build.Framework;
using System.Collections.Generic;

namespace Microsoft.Build.Tasks.ResolveAssemblyReferences.Contract
{
    [MessagePackObject]
    public sealed class ResolveAssemblyReferenceResult
    {
        public ResolveAssemblyReferenceResult()
        {
        }

        internal ResolveAssemblyReferenceResult(bool taskResult, ResolveAssemblyReferenceTaskOutput output)
        {
            TaskResult = taskResult;
            Response = new ResolveAssemblyReferenceResponse(output);
        }

        [Key(0)]
        public bool TaskResult { get; set; }
        [Key(1)]
        public ResolveAssemblyReferenceResponse Response { get; set; }

        [Key(2)]
        public List<LazyFormattedBuildEventArgs> BuildEventArgs { get; set; }

        [IgnoreMember]
        internal ResolveAssemblyReferenceTaskOutput Output => new ResolveAssemblyReferenceTaskOutput(Response);
    }
}
