// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Xml;
using Microsoft.Build.Framework;

namespace Microsoft.NET.Sdk.Publish.Tasks
{
    public class TransformWebConfig : Task
    {
        /// <summary>
        /// The full path to the assembly or executable to be compiled, including file extension
        /// </summary>
        /// <returns></returns>
        [Required]
        public string TargetPath { get; set; }

        /// <summary>
        /// Destination folder of the publish
        /// </summary>
        /// <returns></returns>
        [Required]
        public string PublishDir { get; set; }

        [Required]
        public bool UseAppHost { get; set; }

        /// <summary>
        /// [optional] Transform is targeted for Azure
        /// </summary>
        /// <returns></returns>
        public bool IsAzure { get; set; }
        
        /// <summary>
        /// ProjectGuid that uniquely identifies the project. Used for Telemetry
        /// </summary>
        public string ProjectGuid { get; set; }
        
        /// <summary>
        /// Flag that determines whether the publish telemetry needs to be disabled. 
        /// </summary>
        public bool IgnoreProjectGuid { get; set; }
        /// <summary>
        /// Absolute path to the project file.
        /// </summary>
        public string ProjectFullPath { get; set; }
        /// <summary>
        /// Absolute path to the Solution file.
        /// </summary>
        public string SolutionPath { get; set; }
        /// <summary>
        /// Native executable extension
        /// </summary>
        public string ExecutableExtension { get; set; }

        /// <summary>
        /// AspNetCoreHostingModel defines whether the hosting will be InProcess or OutOfProcess.
        /// </summary>
        /// <returns></returns>
        public string AspNetCoreHostingModel { get; set; }

        /// <summary>
        /// AspNetCoreModule defines the module name
        /// </summary>
        /// <returns></returns>
        public string AspNetCoreModuleName { get; set; }

        public string EnvironmentName { get; set; }

        public override bool Execute()
        {
            Log.LogMessage(MessageImportance.Low, $"Configuring the following project for use with IIS: '{PublishDir}'");

            XDocument webConfigXml = null;

            // Initialize the publish web.config file with project web.config content if present. Else, clean the existing web.config in the
            // publish folder to make sure we have a consistent web.config update experience.
            string defaultWebConfigPath = "web.config";
            string projectWebConfigPath = null;
            string publishWebConfigPath = Path.Combine(PublishDir, defaultWebConfigPath);

            if (!string.IsNullOrEmpty(ProjectFullPath))
            {
                //Ensure that we load the actual web.config name (case-sensitive on Unix-like systems)
                projectWebConfigPath = GetWebConfigFileOrDefault(ProjectFullPath, defaultWebConfigPath);
                publishWebConfigPath = Path.Combine(PublishDir, Path.GetFileName(projectWebConfigPath));
            }

            publishWebConfigPath = Path.GetFullPath(publishWebConfigPath);

            if (File.Exists(publishWebConfigPath))
            {
                if (File.Exists(projectWebConfigPath))
                {
                    File.Copy(projectWebConfigPath, publishWebConfigPath, true);
                }
                else
                {
                    File.WriteAllText(publishWebConfigPath, WebConfigTemplate.Template);
                }

                Log.LogMessage($"Updating web.config at '{publishWebConfigPath}'");

                try
                {
                    webConfigXml = XDocument.Load(publishWebConfigPath);
                }
                catch (XmlException e)
                {
                    Log.LogWarning($"Cannot parse web.config as XML. A new web.config will be generated. Error Details : {e.Message}");
                }
            }
            else
            {
                Log.LogMessage($"No web.config found. Creating '{publishWebConfigPath}'");
            }

            if (IsAzure)
            {
                Log.LogMessage("Configuring web.config for deployment to Azure");
            }

            string outputFile = Path.GetFileName(TargetPath);
            XDocument transformedConfig = WebConfigTransform.Transform(
                webConfigXml,
                outputFile,
                IsAzure,
                UseAppHost,
                ExecutableExtension,
                AspNetCoreModuleName,
                AspNetCoreHostingModel,
                EnvironmentName,
                ProjectFullPath);

            // Telemetry
            transformedConfig = WebConfigTelemetry.AddTelemetry(transformedConfig, ProjectGuid, IgnoreProjectGuid, SolutionPath, ProjectFullPath);
            using (FileStream f = new FileStream(publishWebConfigPath, FileMode.Create))
            {
                transformedConfig.Save(f);
            }

            Log.LogMessage(MessageImportance.Low, "Configuring project completed successfully");
            return true;
        }

        /// <summary>
        /// Searches for an existing (case-insensitive) `Web.config` file. Otherwise defaults to defaultWebConfigName
        /// </summary>
        /// <param name="projectPath">Full path to project file</param>
        /// <param name="defaultWebConfigName">Web Config file name to search for i.e. `web.config`</param> 
        /// <returns></returns>
        public string GetWebConfigFileOrDefault(string projectPath, string defaultWebConfigName)
        {
            var projectDirectory = Path.GetDirectoryName(projectPath);
            var currentWebConfigFileName = Directory.EnumerateFiles(projectDirectory)
                .FirstOrDefault(file => string.Equals(Path.GetFileName(file), defaultWebConfigName, StringComparison.OrdinalIgnoreCase));
            var webConfigFileName = currentWebConfigFileName == null ? defaultWebConfigName : Path.GetFileName(currentWebConfigFileName);
            var projectWebConfigPath = Path.Combine(projectDirectory, webConfigFileName); 

            return projectWebConfigPath; 
        }

    }
}
