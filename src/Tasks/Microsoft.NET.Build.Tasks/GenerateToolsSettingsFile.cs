// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Framework;

namespace Microsoft.NET.Build.Tasks
{
    public class GenerateToolsSettingsFile : TaskBase
    {
        // bump whenever the format changes such that it will break old consumers
        private static readonly int _formatVersion = 1;

        [Required]
        public string EntryPointRelativePath { get; set; }

        [Required]
        public string CommandName { get; set; }

        [Required]
        public string ToolsSettingsFilePath { get; set; }

        protected override void ExecuteCore()
        {
            using (StringWriter writer = new())
            {
                GenerateDocument(EntryPointRelativePath, CommandName).Save(ToolsSettingsFilePath);
            }
        }

        internal static XDocument GenerateDocument(string entryPointRelativePath, string commandName)
        {
            return new XDocument(
                new XDeclaration(version: null, encoding: null, standalone: null),
                new XElement("DotNetCliTool",
                      new XAttribute("Version", _formatVersion),
                      new XElement("Commands",
                          new XElement("Command",
                          new XAttribute("Name", commandName),
                          new XAttribute("EntryPoint", entryPointRelativePath),
                          new XAttribute("Runner", "dotnet")))));
        }
    }
}
