using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.NET.Sdk.Publish.Tasks.Properties;

namespace Microsoft.NET.Sdk.Publish.Tasks
{
    public static class WebConfigTransform
    {
        public static XDocument Transform(XDocument webConfig, string appName, bool configureForAzure, bool useAppHost, string extension, string aspNetCoreHostingModel, string environmentName)
        {
            const string HandlersElementName = "handlers";
            const string aspNetCoreElementName = "aspNetCore";
            const string envVariablesElementName = "environmentVariables";

            const string WebConfigTemplate = @"<configuration><location path=""."" inheritInChildApplications=""false"" /></configuration>";

            if (webConfig == null || webConfig.Root.Name.LocalName != "configuration")
            {
                webConfig = XDocument.Parse(WebConfigTemplate);
            }

            var rootElement = webConfig.Root.Element("location") == null ? webConfig.Root : webConfig.Root.Element("location");
            var webServerSection = GetOrCreateChild(rootElement, "system.webServer");

            TransformHandlers(GetOrCreateChild(webServerSection, HandlersElementName));
            var aspNetCoreSection = GetOrCreateChild(webServerSection, aspNetCoreElementName);
            TransformAspNetCore(aspNetCoreSection, appName, configureForAzure, useAppHost, extension, aspNetCoreHostingModel);
            if (!string.IsNullOrEmpty(environmentName))
            {
                TransformEnvironmentVariables(GetOrCreateChild(aspNetCoreSection, envVariablesElementName), environmentName);
            }

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

        private static void TransformAspNetCore(XElement aspNetCoreElement, string appName, bool configureForAzure, bool useAppHost, string extension, string aspNetCoreHostingModel)
        {
            // Forward slashes currently work neither in AspNetCoreModule nor in dotnet so they need to be
            // replaced with backwards slashes when the application is published on a non-Windows machine
            var appPath = Path.Combine(".", appName).Replace("/", "\\");
            RemoveLauncherArgs(aspNetCoreElement);

            if (useAppHost || string.Equals(Path.GetExtension(appPath), ".exe", StringComparison.OrdinalIgnoreCase))
            {
                appPath = Path.ChangeExtension(appPath, !string.IsNullOrWhiteSpace(extension) ? extension : null);
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

            var hostingModelAttributeValue = aspNetCoreElement.Attribute("hostingModel");
            // Set the hostingmodel attribute only if it is not already set in the web.config and AspNetCoreHostingModel property is set.
            if (hostingModelAttributeValue == null && !string.IsNullOrEmpty(aspNetCoreHostingModel))
            {
                if (string.Equals(aspNetCoreHostingModel, "inprocess", StringComparison.OrdinalIgnoreCase) || string.Equals(aspNetCoreHostingModel, "outofprocess", StringComparison.OrdinalIgnoreCase))
                {
                    aspNetCoreElement.SetAttributeValue("hostingModel", aspNetCoreHostingModel);
                }
                else
                {
                    throw new Exception(Resources.WebConfigTransform_HostingModel_Error);
                }
            }
        }

        private static void TransformEnvironmentVariables(XElement envVariablesElement, string environmentName)
        {
            var envVariableElement =
                envVariablesElement.Elements("environmentVariable")
                .FirstOrDefault(e => string.Equals((string)e.Attribute("name"), "ASPNETCORE_ENVIRONMENT", StringComparison.OrdinalIgnoreCase));

            if (envVariableElement == null)
            {
                envVariableElement = new XElement("environmentVariable");
                envVariablesElement.Add(envVariableElement);
            }

            envVariableElement.SetAttributeValue("name", "ASPNETCORE_ENVIRONMENT");
            envVariableElement.SetAttributeValue("value", environmentName);
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
                string[] templatizedLauncherArgs = new string[] { "%LAUNCHER_ARGS%", "-argFile IISExeLauncherArgs.txt" };
                foreach (var templateLauncherArg in templatizedLauncherArgs)
                {
                    var position = 0;
                    while ((position = arguments.IndexOf(templateLauncherArg, position, StringComparison.OrdinalIgnoreCase)) >= 0)
                    {
                        arguments = arguments.Remove(position, templateLauncherArg.Length);
                    }
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
    }
}