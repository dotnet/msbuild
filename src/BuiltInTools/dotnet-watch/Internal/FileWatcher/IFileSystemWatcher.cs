// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable


namespace Microsoft.DotNet.Watcher.Internal
{
    internal interface IFileSystemWatcher : IDisposable
    {
        event EventHandler<(string filePath, bool newFile)> OnFileChange;

        event EventHandler<Exception> OnError;

        string BasePath { get; }

        bool EnableRaisingEvents { get; set; }
    }
}
