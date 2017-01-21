using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace Microsoft.NET.Sdk.Publish.Tasks
{
    public static class WebConfigTransform
    {
        public static XDocument Transform(XDocument webConfig, string appName, bool configureForAzure, bool isPortable)
        {
            const string HandlersElementName = "handlers";
            const string aspNetCoreElementName = "aspNetCore";

            webConfig = webConfig == null || webConfig.Root.Name.LocalName != "configuration"
                ? XDocument.Parse("<configuration />")
                : webConfig;

            var webServerSection = GetOrCreateChild(webConfig.Root, "system.webServer");

            TransformHandlers(GetOrCreateChild(webServerSection, HandlersElementName));
            TransformAspNetCore(GetOrCreateChild(webServerSection, aspNetCoreElementName), appName, configureForAzure, isPortable);

            // make sure that the aspNetCore element is after handlers element
            var aspNetCoreElement = webServerSection.Element(HandlersElementName)
                .ElementsBeforeSelf(aspNetCoreElementName).SingleOrDefault();
            if (aspNetCoreElement != null)
            {
                aspNetCoreElement.Remove();
                webServerSection.Element(HandlersElementName).AddAfterSelf(aspNetCoreElement);
            }

            return webConfig;
        }

        private static void TransformHandlers(XElement handlersElement)
        {
            var aspNetCoreElement =
                handlersElement.Elements("add")
                    .FirstOrDefault(e => string.Equals((string)e.Attribute("name"), "aspnetcore", StringComparison.OrdinalIgnoreCase));

            if (aspNetCoreElement == null)
            {
                aspNetCoreElement = new XElement("add");
                handlersElement.Add(aspNetCoreElement);
            }

            aspNetCoreElement.SetAttributeValue("name", "aspNetCore");
            SetAttributeValueIfEmpty(aspNetCoreElement, "path", "*");
            SetAttributeValueIfEmpty(aspNetCoreElement, "verb", "*");
            SetAttributeValueIfEmpty(aspNetCoreElement, "modules", "AspNetCoreModule");
            SetAttributeValueIfEmpty(aspNetCoreElement, "resourceType", "Unspecified");
        }

        private static void TransformAspNetCore(XElement aspNetCoreElement, string appName, bool configureForAzure, bool isPortable)
        {
            // Forward slashes currently work neither in AspNetCoreModule nor in dotnet so they need to be
            // replaced with backwards slashes when the application is published on a non-Windows machine
            var appPath = Path.Combine(".", appName).Replace("/", "\\");
            RemoveLauncherArgs(aspNetCoreElement);

            if (!isPortable)
            {
                appPath = Path.ChangeExtension(appPath, ".exe");
            }

            if (!isPortable)
            {
                aspNetCoreElement.SetAttributeValue("processPath", appPath);
            }
            else
            {
                aspNetCoreElement.SetAttributeValue("processPath", "dotnet");

                // In Xml the order of attributes does not matter but it is nice to have
                // the `arguments` attribute next to the `processPath` attribute
                var argumentsAttribute = aspNetCoreElement.Attribute("arguments");
                argumentsAttribute?.Remove();
                var attributes = aspNetCoreElement.Attributes().ToList();
                var processPathIndex = attributes.FindIndex(a => a.Name.LocalName == "processPath");
                // if the app path is already there in the web.config, don't do anything.
                if (string.Equals(appPath, (string)argumentsAttribute, StringComparison.OrdinalIgnoreCase))
                {
                    appPath = String.Empty;
                }
                attributes.Insert(processPathIndex + 1,
                    new XAttribute("arguments", (appPath + " " + (string)argumentsAttribute).Trim()));

                aspNetCoreElement.Attributes().Remove();
                aspNetCoreElement.Add(attributes);
            }

            SetAttributeValueIfEmpty(aspNetCoreElement, "stdoutLogEnabled", "false");

            var logPath = Path.Combine(configureForAzure ? @"\\?\%home%\LogFiles" : @".\logs", "stdout").Replace("/", "\\");
            if (configureForAzure)
            {
                // When publishing for Azure we want to always overwrite path - the folder we set the path to
                // will exist, the path is not easy to customize and stdoutLogPath should be only used for
                // diagnostic purposes
                aspNetCoreElement.SetAttributeValue("stdoutLogFile", logPath);
            }
            else
            {
                SetAttributeValueIfEmpty(aspNetCoreElement, "stdoutLogFile", logPath);
            }
        }

        private static XElement GetOrCreateChild(XElement parent, string childName)
        {
            var childElement = parent.Element(childName);
            if (childElement == null)
            {
                childElement = new XElement(childName);
                parent.Add(childElement);
            }
            return childElement;
        }

        private static void SetAttributeValueIfEmpty(XElement element, string attributeName, string value)
        {
            element.SetAttributeValue(attributeName, (string)element.Attribute(attributeName) ?? value);
        }

        private static void RemoveLauncherArgs(XElement aspNetCoreElement)
        {
            var arguments = (string)aspNetCoreElement.Attribute("arguments");

            if (arguments != null)
            {
                const string launcherArgs = "%LAUNCHER_ARGS%";
                var position = 0;
                while ((position = arguments.IndexOf(launcherArgs, position, StringComparison.OrdinalIgnoreCase)) >= 0)
                {
                    arguments = arguments.Remove(position, launcherArgs.Length);
                }

                aspNetCoreElement.SetAttributeValue("arguments", arguments.Trim());
            }
        }

        public static XDocument AddProjectGuidToWebConfig(XDocument document, string projectGuid, bool ignoreProjectGuid)
        {
            try
            {
                if (document != null && !string.IsNullOrEmpty(projectGuid))
                {
                    IEnumerable<XComment> comments = document.DescendantNodes().OfType<XComment>();
                    projectGuid =  projectGuid.Trim('{', '}', '(', ')').Trim();
                    string projectGuidValue = string.Format("ProjectGuid: {0}", projectGuid);
                    XComment projectGuidComment = comments.FirstOrDefault(comment => string.Equals(comment.Value, projectGuidValue, StringComparison.OrdinalIgnoreCase));
                    if (projectGuidComment != null)
                    {
                        if (ignoreProjectGuid)
                        {
                            projectGuidComment.Remove();
                        }

                        return document;
                    }

                    if (!ignoreProjectGuid)
                    {
                        document.LastNode.AddAfterSelf(new XComment(projectGuidValue));
                        return document;
                    }
                }
            }
            catch
            {
                // This code path is only used for telemetry.
            }

            return document;
        }

        public static string GetProjectGuidFromSolutionFile(string solutionFileFullPath, string projectFileFullPath)
        {
            try
            {
                if (!string.IsNullOrEmpty(solutionFileFullPath) && File.Exists(solutionFileFullPath))
                {
                    return GetProjectGuid(solutionFileFullPath, projectFileFullPath);
                }

                int parentLevelsToSearch = 5;
                string solutionDirectory = Path.GetDirectoryName(projectFileFullPath);

                while (parentLevelsToSearch-- > 0)
                {
                    if (string.IsNullOrEmpty(solutionDirectory) || !Directory.Exists(solutionDirectory))
                    {
                        return null;
                    }

                    IEnumerable<string> solutionFiles = Directory.EnumerateFiles(solutionDirectory, "*.sln", SearchOption.TopDirectoryOnly);
                    foreach (string solutionFile in solutionFiles)
                    {
                        string projectGuid = GetProjectGuid(solutionFile, projectFileFullPath);
                        if (!string.IsNullOrEmpty(projectGuid))
                        {
                            return projectGuid;
                        }
                    }

                    solutionDirectory = Directory.GetParent(solutionDirectory)?.FullName;
                }
            }
            catch
            {
                // This code path is only used for telemetry.
            }

            return null;
        }

        // An example of a project line looks like this:
        //  Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "ClassLibrary1", "ClassLibrary1\ClassLibrary1.csproj", "{05A5AD00-71B5-4612-AF2F-9EA9121C4111}"
        private static readonly Lazy<Regex> s_crackProjectLine = new Lazy<Regex>(
            () => new Regex
                (
                @"^" // Beginning of line
                + @"Project\s*\(\""[^""]*?\""\)"
                + @"\s*=\s*" // Any amount of whitespace plus "=" plus any amount of whitespace
                + @"\""[^\""]*\"""
                + @"\s*,\s*" // Any amount of whitespace plus "," plus any amount of whitespace
                + @"\""(?<RELATIVEPATH>[^""]*?)\"""
                + @"\s*,\s*" // Any amount of whitespace plus "," plus any amount of whitespace
                + @"\""(?<PROJECTGUID>[^""]*?)\"""
                + @"\s*$", // End-of-line
                RegexOptions.Compiled
                )
            );
        private static string GetProjectGuid(string solutionFileFullPath, string projectFileFullPath)
        {
            if (!string.IsNullOrEmpty(solutionFileFullPath) && File.Exists(solutionFileFullPath))
            {
                string[] solutionFileLines = File.ReadAllLines(solutionFileFullPath);
                foreach (string solutionFileLine in solutionFileLines)
                {
                    Match match = s_crackProjectLine.Value.Match(solutionFileLine);
                    if (match.Success)
                    {
                        string projectRelativePath = match.Groups["RELATIVEPATH"].Value.Trim();
                        string projectFullPathConstructed = Path.Combine(Path.GetDirectoryName(solutionFileFullPath), projectRelativePath);
                        projectFullPathConstructed = Path.GetFullPath((new Uri(projectFullPathConstructed)).LocalPath);
                        if (string.Equals(projectFileFullPath, projectFullPathConstructed, StringComparison.OrdinalIgnoreCase))
                        {
                            string projectGuid = match.Groups["PROJECTGUID"].Value.Trim();
                            return projectGuid;
                        }
                    }
                }
            }

            return null;
        }
    }
}