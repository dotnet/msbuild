// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using Microsoft.Build.Framework;
using Microsoft.DotNet.PlatformAbstractions;
using Microsoft.Extensions.DependencyModel.Resolution;

namespace Microsoft.NET.Build.Tasks
{
    internal static class FrameworkReferenceResolver
    {
        public static string GetDefaultReferenceAssembliesPath()
        {
            // Allow setting the reference assemblies path via an environment variable
            var referenceAssembliesPath = DotNetReferenceAssembliesPathResolver.Resolve();

            if (!string.IsNullOrEmpty(referenceAssembliesPath))
            {
                return referenceAssembliesPath;
            }

            if (RuntimeEnvironment.OperatingSystemPlatform != Platform.Windows)
            {
                // There is no reference assemblies path outside of windows
                // The environment variable can be used to specify one
                return null;
            }

            // References assemblies are in %ProgramFiles(x86)% on
            // 64 bit machines
            var programFiles = Environment.GetEnvironmentVariable("ProgramFiles(x86)");

            if (string.IsNullOrEmpty(programFiles))
            {
                // On 32 bit machines they are in %ProgramFiles%
                programFiles = Environment.GetEnvironmentVariable("ProgramFiles");
            }

            if (string.IsNullOrEmpty(programFiles))
            {
                // Reference assemblies aren't installed
                return null;
            }

            return Path.Combine(
                    programFiles,
                    "Reference Assemblies", "Microsoft", "Framework");
        }
    }
}
