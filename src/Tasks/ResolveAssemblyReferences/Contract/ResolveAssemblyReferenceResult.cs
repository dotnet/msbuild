// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using MessagePack;
using Microsoft.Build.Framework;
using System.Collections.Generic;

namespace Microsoft.Build.Tasks.ResolveAssemblyReferences.Contract
{
    // MessagePack requires transported objects to be public
    [MessagePackObject]
    public sealed class ResolveAssemblyReferenceResult
    {
        public ResolveAssemblyReferenceResult()
        {
        }

        internal ResolveAssemblyReferenceResult(bool taskResult, ResolveAssemblyReferenceTaskOutput output, ResolveAssemblyReferenceTaskInput input)
        {
            TaskResult = taskResult;
            Response = new ResolveAssemblyReferenceResponse(output);
            Request = new ResolveAssemblyReferenceRequest(input);
        }

        [Key(0)]
        public bool TaskResult { get; set; }
        [Key(1)]
        public ResolveAssemblyReferenceResponse Response { get; set; }

        [Key(2)]
        public int EventCount { get; set; }

        [Key(3)]
        public List<CustomBuildEventArgs> CustomBuildEvents { get; set; }

        [Key(4)]
        public List<BuildErrorEventArgs> BuildErrorEvents { get; set; }

        [Key(5)]
        public List<BuildMessageEventArgs> BuildMessageEvents { get; set; }

        [Key(6)]
        public List<BuildWarningEventArgs> BuildWarningEvents { get; set; }


        [Key(7)]
        public ResolveAssemblyReferenceRequest Request { get; set; }

        [IgnoreMember]
        internal ResolveAssemblyReferenceTaskOutput Output => new ResolveAssemblyReferenceTaskOutput(Response);

        [IgnoreMember]
        internal ResolveAssemblyReferenceTaskInput InputOutput => new ResolveAssemblyReferenceTaskInput(Request);
    }
}
