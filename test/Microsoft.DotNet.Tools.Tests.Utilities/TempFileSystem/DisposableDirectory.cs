// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
 
namespace Microsoft.DotNet.Tools.Test.Utilities
{
    public sealed class DisposableDirectory : TempDirectory, IDisposable
    {
        public DisposableDirectory(TempRoot root)
            : base(root)
        {
        }
 
        public void Dispose()
        {
            if (Path != null && Directory.Exists(Path))
            {
                try
                {
                    Directory.Delete(Path, recursive: true);
                }
                catch
                {
                }
            }
        }
    }
}
