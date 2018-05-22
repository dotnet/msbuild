// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.IO;

namespace Microsoft.DotNet.Tools.Test.Utilities
{
    public static class NuGetConfig
    {
        public static void Write(string directory)
        {
            var contents = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
<packageSources>
<!--To inherit the global NuGet package sources remove the <clear/> line below -->
<clear />
<add key=""dotnet-core"" value=""https://dotnet.myget.org/F/dotnet-core/api/v3/index.json"" />
<add key=""api.nuget.org"" value=""https://api.nuget.org/v3/index.json"" />
</packageSources>
</configuration>";

            var path = Path.Combine(directory, "NuGet.config");

            File.WriteAllText(path, contents);
        }
        
        public static void Write(string directory, string configname, string localFeedPath)
        {
            const string template = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
<packageSources>
<!--To inherit the global NuGet package sources remove the <clear/> line below -->
<clear />
<add key=""Test Source"" value=""{0}"" />
<add key=""api.nuget.org"" value=""https://api.nuget.org/v3/index.json"" />
<add key=""dotnet-core"" value=""https://dotnet.myget.org/F/dotnet-core/api/v3/index.json"" />
</packageSources>
</configuration>";

            var path = Path.Combine(directory, configname);

            File.WriteAllText(path, string.Format(template, localFeedPath));
        }
    }
}
