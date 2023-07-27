// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.


using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Microsoft.DotNet.Watcher.Internal;

namespace Microsoft.DotNet.Watcher
{
    internal sealed class ProcessSpec
    {
        internal sealed class ProcessSpecEnvironmentVariables : Dictionary<string, string>
        {
            public List<string> DotNetStartupHooks { get; } = new();
            public List<string> AspNetCoreHostingStartupAssemblies { get; } = new();

        }

        public string? Executable { get; set; }
        public string? WorkingDirectory { get; set; }
        public ProcessSpecEnvironmentVariables EnvironmentVariables { get; } = new();
        public IReadOnlyList<string>? Arguments { get; set; }
        public string? EscapedArguments { get; set; }
        public OutputCapture? OutputCapture { get; set; }
        public DataReceivedEventHandler? OnOutput { get; set; }
        public CancellationToken CancelOutputCapture { get; set; }

        [MemberNotNullWhen(true, nameof(OutputCapture))]
        public bool IsOutputCaptured => OutputCapture != null;

        public string? ShortDisplayName()
            => Path.GetFileNameWithoutExtension(Executable);
    }
}
