using System.IO;
using System.Xml;
using System.Xml.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

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
        /// Flag that determines whether the publish telemtry needs to be disabled. 
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
            string webConfigPath = Path.Combine(PublishDir, "web.config");
            webConfigPath = Path.GetFullPath(webConfigPath);

            if (File.Exists(webConfigPath))
            {
                Log.LogMessage($"Updating web.config at '{webConfigPath}'");

                try
                {
                    webConfigXml = XDocument.Load(webConfigPath);
                }
                catch (XmlException e)
                {
                    Log.LogWarning($"Cannot parse web.config as XML. A new web.config will be generated. Error Details : {e.Message}");
                }
            }
            else
            {
                Log.LogMessage($"No web.config found. Creating '{webConfigPath}'");
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
            using (FileStream f = new FileStream(webConfigPath, FileMode.Create))
            {
                transformedConfig.Save(f);
            }

            Log.LogMessage(MessageImportance.Low, "Configuring project completed successfully");
            return true;
        }
    }
}
