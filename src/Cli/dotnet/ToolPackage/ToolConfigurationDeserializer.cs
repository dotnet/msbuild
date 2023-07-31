// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Xml;
using System.Xml.Serialization;
using Microsoft.DotNet.ToolPackage.ToolConfigurationDeserialization;
using Microsoft.DotNet.Tools;

namespace Microsoft.DotNet.ToolPackage
{
    internal static class ToolConfigurationDeserializer
    {
        // The supported tool configuration schema version.
        // This should match the schema version in the GenerateToolsSettingsFile task from the SDK.
        private const int SupportedVersion = 1;

        public static ToolConfiguration Deserialize(string pathToXml)
        {
            var serializer = new XmlSerializer(typeof(DotNetCliTool));

            DotNetCliTool dotNetCliTool;

            try
            {
                using (var fs = File.OpenRead(pathToXml))
                {
                    var reader = XmlReader.Create(fs);
                    dotNetCliTool = (DotNetCliTool)serializer.Deserialize(reader);
                }
            }
            catch (InvalidOperationException ex) when (ex.InnerException is XmlException)
            {
                throw new ToolConfigurationException(
                    string.Format(
                        CommonLocalizableStrings.ToolSettingsInvalidXml,
                        ex.InnerException.Message),
                    ex.InnerException);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                throw new ToolConfigurationException(
                    string.Format(
                        CommonLocalizableStrings.FailedToRetrieveToolConfiguration,
                        ex.Message),
                    ex);
            }

            List<string> warnings = GenerateWarningAccordingToVersionAttribute(dotNetCliTool);

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
                dotNetCliTool.Commands[0].EntryPoint,
                warnings);
        }

        private static List<string> GenerateWarningAccordingToVersionAttribute(DotNetCliTool dotNetCliTool)
        {
            List<string> warnings = new List<string>();
            if (string.IsNullOrWhiteSpace(dotNetCliTool.Version))
            {
                warnings.Add(CommonLocalizableStrings.FormatVersionIsMissing);
            }
            else
            {
                if (!int.TryParse(dotNetCliTool.Version, out int version))
                {
                    warnings.Add(CommonLocalizableStrings.FormatVersionIsMalformed);
                }
                else
                {
                    if (version > SupportedVersion)
                    {
                        warnings.Add(CommonLocalizableStrings.FormatVersionIsHigher);
                    }
                }
            }

            return warnings;
        }
    }
}
