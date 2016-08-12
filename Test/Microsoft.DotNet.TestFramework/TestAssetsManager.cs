// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace Microsoft.DotNet.TestFramework
{
    public class TestAssetsManager
    {
        public string AssetsRoot
        {
            get; private set;
        }

        public TestAssetsManager(string assetsRoot)
        {
            if (!Directory.Exists(assetsRoot))
            {
                throw new DirectoryNotFoundException($"Directory not found: '{assetsRoot}'");
            }

            AssetsRoot = assetsRoot;
        }

        public TestInstance CopyTestAsset(
            string testProjectName,
            [CallerMemberName] string callingMethod = "",
            string identifier = "")
        {
            string testProjectDir = Path.Combine(AssetsRoot, testProjectName);

            if (!Directory.Exists(testProjectDir))
            {
                throw new Exception($"Cannot find '{testProjectName}' at '{AssetsRoot}'");
            }

            var testDestination = GetTestDestinationDirectoryPath(testProjectName, callingMethod, identifier);
            var testInstance = new TestInstance(testProjectDir, testDestination);
            return testInstance;
        }

        private string GetTestDestinationDirectoryPath(string testProjectName, string callingMethod, string identifier)
        {
#if NET451
            string baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
#else
            string baseDirectory = AppContext.BaseDirectory;
#endif
            return Path.Combine(baseDirectory, callingMethod + identifier, testProjectName);
        }
    }
}
