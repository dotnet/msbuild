// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.DotNet.ProjectModel.Resolution;

namespace Microsoft.DotNet.ProjectModel.Graph
{
    public class LockFilePatcher
    {
        private readonly LockFile _lockFile;
        private Dictionary<string, IList<LockFileTargetLibrary>> _msbuildTargetLibraries;
        private readonly LockFileReader _reader;

        public LockFilePatcher(LockFile lockFile, LockFileReader reader)
        {
            _lockFile = lockFile;
            _reader = reader;

            var msbuildProjectLibraries = lockFile.ProjectLibraries.Where(MSBuildDependencyProvider.IsMSBuildProjectLibrary);
            _msbuildTargetLibraries = msbuildProjectLibraries.ToDictionary(GetProjectLibraryKey, l => GetTargetsForLibrary(_lockFile, l));
        }

        public void Patch()
        {
            var exportFilePath = GetExportFilePath(_lockFile.LockFilePath);
            if (File.Exists(exportFilePath) && _msbuildTargetLibraries.Any())
            {
                var exportFile = _reader.ReadExportFile(exportFilePath);
                PatchLockWithExport(exportFile);
            }
            else
            {
                ThrowIfAnyMsbuildLibrariesPresent();
            }
        }

        public void ThrowIfAnyMsbuildLibrariesPresent()
        {
            if (_msbuildTargetLibraries.Any())
            {
                throw new LockFilePatchingException($"Lock file {_lockFile} contains msbuild projects but there is no export file");
            }
        }

        private void PatchLockWithExport(ExportFile exportFile)
        {
            if (_lockFile.Version != exportFile.Version)
            {
                throw new LockFilePatchingException($"Export file {exportFile.ExportFilePath} has a different version than the lock file {_lockFile.LockFilePath}");
            }

            var exportDict = exportFile.Exports.ToDictionary(GetTargetLibraryKey);

            var uncoveredLibraries = _msbuildTargetLibraries.Keys.Except(exportDict.Keys);
            if (uncoveredLibraries.Any())
            {
                throw new LockFilePatchingException($"Export {exportFile.ExportFilePath} does not provide exports for all the msbuild projects in {_lockFile.LockFilePath}");
            }

            foreach(var exportKey in exportDict.Keys)
            {
                var export = exportDict[exportKey];
                var librariesToPatch = _msbuildTargetLibraries[exportKey];

                foreach (var libraryToPatch in librariesToPatch)
                {
                    Patch(libraryToPatch, export);
                }
            }

            _lockFile.ExportFile = exportFile;
        }

        private static void Patch(LockFileTargetLibrary libraryToPatch, LockFileTargetLibrary export)
        {
            libraryToPatch.CompileTimeAssemblies = export.CompileTimeAssemblies;
            libraryToPatch.ContentFiles = export.ContentFiles;
            libraryToPatch.FrameworkAssemblies = export.FrameworkAssemblies;
            libraryToPatch.NativeLibraries = export.NativeLibraries;
            libraryToPatch.ResourceAssemblies = export.ResourceAssemblies;
            libraryToPatch.RuntimeAssemblies = export.RuntimeAssemblies;
        }

        private static IList<LockFileTargetLibrary> GetTargetsForLibrary(LockFile lockFile, LockFileProjectLibrary library)
        {
            return lockFile.Targets
                .SelectMany(
                    t => t.Libraries
                        .Where(
                            l => string.Equals(GetProjectLibraryKey(library), (GetTargetLibraryKey(l)))
                            )
                    )
                .ToList();
        }

        private static object TypeName(LockFileTargetLibrary library)
        {
            return library.Name + "/" + library.Version + "/" + library.Type;
        }

        private static string GetTargetLibraryKey(LockFileTargetLibrary library)
        {
            return library.Name + "/" + library.Version;
        }

        private static string GetProjectLibraryKey(LockFileProjectLibrary library)
        {
            return library.Name + "/" + library.Version;
        }

        private static string GetExportFilePath(string masterLockFilePath)
        {
            var parentDirectory = Directory.GetParent(masterLockFilePath).FullName;
            return Path.Combine(parentDirectory, ExportFile.ExportFileName);
        }
    }

    internal class LockFilePatchingException : Exception
    {
        public LockFilePatchingException(string message) : base(message)
        {
        }
    }

}
