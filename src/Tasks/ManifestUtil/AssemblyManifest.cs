// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Xml.Serialization;

namespace Microsoft.Build.Tasks.Deployment.ManifestUtilities
{
    /// <summary>
    /// Describes a Win32 assembly manifest.
    /// </summary>
    [ComVisible(false)]
    [XmlRoot("AssemblyManifest")]
    public class AssemblyManifest : Manifest
    {
        private ProxyStub[] _externalProxyStubs = null;

        /// <summary>
        /// Initializes a new instance of the AssemblyManifest class.
        /// </summary>
        public AssemblyManifest()
        {
        }

        /// <summary>
        /// Specifies the set of external proxy stubs referenced by the manifest for isolated applications and Reg-Free COM.
        /// </summary>
        [XmlIgnore]
        public ProxyStub[] ExternalProxyStubs
        {
            get { return _externalProxyStubs; }
        }

        #region " XmlSerializer "

        [Browsable(false)]
        [EditorBrowsable(EditorBrowsableState.Never)]
        [XmlArray("ExternalProxyStubs")]
        public ProxyStub[] XmlExternalProxyStubs
        {
            get { return _externalProxyStubs; }
            set { _externalProxyStubs = value; }
        }

        #endregion
    }
}
