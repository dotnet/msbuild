// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using NuGet.Common;
using NuGet.ProjectModel;

namespace Microsoft.DotNet.Cli.Utils
{
    public static class LockFileFormatExtensions
    {
        public static async Task<LockFile> ReadWithLock(this LockFileFormat subject, string path)
        {
            return await ConcurrencyUtilities.ExecuteWithFileLockedAsync(
                path, 
                lockedToken =>
                {
                    if (!File.Exists(path))
                    {
                        throw new GracefulException(string.Join(
                            Environment.NewLine,
                            string.Format(LocalizableStrings.FileNotFound, path),
                            LocalizableStrings.ProjectNotRestoredOrRestoreFailed));
                    }
                    
                    var lockFile = FileAccessRetrier.RetryOnFileAccessFailure(() => subject.Read(path), LocalizableStrings.CouldNotAccessAssetsFile);

                    return lockFile;       
                },
                CancellationToken.None);
        }
    }
}
