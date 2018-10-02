// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Xml.Serialization;

namespace Microsoft.Build.Tasks.Deployment.ManifestUtilities
{
    /// <summary>
    /// Describes a CompatibleFramework for an deployment manifest
    /// </summary>
    [ComVisible(false)]
    public sealed class CompatibleFramework
    {
        private string _version;
        private string _profile;
        private string _supportedRuntime;

        [XmlIgnore]
        public string Version
        {
            get => _version;
            set => _version = value;
        }

        [XmlIgnore]
        public string Profile
        {
            get => _profile;
            set => _profile = value;
        }

        [XmlIgnore]
        public string SupportedRuntime
        {
            get => _supportedRuntime;
            set => _supportedRuntime = value;
        }

        #region " XmlSerializer "

        [Browsable(false)]
        [EditorBrowsable(EditorBrowsableState.Never)]
        [XmlAttribute("Version")]
        public string XmlVersion
        {
            get => _version;
            set => _version = value;
        }

        [Browsable(false)]
        [EditorBrowsable(EditorBrowsableState.Never)]
        [XmlAttribute("Profile")]
        public string XmlProfile
        {
            get => _profile;
            set => _profile = value;
        }

        [Browsable(false)]
        [EditorBrowsable(EditorBrowsableState.Never)]
        [XmlAttribute("SupportedRuntime")]
        public string XmlSupportedRuntime
        {
            get => _supportedRuntime;
            set => _supportedRuntime = value;
        }

        #endregion
    }
}
