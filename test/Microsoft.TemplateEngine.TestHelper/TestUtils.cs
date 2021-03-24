// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;

namespace Microsoft.TemplateEngine.TestHelper
{
    public class TestUtils
    {
        public static string HomeEnvironmentVariableName { get; } = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "USERPROFILE" : "HOME";

        public static string CreateTemporaryFolder(string name = "")
        {
            string workingDir = Path.Combine(Path.GetTempPath(), "TemplateEngine.Tests", Guid.NewGuid().ToString(), name);
            Directory.CreateDirectory(workingDir);
            return workingDir;
        }

        public static string GetTestTemplateLocation(string templateName)
        {
            string codebase = typeof(TestUtils).GetTypeInfo().Assembly.Location;
            string dir = Path.GetDirectoryName(codebase);
            string templateLocation = Path.Combine(dir, "..", "..", "..", "..", "..", "test", "Microsoft.TemplateEngine.TestTemplates", "test_templates", templateName);

            if (!Directory.Exists(templateLocation))
            {
                throw new Exception($"{templateLocation} does not exist");
            }
            return Path.GetFullPath(templateLocation);
        }
    }
}
