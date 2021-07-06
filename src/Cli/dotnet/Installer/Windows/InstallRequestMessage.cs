// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable disable

using System.Text;
using Microsoft.DotNet.Workloads.Workload.Install.InstallRecord;
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
        /// The dependent value to modify.
        /// </summary>
        public string Dependent
        {
            get;
            set;
        }

        public string ManifestPath
        {
            get;
            set;
        }

        public string LogFile
        {
            get;
            set;
        }

        public string PackageId
        {
            get;
            set;
        }

        public string PackagePath
        {
            get;
            set;
        }

        public string PackageVersion
        {
            get;
            set;
        }

        public string ProductCode
        {
            get;
            set;
        }

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

        public string SdkFeatureBand
        {
            get;
            set;
        }

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
