// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Newtonsoft.Json;

namespace Microsoft.DotNet.Installer.Windows
{
    /// <summary>
    /// Defines the IPC message structure used to send commands from the unelevated client to the elevated server instance of
    /// the installer
    /// </summary>
    internal class InstallRequestMessage : InstallMessageBase
    {
        /// <summary>
        /// The dependent value to add or remove when updating reference counts.
        /// </summary>
        public string Dependent
        {
            get;
            set;
        }

        /// <summary>
        /// The path of the msi.json manifest.
        /// </summary>
        public string ManifestPath
        {
            get;
            set;
        }

        /// <summary>
        /// The path of the MSI log file to generate when installing, uninstalling or repairing a specific MSI.
        /// </summary>
        public string LogFile
        {
            get;
            set;
        }

        /// <summary>
        /// The package ID of the payload package that carries the MSI.
        /// </summary>
        public string PackageId
        {
            get;
            set;
        }

        /// <summary>
        /// The path of the MSI in the secure package cache.
        /// </summary>
        public string PackagePath
        {
            get;
            set;
        }

        /// <summary>
        /// The version of the payload package carrying the MSI.
        /// </summary>
        public string PackageVersion
        {
            get;
            set;
        }

        /// <summary>
        /// The product code of the MSI.
        /// </summary>
        public string ProductCode
        {
            get;
            set;
        }

        /// <summary>
        /// The provider key name used to track reference counts against an MSI.
        /// </summary>
        public string ProviderKeyName
        {
            get;
            set;
        }

        /// <summary>
        /// The type of the install message.
        /// </summary>
        public InstallRequestType RequestType
        {
            get;
            set;
        }

        /// <summary>
        /// The SDK feature band associated with a workload installation record.
        /// </summary>
        public string SdkFeatureBand
        {
            get;
            set;
        }

        /// <summary>
        /// The workload ID associated with a workload installation record.
        /// </summary>
        public string WorkloadId
        {
            get;
            set;
        }

        /// <summary>
        /// Converts a deserialized array of bytes into an <see cref="InstallRequestMessage"/>.
        /// </summary>
        /// <param name="bytes">The array of bytes to convert.</param>
        /// <returns>An <see cref="InstallRequestMessage"/>.</returns>
        public static InstallRequestMessage Create(byte[] bytes)
        {
            string json = Encoding.UTF8.GetString(bytes);
            return JsonConvert.DeserializeObject<InstallRequestMessage>(json, DefaultSerializerSettings);
        }
    }
}
