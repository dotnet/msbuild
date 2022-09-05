// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;

namespace Microsoft.DotNet.Watcher
{
    internal class CommandLineOptions
    {
        public string Project { get; set; }
        public string LaunchProfile { get; set; }
        public bool Quiet { get; set; }
        public bool Verbose { get; set; }
        public bool List { get; set; }
        public bool NoHotReload { get; set; }

        public bool NonInteractive { get; set; }
        public IReadOnlyList<string> RemainingArguments { get; set; }

        public static bool IsPollingEnabled
        {
            get
            {
                var envVar = Environment.GetEnvironmentVariable("DOTNET_USE_POLLING_FILE_WATCHER");
                return envVar != null &&
                    (envVar.Equals("1", StringComparison.OrdinalIgnoreCase) ||
                     envVar.Equals("true", StringComparison.OrdinalIgnoreCase));
            }
        }
    }
}
