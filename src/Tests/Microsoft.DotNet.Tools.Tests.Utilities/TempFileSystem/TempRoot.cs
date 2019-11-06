// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
 
namespace Microsoft.DotNet.Tools.Test.Utilities
{
    public sealed class TempRoot : IDisposable
    {
        private static readonly bool DoDispose;
        private readonly List<IDisposable> _temps = new List<IDisposable>();
        public static readonly string Root;
 
        static TempRoot()
        {
            Root = RepoDirectoriesProvider.TestWorkingFolder;
            DoDispose = false;

            Directory.CreateDirectory(Root);
        }
 
        public void Dispose()
        {
            if (!DoDispose || _temps == null) return;

            DisposeAll(_temps);
            _temps.Clear();
        }
 
        private static void DisposeAll(IEnumerable<IDisposable> temps)
        {
            foreach (var temp in temps)
            {
                try
                {
                    temp?.Dispose();
                }
                catch
                {
                    // ignore
                }
            }
        }
 
        internal static void CreateStream(string fullPath)
        {
            using (var file = new FileStream(fullPath, FileMode.CreateNew)) { }
        }
    }
}
