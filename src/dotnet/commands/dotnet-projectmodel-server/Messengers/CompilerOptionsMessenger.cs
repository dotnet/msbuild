// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using Microsoft.DotNet.ProjectModel.Server.Models;

namespace Microsoft.DotNet.ProjectModel.Server.Messengers
{
    internal class CompilerOptionsMessenger : Messenger<ProjectContextSnapshot>
    {
        public CompilerOptionsMessenger(Action<string, object> transmit)
            : base(MessageTypes.CompilerOptions, transmit)
        { }

        protected override bool CheckDifference(ProjectContextSnapshot local, ProjectContextSnapshot remote)
        {
            return remote.CompilerOptions != null &&
                   Equals(local.CompilerOptions, remote.CompilerOptions);
        }

        protected override void SendPayload(ProjectContextSnapshot local, Action<object> send)
        {
            send(new CompilationOptionsMessage
            {
                Framework = local.TargetFramework.ToPayload(),
                Options = local.CompilerOptions
            });
        }

        protected override void SetValue(ProjectContextSnapshot local, ProjectContextSnapshot remote)
        {
            remote.CompilerOptions = local.CompilerOptions;
        }

        private class CompilationOptionsMessage
        {
            public FrameworkData Framework { get; set; }

            public CommonCompilerOptions Options { get; set; }
        }
    }
}
