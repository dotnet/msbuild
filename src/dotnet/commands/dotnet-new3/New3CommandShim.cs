// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Cli;
using Microsoft.TemplateEngine.Edge;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Config;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Macros;
using Microsoft.TemplateEngine.Utils;

namespace Microsoft.DotNet.Tools.New3
{
    internal class New3CommandShim
    {
        private const string HostIdentifier = "dotnetcli";
        private const string HostVersion = "1.0.0";
        private const string CommandName = "new3";

        public static int Run(string[] args)
        {
            return New3Command.Run(CommandName, CreateHost(), FirstRun, args);
        }

        private static ITemplateEngineHost CreateHost()
        {
            var builtIns = new Dictionary<Guid, Func<Type>>
            {
                { new Guid("0C434DF7-E2CB-4DEE-B216-D7C58C8EB4B3"), () => typeof(RunnableProjectGenerator) },
                { new Guid("3147965A-08E5-4523-B869-02C8E9A8AAA1"), () => typeof(BalancedNestingConfig) },
                { new Guid("3E8BCBF0-D631-45BA-A12D-FBF1DE03AA38"), () => typeof(ConditionalConfig) },
                { new Guid("A1E27A4B-9608-47F1-B3B8-F70DF62DC521"), () => typeof(FlagsConfig) },
                { new Guid("3FAE1942-7257-4247-B44D-2DDE07CB4A4A"), () => typeof(IncludeConfig) },
                { new Guid("3D33B3BF-F40E-43EB-A14D-F40516F880CD"), () => typeof(RegionConfig) },
                { new Guid("62DB7F1F-A10E-46F0-953F-A28A03A81CD1"), () => typeof(ReplacementConfig) },
                { new Guid("370996FE-2943-4AED-B2F6-EC03F0B75B4A"), () => typeof(ConstantMacro) },
                { new Guid("BB625F71-6404-4550-98AF-B2E546F46C5F"), () => typeof(EvaluateMacro) },
                { new Guid("10919008-4E13-4FA8-825C-3B4DA855578E"), () => typeof(GuidMacro) },
                { new Guid("F2B423D7-3C23-4489-816A-41D8D2A98596"), () => typeof(NowMacro) },
                { new Guid("011E8DC1-8544-4360-9B40-65FD916049B7"), () => typeof(RandomMacro) },
                { new Guid("8A4D4937-E23F-426D-8398-3BDBD1873ADB"), () => typeof(RegexMacro) },
                { new Guid("B57D64E0-9B4F-4ABE-9366-711170FD5294"), () => typeof(SwitchMacro) },
                { new Guid("10919118-4E13-4FA9-825C-3B4DA855578E"), () => typeof(CaseChangeMacro) }
            }.ToList();

            var preferences = new Dictionary<string, string>
            {
                { "prefs:language", "C#" }
            };

            return new DefaultTemplateEngineHost(HostIdentifier, HostVersion, CultureInfo.CurrentCulture.Name, preferences, builtIns);
        }

        private static void FirstRun(ITemplateEngineHost host, IInstaller installer)
        { 
            var templatesDir = Path.Combine(Paths.Global.BaseDir, "Templates");

            if (templatesDir.Exists())
            {
                var layoutIncludedPackages = host.FileSystem.EnumerateFiles(templatesDir, "*.nupkg", SearchOption.TopDirectoryOnly);
                installer.InstallPackages(layoutIncludedPackages);
            }
        }
    }
}
