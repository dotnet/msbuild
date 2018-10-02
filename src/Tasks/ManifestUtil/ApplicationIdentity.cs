// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Runtime.InteropServices;

namespace Microsoft.Build.Tasks.Deployment.ManifestUtilities
{
    /// <summary>
    /// Provides a unique identifier for a ClickOnce application.
    /// </summary>
    [ComVisible(false)]
    public sealed class ApplicationIdentity
    {
        private readonly AssemblyIdentity _applicationManifestIdentity;
        private readonly AssemblyIdentity _deployManifestIdentity;
        private readonly string _url;

        /// <summary>
        /// Initializes a new instance of the ApplicationIdentity class.
        /// </summary>
        /// <param name="url">The deployment provider URL for the ClickOnce deployment manifest.</param>
        /// <param name="deployManifestPath">Path to ClickOnce deployment manifest. The assembly identity will be obtained from the specified file.</param>
        /// <param name="applicationManifestPath">Path to ClickOnce application manifest. The assembly identity will be obtained from the specified file.</param>
        public ApplicationIdentity(string url, string deployManifestPath, string applicationManifestPath)
        {
            if (String.IsNullOrEmpty(url))
            {
                throw new ArgumentNullException(nameof(url));
            }

            if (String.IsNullOrEmpty(deployManifestPath))
            {
                throw new ArgumentNullException(nameof(deployManifestPath));
            }

            if (String.IsNullOrEmpty(applicationManifestPath))
            {
                throw new ArgumentNullException(nameof(applicationManifestPath));
            }
            _url = url;
            _deployManifestIdentity = AssemblyIdentity.FromManifest(deployManifestPath);
            _applicationManifestIdentity = AssemblyIdentity.FromManifest(applicationManifestPath);
        }

        /// <summary>
        /// Initializes a new instance of the ApplicationIdentity class.
        /// </summary>
        /// <param name="url">The deployment provider URL for the ClickOnce deployment manifest.</param>
		/// <param name="deployManifestIdentity">Assembly identity of the ClickOnce deployment manifest.</param>
		/// <param name="applicationManifestIdentity">Assembly identity of the ClickOnce application manifest.</param>
		public ApplicationIdentity(string url, AssemblyIdentity deployManifestIdentity, AssemblyIdentity applicationManifestIdentity)
        {
            if (String.IsNullOrEmpty(url))
                throw new ArgumentNullException(nameof(url));
            _url = url;
            _deployManifestIdentity = deployManifestIdentity ?? throw new ArgumentNullException(nameof(deployManifestIdentity));
            _applicationManifestIdentity = applicationManifestIdentity ?? throw new ArgumentNullException(nameof(applicationManifestIdentity));
        }

        /// <summary>
        /// Returns the full ClickOnce application identity.
        /// </summary>
        /// <returns>A string containing the ClickOnce application identity.</returns>
        public override string ToString()
        {
            string dname = string.Empty;
            if (_deployManifestIdentity != null)
            {
                dname = _deployManifestIdentity.GetFullName(AssemblyIdentity.FullNameFlags.ProcessorArchitecture);
            }

            string aname = string.Empty;
            if (_applicationManifestIdentity != null)
            {
                aname = _applicationManifestIdentity.GetFullName(AssemblyIdentity.FullNameFlags.ProcessorArchitecture | AssemblyIdentity.FullNameFlags.Type);
            }
            return $"{_url}#{dname}/{aname}";
        }
    }
}
