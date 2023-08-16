// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.NET.TestFramework
{
    public static class NuGetConfigWriter
    {
        public static readonly string DotnetCoreBlobFeed = "https://dotnetfeed.blob.core.windows.net/dotnet-core/index.json";
        public static readonly string AspNetCoreDevFeed = "https://dotnet.myget.org/F/aspnetcore-dev/api/v3/index.json";

        public static void Write(string folder, params string[] nugetSources)
        {
            Write(folder, nugetSources.ToList());
        }
        public static void Write(string folder, List<string> nugetSources)
        {
            string configFilePath = Path.Combine(folder, "NuGet.Config");
            var root = new XElement("configuration");

            var packageSources = new XElement("packageSources");
            root.Add(packageSources);

            for (int i = 0; i < nugetSources.Count; i++)
            {
                packageSources.Add(new XElement("add",
                    new XAttribute("key", Guid.NewGuid().ToString()),
                    new XAttribute("value", nugetSources[i])
                    ));
            }

            File.WriteAllText(configFilePath, root.ToString());
        }
    }
}
