// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Threading.Tasks;

namespace Microsoft.DotNet.Cli.Utils
{
    public static class FileAccessRetrier
    {
        public static async Task<T> RetryOnFileAccessFailure<T>(Func<T> func, int maxRetries = 3000, TimeSpan sleepDuration = default(TimeSpan)) 
        {
            var attemptsLeft = maxRetries;

            if (sleepDuration == default(TimeSpan))
            {
                sleepDuration = TimeSpan.FromMilliseconds(10);
            }
            
            while (true)
            {
                if (attemptsLeft < 1)
                {
                    throw new InvalidOperationException(LocalizableStrings.CouldNotAccessAssetsFile);
                }

                attemptsLeft--;

                try
                {
                    return func();
                }
                catch (UnauthorizedAccessException)
                {
                    // This can occur when the file is being deleted
                    // Or when an admin user has locked the file
                    await Task.Delay(sleepDuration);

                    continue;
                }
                catch (IOException)
                {
                    await Task.Delay(sleepDuration);

                    continue;
                }
            }
        }
    }
}
