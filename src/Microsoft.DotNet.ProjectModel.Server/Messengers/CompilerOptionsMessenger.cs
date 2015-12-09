// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using Microsoft.DotNet.ProjectModel.Server.InternalModels;
using Microsoft.DotNet.ProjectModel.Server.Models;

namespace Microsoft.DotNet.ProjectModel.Server.Messengers
{
    internal class CompilerOptionsMessenger : Messenger<ProjectSnapshot>
    {
        public CompilerOptionsMessenger(Action<string, object> transmit)
            : base(MessageTypes.CompilerOptions, transmit)
        { }

        protected override bool CheckDifference(ProjectSnapshot local, ProjectSnapshot remote)
        {
            return remote.CompilerOptions != null &&
                   Equals(local.CompilerOptions, remote.CompilerOptions);
        }

        protected override object CreatePayload(ProjectSnapshot local)
        {
            return new CompilationOptionsMessagePayload
            {
                Framework = local.TargetFramework.ToPayload(_resolver),
                Options = local.CompilerOptions
            };
        }

        protected override void SetValue(ProjectSnapshot local, ProjectSnapshot remote)
        {
            remote.CompilerOptions = local.CompilerOptions;
        }

        private class CompilationOptionsMessagePayload
        {
            public FrameworkData Framework { get; set; }

            public CommonCompilerOptions Options { get; set; }
        }
    }
}
