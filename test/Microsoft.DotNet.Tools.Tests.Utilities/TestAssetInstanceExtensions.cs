// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.TestFramework;
using Microsoft.DotNet.Tools.Common;

namespace Microsoft.DotNet.Tools.Test.Utilities
{
    public static class TestAssetInstanceExtensions
    {
        public static TestAssetInstance WithNuGetConfigAndExternalRestoreSources(
            this TestAssetInstance testAssetInstance, string nugetCache)
        {
            var externalRestoreSourcesForTests = Path.Combine(
                new RepoDirectoriesProvider().TestArtifactsFolder, "ExternalRestoreSourcesForTestsContainer.txt");
            var externalRestoreSources = File.Exists(externalRestoreSourcesForTests) ?
                File.ReadAllText(externalRestoreSourcesForTests) :
                string.Empty;

            return testAssetInstance.WithNuGetConfig(nugetCache, externalRestoreSources);
        }
    }
}