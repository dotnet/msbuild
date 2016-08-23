// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.NETCore.TestFramework
{
    public class RepoInfo
    {
        private static string s_repoRoot;

        private static string s_configuration;

        public static string RepoRoot
        {
            get
            {
                if (!string.IsNullOrEmpty(s_repoRoot))
                {
                    return s_repoRoot;
                }

                string directory = GetBaseDirectory();

                while (!Directory.Exists(Path.Combine(directory, ".git")) && directory != null)
                {
                    directory = Directory.GetParent(directory).FullName;
                }

                if (directory == null)
                {
                    throw new Exception("Cannot find the git repository root");
                }

                s_repoRoot = directory;
                return s_repoRoot;
            }
        }

        public static string Configuration
        {
            get
            {
                if (string.IsNullOrEmpty(s_configuration))
                {
                    s_configuration = FindConfigurationInBasePath();
                }

                return s_configuration;
            }
        }

        public static string Bin
        {
            get
            {
                return Path.Combine(RepoRoot, "bin");
            }
        }

        public static string DotNetHostPath
        {
            get
            {
                return Path.Combine(RepoRoot, ".dotnet_cli", $"dotnet{Constants.ExeSuffix}");
            }
        }

        private static string FindConfigurationInBasePath()
        {
            return new DirectoryInfo(GetBaseDirectory()).Parent.Name;
        }

        private static string GetBaseDirectory()
        {
#if NET451
            string directory = AppDomain.CurrentDomain.BaseDirectory;
#else
            string directory = AppContext.BaseDirectory;
#endif

            return directory;
        }
    }
}