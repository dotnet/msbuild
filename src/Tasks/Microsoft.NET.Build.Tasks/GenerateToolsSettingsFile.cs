// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Build.Framework;
using System.Xml.Linq;
using System.IO;

namespace Microsoft.NET.Build.Tasks
{
    public class GenerateToolsSettingsFile : TaskBase
    {
        [Required]
        public string EntryPointRelativePath { get; set; }

        [Required]
        public string CommandName { get; set; }

        [Required]
        public string ToolsSettingsFilePath { get; set; }

        protected override void ExecuteCore()
        {
            using (StringWriter writer = new StringWriter())
            {
                GenerateDocument(EntryPointRelativePath, CommandName).Save(ToolsSettingsFilePath);
            }
        }

        internal static XDocument GenerateDocument(string entryPointRelativePath, string commandName)
        {
            return new XDocument(
                new XDeclaration(version: null, encoding: null, standalone: null),
                new XElement("DotNetCliTool",
                      new XElement("Commands",
                          new XElement("Command",
                          new XAttribute("Name", commandName),
                          new XAttribute("EntryPoint", entryPointRelativePath),
                          new XAttribute("Runner", "dotnet")))));
        }
    }
}
