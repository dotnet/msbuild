// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Text;
using Microsoft.Build.Framework;
using Microsoft.NET.HostModel.ComHost;

namespace Microsoft.NET.Build.Tasks
{
    public class GenerateRegFreeComManifest : TaskWithAssemblyResolveHooks
    {
        [Required]
        public string IntermediateAssembly { get; set; }

        [Required]
        public string ComHostName { get; set; }

        [Required]
        public string ClsidMapPath { get; set; }

        [Required]
        public string ComManifestPath { get; set; }

        public ITaskItem[] TypeLibraries { get; set; }

        protected override void ExecuteCore()
        {
            try
            {
                if (!TypeLibraryDictionaryBuilder.TryCreateTypeLibraryIdDictionary(
                    TypeLibraries,
                    out Dictionary<int, string> typeLibIdMap,
                    out IEnumerable<string> errors))
                {
                    foreach (string error in errors)
                    {
                        Log.LogError(error);
                    }
                    return;
                }

                RegFreeComManifest.CreateManifestFromClsidmap(
                    Path.GetFileNameWithoutExtension(IntermediateAssembly),
                    ComHostName,
                    FileUtilities.TryGetAssemblyVersion(IntermediateAssembly).ToString(),
                    ClsidMapPath,
                    ComManifestPath);
            }
            catch (TypeLibraryDoesNotExistException ex)
            {
                Log.LogError(Strings.TypeLibraryDoesNotExist, ex.Path);
            }
            catch (InvalidTypeLibraryException ex)
            {
                Log.LogError(Strings.InvalidTypeLibrary, ex.Path);
            }
        }
    }
}
