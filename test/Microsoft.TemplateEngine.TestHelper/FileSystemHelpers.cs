using System;
using System.IO;
using Microsoft.TemplateEngine.Abstractions;

namespace Microsoft.TemplateEngine.TestHelper
{
    public static class FileSystemHelpers
    {
        public static string GetNewVirtualizedPath(IEngineEnvironmentSettings environment)
        {
            string basePath = Path.Combine(Directory.GetCurrentDirectory(), "sandbox", Guid.NewGuid().ToString()) + Path.DirectorySeparatorChar;
            environment.Host.VirtualizeDirectory(basePath);

            return basePath;
        }
    }
}
