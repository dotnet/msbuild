// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Reflection;

namespace Microsoft.DotNet.ProjectModel.Compilation
{
    public static class LibraryAssetExtensions
    {
        private const string NativeImageSufix = ".ni";

        public static AssemblyName GetAssemblyName(this LibraryAsset asset)
        {
            var name = asset.Name;
            if (asset.Name.EndsWith(NativeImageSufix))
            {
                name = name.Substring(0, name.Length - NativeImageSufix.Length);
            }

            return new AssemblyName(name);
        }

        public static string GetTransformedFile(this LibraryAsset asset, string tempLocation, string tempName = null)
        {
            if (asset.Transform == null)
            {
                return asset.ResolvedPath;
            }

            tempName = tempName ?? Path.GetFileName(asset.RelativePath);
            using (var input = File.OpenRead(asset.ResolvedPath))
            {
                var transformedName = Path.Combine(tempLocation, tempName);
                using (var output = File.OpenWrite(transformedName))
                {
                    asset.Transform(input, output);
                }
                return transformedName;
            }
        }

        public static Stream GetTransformedStream(this LibraryAsset asset)
        {
            if (asset.Transform == null)
            {
                return File.OpenRead(asset.ResolvedPath);
            }

            using (var input = File.OpenRead(asset.ResolvedPath))
            {
                var output = new MemoryStream();
                asset.Transform(input, output);
                return output;
            }
        }
    }
}
