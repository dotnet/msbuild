// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.IO;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using SdkResolverBase = Microsoft.Build.Framework.SdkResolver;
using SdkResolverContextBase = Microsoft.Build.Framework.SdkResolverContext;
using SdkResultBase = Microsoft.Build.Framework.SdkResult;
using SdkResultFactoryBase = Microsoft.Build.Framework.SdkResultFactory;

#nullable disable

namespace Microsoft.Build.BackEnd.SdkResolution
{
    /// <summary>
    ///     Resolver for repo-local SDKs located in .msbuild/Sdk/ directory.
    /// <remarks>
    ///     Searches for SDKs in:
    ///         {repo-root}/.msbuild/Sdk/{SdkName}/
    ///     
    ///     Where {repo-root} is determined by walking up the directory tree from the project
    ///     directory and looking for:
    ///         - .git/
    ///         - .hg/
    ///         - .svn/
    ///         - or the filesystem root
    /// </remarks>
    /// </summary>
    internal class RepoLocalSdkResolver : SdkResolverBase
    {
        public override string Name => "RepoLocalSdkResolver";

        // Higher priority than DefaultSdkResolver (10000) to allow repo-local SDKs to override global ones
        public override int Priority => 5000;

        public override SdkResultBase Resolve(SdkReference sdk, SdkResolverContextBase context, SdkResultFactoryBase factory)
        {
            // Start from the project directory or solution directory
            string startingDirectory = context.ProjectFilePath;
            if (string.IsNullOrEmpty(startingDirectory))
            {
                startingDirectory = context.SolutionFilePath;
            }

            if (string.IsNullOrEmpty(startingDirectory))
            {
                return factory.IndicateFailure(null);
            }

            // Get the directory containing the project/solution file
            string searchDirectory = Path.GetDirectoryName(startingDirectory);
            if (string.IsNullOrEmpty(searchDirectory))
            {
                return factory.IndicateFailure(null);
            }

            // Find the repository root
            string repoRoot = FindRepositoryRoot(searchDirectory);
            if (string.IsNullOrEmpty(repoRoot))
            {
                return factory.IndicateFailure(null);
            }

            // Check for .msbuild/Sdk/{SdkName}/Sdk directory
            string sdkPath = Path.Combine(repoRoot, ".msbuild", "Sdk", sdk.Name, "Sdk");

            if (!FileUtilities.DirectoryExistsNoThrow(sdkPath))
            {
                return factory.IndicateFailure(null);
            }

            // Verify that the SDK directory contains the required files (Sdk.props and/or Sdk.targets)
            string sdkPropsPath = Path.Combine(sdkPath, "Sdk.props");
            string sdkTargetsPath = Path.Combine(sdkPath, "Sdk.targets");

            bool hasSdkProps = FileUtilities.FileExistsNoThrow(sdkPropsPath);
            bool hasSdkTargets = FileUtilities.FileExistsNoThrow(sdkTargetsPath);

            if (!hasSdkProps && !hasSdkTargets)
            {
                return factory.IndicateFailure([$"Repo-local SDK directory '{sdkPath}' does not contain Sdk.props or Sdk.targets."], null);
            }

            return factory.IndicateSuccess(sdkPath, string.Empty);
        }

        /// <summary>
        /// Finds the repository root by walking up the directory tree looking for
        /// repository markers (.git, .hg, .svn directories).
        /// </summary>
        /// <param name="startingDirectory">The directory to start searching from.</param>
        /// <returns>The repository root directory, or null if not found.</returns>
        private static string FindRepositoryRoot(string startingDirectory)
        {
            string currentDirectory = FileUtilities.NormalizePath(startingDirectory);
            
            while (!string.IsNullOrEmpty(currentDirectory))
            {
                // Check for repository markers
                if (FileUtilities.DirectoryExistsNoThrow(Path.Combine(currentDirectory, ".git")) ||
                    FileUtilities.DirectoryExistsNoThrow(Path.Combine(currentDirectory, ".hg")) ||
                    FileUtilities.DirectoryExistsNoThrow(Path.Combine(currentDirectory, ".svn")))
                {
                    return currentDirectory;
                }

                // Move to parent directory
                DirectoryInfo parent = Directory.GetParent(currentDirectory);
                if (parent == null)
                {
                    // Reached the root of the filesystem without finding a repository marker
                    break;
                }

                currentDirectory = parent.FullName;
            }

            return null;
        }
    }
}
