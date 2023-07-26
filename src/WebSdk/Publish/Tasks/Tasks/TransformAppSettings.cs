// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Framework;

namespace Microsoft.NET.Sdk.Publish.Tasks
{
    public class TransformAppSettings : Task
    {
        /// <summary>
        /// Full Path to the Project Folder. This is used to determine the default appsettings location.
        /// </summary>
        [Required]
        public string ProjectDirectory { get; set; }
        /// <summary>
        /// Path to the Publish Folder.
        /// </summary>
        [Required]
        public string PublishDirectory { get; set; }
        /// <summary>
        /// Gets the destination connection string information.
        /// </summary>
        [Required]
        public ITaskItem[] DestinationConnectionStrings { get; set; }
        /// <summary>
        /// AppSettings file name.
        /// </summary>
        public string SourceAppSettingsName { get; set; }
        /// <summary>
        /// Optional: Get the destination AppSettingsName
        /// </summary>
        public string DestinationAppSettingsName { get; set; }
        /// <summary>
        ///  options: Name of the deployment environment.
        /// </summary>
        public string EnvironmentName { get; set; }

        public override bool Execute()
        {
            bool isSuccess = true;

            Log.LogMessage(MessageImportance.Low, $"Updating the destination connection strings");
            isSuccess = TransformAppSettingsInternal();
            if (isSuccess)
            {
                Log.LogMessage(MessageImportance.Low, "Updating the destination connection string completed successfully");
            }

            return isSuccess;
        }

        public bool TransformAppSettingsInternal()
        {
            if (!ValidateInput())
            {
                return false;
            }

            InitializeProperties();

            string sourceAppSettingsFilePath = Path.Combine(ProjectDirectory, SourceAppSettingsName);
            if (string.IsNullOrEmpty(DestinationAppSettingsName))
            {
                DestinationAppSettingsName = $"{Path.GetFileNameWithoutExtension(SourceAppSettingsName)}.{EnvironmentName}{Path.GetExtension(SourceAppSettingsName)}";
            }
            string destinationAppSettingsFilePath = Path.Combine(PublishDirectory, DestinationAppSettingsName);

            // If the source appsettings is not present, generate one.
            if (!File.Exists(sourceAppSettingsFilePath))
            {
                sourceAppSettingsFilePath = AppSettingsTransform.GenerateDefaultAppSettingsJsonFile();
            }

            if (!File.Exists(destinationAppSettingsFilePath))
            {
                File.Copy(sourceAppSettingsFilePath, destinationAppSettingsFilePath);
            }

            AppSettingsTransform.UpdateDestinationConnectionStringEntries(destinationAppSettingsFilePath, DestinationConnectionStrings);

            return true;
        }

        private bool ValidateInput()
        {
            if (!Directory.Exists(PublishDirectory))
            {
                return false;
            }

            return true;
        }

        private void InitializeProperties()
        {
            if (string.IsNullOrEmpty(SourceAppSettingsName))
            {
                SourceAppSettingsName = "appsettings.json";
            }

            if (string.IsNullOrEmpty(EnvironmentName))
            {
                EnvironmentName = "production";
            }
        }

    }
}
