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
                        string.Format(
                            CommonLocalizableStrings.ToolSettingsInvalidXml,
                            e.InnerException.Message));
                }
            }

            if (dotNetCliTool.Commands.Length != 1)
            {
                throw new ToolConfigurationException(CommonLocalizableStrings.ToolSettingsMoreThanOneCommand);
            }

            if (dotNetCliTool.Commands[0].Runner != "dotnet")
            {
                throw new ToolConfigurationException(
                    string.Format(
                        CommonLocalizableStrings.ToolSettingsUnsupportedRunner,
                        dotNetCliTool.Commands[0].Name,
                        dotNetCliTool.Commands[0].Runner));
            }

            return new ToolConfiguration(
                dotNetCliTool.Commands[0].Name,
                dotNetCliTool.Commands[0].EntryPoint);
        }
    }
}
