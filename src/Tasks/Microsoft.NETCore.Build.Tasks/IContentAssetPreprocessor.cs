// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using NuGet.Common;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace Microsoft.NETCore.Build.Tasks
{
    /// <summary>
    /// Resource for reading and writing content assets
    /// </summary>
    public interface IContentAssetPreprocessor
    {
        bool Process(string originalAssetPath, string packageId, string packageVersion, string ppOutputPath, out string pathToFinalAsset);
    }
}
