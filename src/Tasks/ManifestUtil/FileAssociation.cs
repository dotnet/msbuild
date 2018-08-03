// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Xml.Serialization;

namespace Microsoft.Build.Tasks.Deployment.ManifestUtilities
{
    /// <summary>
    /// Describes a fileAssociation for an application manifest
    /// </summary>
    [ComVisible(false)]
    public sealed class FileAssociation
    {
        private string _defaultIcon;
        private string _description;
        private string _extension;
        private string _progid;

        [XmlIgnore]
        public string DefaultIcon
        {
            get => _defaultIcon;
            set => _defaultIcon = value;
        }

        [XmlIgnore]
        public string Description
        {
            get => _description;
            set => _description = value;
        }

        [XmlIgnore]
        public string Extension
        {
            get => _extension;
            set => _extension = value;
        }

        [XmlIgnore]
        public string ProgId
        {
            get => _progid;
            set => _progid = value;
        }

        #region " XmlSerializer "

        [Browsable(false)]
        [EditorBrowsable(EditorBrowsableState.Never)]
        [XmlAttribute("DefaultIcon")]
        public string XmlDefaultIcon
        {
            get => _defaultIcon;
            set => _defaultIcon = value;
        }

        [Browsable(false)]
        [EditorBrowsable(EditorBrowsableState.Never)]
        [XmlAttribute("Description")]
        public string XmlDescription
        {
            get => _description;
            set => _description = value;
        }

        [Browsable(false)]
        [EditorBrowsable(EditorBrowsableState.Never)]
        [XmlAttribute("Extension")]
        public string XmlExtension
        {
            get => _extension;
            set => _extension = value;
        }

        [Browsable(false)]
        [EditorBrowsable(EditorBrowsableState.Never)]
        [XmlAttribute("Progid")]
        public string XmlProgId
        {
            get => _progid;
            set => _progid = value;
        }

        #endregion
    }
}
