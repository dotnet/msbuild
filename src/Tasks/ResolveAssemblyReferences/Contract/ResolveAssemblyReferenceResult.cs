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

        internal ResolveAssemblyReferenceResult(bool taskResult, ResolveAssemblyReferenceTaskOutput output, ResolveAssemblyReferenceTaskInput input)
        {
            TaskResult = taskResult;
            Response = new ResolveAssemblyReferenceResponse(output);
            Request = new ResolveAssemblyReferenceRequest(input);
        }

        public bool TaskResult { get; set; }

        public ResolveAssemblyReferenceResponse Response { get; set; }

        public int EventCount { get; set; }

        public List<CustomBuildEventArgs> CustomBuildEvents { get; set; }

        public List<BuildErrorEventArgs> BuildErrorEvents { get; set; }

        public List<BuildMessageEventArgs> BuildMessageEvents { get; set; }

        public List<BuildWarningEventArgs> BuildWarningEvents { get; set; }

        public ResolveAssemblyReferenceRequest Request { get; set; }

        internal ResolveAssemblyReferenceTaskOutput Output => new ResolveAssemblyReferenceTaskOutput(Response);

        internal ResolveAssemblyReferenceTaskInput InputOutput => new ResolveAssemblyReferenceTaskInput(Request);
    }
}
