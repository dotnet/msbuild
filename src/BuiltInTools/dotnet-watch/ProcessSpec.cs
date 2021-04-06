// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using Microsoft.DotNet.Watcher.Internal;

namespace Microsoft.DotNet.Watcher
{
    public class ProcessSpec
    {
        public string Executable { get; set; }
        public string WorkingDirectory { get; set; }
        public ProcessSpecEnvironmentVariables EnvironmentVariables { get; } = new();

        public IReadOnlyList<string> Arguments { get; set; }
        public string EscapedArguments { get; set; }
        public OutputCapture OutputCapture { get; set; }

        public string ShortDisplayName()
            => Path.GetFileNameWithoutExtension(Executable);

        public bool IsOutputCaptured => OutputCapture != null;

        public DataReceivedEventHandler OnOutput { get; set; }

        public CancellationToken CancelOutputCapture { get; set; }

        public sealed class ProcessSpecEnvironmentVariables : Dictionary<string, string>
        {
            public List<string> DotNetStartupHooks { get; } = new();
            public List<string> AspNetCoreHostingStartupAssemblies { get; } = new();
        }
    }
}
