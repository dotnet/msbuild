// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Xml;
using System.Xml.Serialization;
using Microsoft.DotNet.ToolPackage.ToolConfigurationDeserialization;
using Microsoft.DotNet.Tools;

namespace Microsoft.DotNet.ToolPackage
{
    internal static class ToolConfigurationDeserializer
    {
        public static ToolConfiguration Deserialize(string pathToXml)
        {
            var serializer = new XmlSerializer(typeof(DotNetCliTool));

            DotNetCliTool dotNetCliTool;

            using (var fs = new FileStream(pathToXml, FileMode.Open))
            {
                var reader = XmlReader.Create(fs);

                try
                {
                    dotNetCliTool = (DotNetCliTool)serializer.Deserialize(reader);
                }
                catch (InvalidOperationException e) when (e.InnerException is XmlException)
                {
                    throw new ToolConfigurationException(
                        string.Format(CommonLocalizableStrings.ToolSettingsInvalidXml, e.InnerException.Message));
                }
            }

            if (dotNetCliTool.Commands.Length != 1)
            {
                throw new ToolConfigurationException(
                    CommonLocalizableStrings.ToolSettingMoreThanOneCommand);
            }

            if (dotNetCliTool.Commands[0].Runner != "dotnet")
            {
                throw new ToolConfigurationException(
                    CommonLocalizableStrings.ToolSettingInvalidRunner);
            }

            var commandName = dotNetCliTool.Commands[0].Name;
            var toolAssemblyEntryPoint = dotNetCliTool.Commands[0].EntryPoint;

            try
            {
                return new ToolConfiguration(commandName, toolAssemblyEntryPoint);
            }
            catch (ArgumentException e)
            {
                throw new ToolConfigurationException(
                    string.Format(CommonLocalizableStrings.ToolSettingsContainError,
                    e.Message));
            }
        }
    }
}
