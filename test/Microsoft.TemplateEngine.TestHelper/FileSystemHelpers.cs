// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.Mount;

namespace Microsoft.TemplateEngine.TestHelper
{
    public static class FileSystemHelpers
    {
        public static string GetNewVirtualizedPath(this IEngineEnvironmentSettings environmentSettings)
        {
            string basePath = Path.Combine(Directory.GetCurrentDirectory(), "sandbox", Guid.NewGuid().ToString()) + Path.DirectorySeparatorChar;
            environmentSettings.Host.VirtualizeDirectory(basePath);

            return basePath;
        }

        public static IMountPoint MountPath (this IEngineEnvironmentSettings environmentSettings, string path)
        {
            foreach (var factory in environmentSettings.Components.OfType<IMountPointFactory>())
            {
                if (factory.TryMount(environmentSettings, null, path, out IMountPoint? mountPoint))
                {
                    return mountPoint;
                }
            }
            throw new Exception($"Failed to mount path {path}.");
        }
    }
}
