// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace Microsoft.Build.Logging;

public static class ArchiveFileEventArgsExtensions
{
    /// <summary>
    /// Helper method that allows to subscribe to <see cref="IBuildEventArgsReaderNotifications.ArchiveFileEncountered"/> event via <see cref="IBuildEventArgsReaderNotifications.StringReadDone"/> event handler.
    ///
    /// This applies only when subscriber is OK with greedy reading entire content of the file and is interested only in the individual strings (e.g. for sensitive data redaction purposes),
    ///  without distinction what each individual string means (e.g. they do not care about distinction between path and content or between individual files - they just need all textual data).
    ///
    /// <example>
    /// <code>
    /// private void OnStringReadDone(StringReadEventArgs e)
    /// {
    ///     e.StringToBeUsed = e.StringToBeUsed.Replace("foo", "bar");
    /// }
    ///
    /// private void SubscribeToEvents()
    /// {
    ///     reader.StringReadDone += OnStringReadDone;
    ///     reader.ArchiveFileEncountered += ((Action&lt;StringReadEventArgs&gt;)OnStringReadDone).ToArchiveFileHandler();
    /// }
    /// </code>
    /// </example>
    /// </summary>
    public static Action<ArchiveFileEventArgs> ToArchiveFileHandler(this Action<StringReadEventArgs> stringHandler)
    {
        return args =>
        {
            var archiveFile = args.ArchiveData.ToArchiveFile();
            var pathArgs = new StringReadEventArgs(archiveFile.FullPath);
            stringHandler(pathArgs);
            var contentArgs = new StringReadEventArgs(archiveFile.Content);
            stringHandler(contentArgs);

            if(pathArgs.StringToBeUsed != pathArgs.OriginalString ||
               contentArgs.StringToBeUsed != contentArgs.OriginalString)
            {
                args.ArchiveData = new ArchiveFile(pathArgs.StringToBeUsed, contentArgs.StringToBeUsed);
            }
            else
            {
                args.ArchiveData = archiveFile;
            }
        };
    }
}
