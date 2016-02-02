// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.DotNet.ProjectModel.Server
{
    public class MessageTypes
    {
        // Incoming
        public const string Initialize = nameof(Initialize);
        public const string ChangeConfiguration = nameof(ChangeConfiguration);
        public const string RefreshDependencies = nameof(RefreshDependencies);
        public const string RestoreComplete = nameof(RestoreComplete);
        public const string FilesChanged = nameof(FilesChanged);
        public const string GetDiagnostics = nameof(GetDiagnostics);
        public const string ProtocolVersion = nameof(ProtocolVersion);

        // Outgoing
        public const string Error = nameof(Error);
        public const string ProjectInformation = nameof(ProjectInformation);
        public const string Diagnostics = nameof(Diagnostics);
        public const string DependencyDiagnostics = nameof(DependencyDiagnostics);
        public const string Dependencies = nameof(Dependencies);
        public const string CompilerOptions = nameof(CompilerOptions);
        public const string References = nameof(References);
        public const string Sources = nameof(Sources);
    }
}
