// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Threading;

namespace Microsoft.DotNet.ProjectModel.Utilities
{
    public class ResilientFileStreamOpener
    {
        public static FileStream OpenFile(string filepath)
        {
            return OpenFile(filepath, 0);
        }

        public static FileStream OpenFile(string filepath, int retry)
        {
            if (retry < 0)
            {
                throw new ArgumentException("Retry can't be fewer than 0", nameof(retry));
            }

            var count = 0;
            while (true)
            {
                try
                {
                    return new FileStream(filepath, FileMode.Open, FileAccess.Read, FileShare.Read);
                }
                catch
                {
                    if (++count > retry)
                    {
                        throw;
                    }
                    else
                    {
                        Thread.Sleep(500);
                    }
                }
            }
        }
    }
}