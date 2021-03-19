// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.Extensions.Tools.Internal;

namespace Microsoft.DotNet.Watcher.Tools
{
    public class DotNetWatchContext
    {
        public IReporter Reporter { get; init; } = NullReporter.Singleton;

        public ProcessSpec ProcessSpec { get; set; }

        public FileSet FileSet { get; set; }

        public int Iteration { get; set; } = -1;

        public FileItem? ChangedFile { get; set; }

        public bool RequiresMSBuildRevaluation { get; set; }

        public bool SuppressMSBuildIncrementalism { get; set; }

        public BrowserRefreshServer BrowserRefreshServer { get; set; }

        public LaunchSettingsProfile DefaultLaunchSettingsProfile { get; set; }
    }
}
