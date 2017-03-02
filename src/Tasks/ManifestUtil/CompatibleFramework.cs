// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Serialization;

namespace Microsoft.Build.Tasks.Deployment.ManifestUtilities
{
    /// <summary>
    /// Describes a CompatibleFramework for an deployment manifest
    /// </summary>
    [ComVisible(false)]
    public sealed class CompatibleFramework
    {
        private string _version = null;
        private string _profile = null;
        private string _supportedRuntime = null;

        /// <summary>
        /// Initializes a new instance of the CompatibleFramework class
        /// </summary>
        public CompatibleFramework()
        {
        }

        [XmlIgnore]
        public string Version
        {
            get { return _version; }
            set { _version = value; }
        }

        [XmlIgnore]
        public string Profile
        {
            get { return _profile; }
            set { _profile = value; }
        }

        [XmlIgnore]
        public string SupportedRuntime
        {
            get { return _supportedRuntime; }
            set { _supportedRuntime = value; }
        }

        #region " XmlSerializer "

        [Browsable(false)]
        [EditorBrowsable(EditorBrowsableState.Never)]
        [XmlAttribute("Version")]
        public string XmlVersion
        {
            get { return _version; }
            set { _version = value; }
        }

        [Browsable(false)]
        [EditorBrowsable(EditorBrowsableState.Never)]
        [XmlAttribute("Profile")]
        public string XmlProfile
        {
            get { return _profile; }
            set { _profile = value; }
        }

        [Browsable(false)]
        [EditorBrowsable(EditorBrowsableState.Never)]
        [XmlAttribute("SupportedRuntime")]
        public string XmlSupportedRuntime
        {
            get { return _supportedRuntime; }
            set { _supportedRuntime = value; }
        }

        #endregion
    }
}
