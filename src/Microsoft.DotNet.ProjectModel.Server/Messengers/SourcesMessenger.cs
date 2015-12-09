// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.DotNet.ProjectModel.Server.InternalModels;
using Microsoft.DotNet.ProjectModel.Server.Models;

namespace Microsoft.DotNet.ProjectModel.Server.Messengers
{
    internal class SourcesMessenger : Messenger<ProjectSnapshot>
    {
        public SourcesMessenger(Action<string, object> transmit)
            : base(MessageTypes.Sources, transmit)
        { }

        protected override bool CheckDifference(ProjectSnapshot local, ProjectSnapshot remote)
        {
            return remote.SourceFiles != null &&
                   Enumerable.SequenceEqual(local.SourceFiles, remote.SourceFiles);
        }

        protected override object CreatePayload(ProjectSnapshot local)
        {
            return new SourcesMessagePayload
            {
                Framework = local.TargetFramework.ToPayload(_resolver),
                Files = local.SourceFiles,
                GeneratedFiles = new Dictionary<string, string>()
            };
        }

        protected override void SetValue(ProjectSnapshot local, ProjectSnapshot remote)
        {
            remote.SourceFiles = local.SourceFiles;
        }

        private class SourcesMessagePayload
        {
            public FrameworkData Framework { get; set; }
            public IReadOnlyList<string> Files { get; set; }
            public IDictionary<string, string> GeneratedFiles { get; set; }
        }
    }
}
