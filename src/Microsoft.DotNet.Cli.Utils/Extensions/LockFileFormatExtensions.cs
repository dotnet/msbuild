// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Threading;
using System.Threading.Tasks;

using NuGet.Common;
using NuGet.ProjectModel;

namespace Microsoft.DotNet.Cli.Utils
{
    public static class LockFileFormatExtensions
    {

        private const int NumberOfRetries = 3000;
        
        private static readonly TimeSpan SleepDuration = TimeSpan.FromMilliseconds(10);

        public static async Task<LockFile> ReadWithLock(this LockFileFormat subject, string path)
        {
            return await ConcurrencyUtilities.ExecuteWithFileLockedAsync(
                path, 
                lockedToken =>
                {
                    var lockFile = FileAccessRetrier.RetryOnFileAccessFailure(() => subject.Read(path));

                    return lockFile;       
                },
                CancellationToken.None);
        }
    }
}
