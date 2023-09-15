// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace Microsoft.Build.Logging;

public static class ArchiveFileEventArgsExtensions
{
    public static Action<ArchiveFileEventArgs> ToArchiveFileHandler(this Action<StringReadEventArgs> stringHandler)
    {
        return args =>
        {
            var archiveFile = args.ObtainArchiveFile();
            var pathArgs = new StringReadEventArgs(archiveFile.FullPath);
            stringHandler(pathArgs);
            var contentArgs = new StringReadEventArgs(archiveFile.GetContent());
            stringHandler(contentArgs);

            args.SetResult(pathArgs.StringToBeUsed, contentArgs.StringToBeUsed);
        };
    }
}
