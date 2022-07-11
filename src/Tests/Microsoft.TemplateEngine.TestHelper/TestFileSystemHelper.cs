// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.Mount;
using Microsoft.TemplateEngine.Core.Contracts;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects;
using Xunit;

namespace Microsoft.TemplateEngine.TestHelper
{
    public static class TestFileSystemHelper
    {
        public static readonly Guid FileSystemMountPointFactoryId = new Guid("8C19221B-DEA3-4250-86FE-2D4E189A11D2");
        public static readonly string DefaultConfigRelativePath = ".template.config/template.json";

        public static void WriteTemplateSource(
            IEngineEnvironmentSettings environment,
            string sourceBasePath,
            IDictionary<string, string?> templateSourceFileNamesWithContent)
        {
            foreach (KeyValuePair<string, string?> fileInfo in templateSourceFileNamesWithContent)
            {
                string filePath = Path.Combine(sourceBasePath, fileInfo.Key);
                string fullPathDir = Path.GetDirectoryName(filePath)!;
                environment.Host.FileSystem.CreateDirectory(fullPathDir);
                environment.Host.FileSystem.WriteAllText(filePath, fileInfo.Value ?? string.Empty);
            }
        }

        public static IMountPoint CreateMountPoint(IEngineEnvironmentSettings environment, string sourceBasePath)
        {
            foreach (var factory in environment.Components.OfType<IMountPointFactory>())
            {
                if (factory.TryMount(environment, null, sourceBasePath, out IMountPoint? sourceMountPoint))
                {
                    if (sourceMountPoint is null)
                    {
                        throw new InvalidOperationException($"{nameof(sourceMountPoint)} cannot be null when {nameof(factory.TryMount)} is 'true'");
                    }
                    return sourceMountPoint;
                }
            }
            Assert.True(false, "couldn't create source mount point");
            throw new Exception("couldn't create source mount point");
        }
    }
}
