// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using FluentAssertions;
using Xunit;
using Microsoft.DotNet.Cli.Compiler.Common;
using Microsoft.DotNet.ProjectModel.Compilation;
using System.IO;
using System;
using System.Linq;
using System.Collections.Generic;

namespace Microsoft.DotNet.Cli.Compiler.Common.Tests
{
    public class GivenThatICopyLibraryAssets
    {
        [Fact]
        public void LibraryAsset_CopyTo_Clears_Readonly()
        {
            var libraryAsset = GetMockLibraryAsset(nameof(LibraryAsset_CopyTo_Clears_Readonly));
            MakeFileReadonly(libraryAsset.ResolvedPath);

            IEnumerable<LibraryAsset> assets = new LibraryAsset[] { libraryAsset };

            var outputDirectory = Path.Combine(AppContext.BaseDirectory,$"{nameof(LibraryAsset_CopyTo_Clears_Readonly)}_out");
            assets.CopyTo(outputDirectory);

            var copiedFile = Directory.EnumerateFiles(outputDirectory, Path.GetFileName(libraryAsset.RelativePath)).First();
            IsFileReadonly(copiedFile).Should().BeFalse();
        }

        [Fact]
        public void LibraryAsset_StructuredCopyTo_Clears_Readonly()
        {
            var libraryAsset = GetMockLibraryAsset(nameof(LibraryAsset_StructuredCopyTo_Clears_Readonly));
            MakeFileReadonly(libraryAsset.ResolvedPath);

            IEnumerable<LibraryAsset> assets = new LibraryAsset[] { libraryAsset };

            var intermediateDirectory = Path.Combine(AppContext.BaseDirectory,$"{nameof(LibraryAsset_StructuredCopyTo_Clears_Readonly)}_obj");
            var outputDirectory = Path.Combine(AppContext.BaseDirectory,$"{nameof(LibraryAsset_StructuredCopyTo_Clears_Readonly)}_out");
            assets.StructuredCopyTo(outputDirectory, intermediateDirectory);

            var copiedFile = Directory.EnumerateFiles(outputDirectory, Path.GetFileName(libraryAsset.RelativePath)).First();
            IsFileReadonly(copiedFile).Should().BeFalse();
        }

        private void MakeFileReadonly(string file)
        {
            File.SetAttributes(file, File.GetAttributes(file) | FileAttributes.ReadOnly);
        }

        private bool IsFileReadonly(string file)
        {
            return (File.GetAttributes(file) & FileAttributes.ReadOnly) == FileAttributes.ReadOnly;
        }

        private LibraryAsset GetMockLibraryAsset(string mockedLibraryAssetName)
        {
            var mockedLibraryAssetFileName = $"{mockedLibraryAssetName}.dll";

            var fakeFile = Path.Combine(AppContext.BaseDirectory, mockedLibraryAssetFileName);

            if (File.Exists(fakeFile))
            {
                File.SetAttributes(fakeFile, FileAttributes.Normal);
                File.Delete(fakeFile);
            }

            File.WriteAllText(fakeFile, mockedLibraryAssetName);

            return new LibraryAsset(mockedLibraryAssetName, mockedLibraryAssetFileName, fakeFile);
        }
    }
}
