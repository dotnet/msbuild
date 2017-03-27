// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace Microsoft.NET.TestFramework
{
    public class PackageReference
    {
        public PackageReference(string id, string version, string path)
        {
            ID = id;
            Version = version;
            LocalPath = path;
        }

        public string ID { get; private set; }
        public string Version { get; private set; }
        public string LocalPath { get; private set; }

    }
}
