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
    /// Describes a fileAssociation for an application manifest
    /// </summary>
    [ComVisible(false)]
    public sealed class FileAssociation
    {
        private string _defaultIcon = null;
        private string _description = null;
        private string _extension = null;
        private string _progid = null;

        /// <summary>
        /// Initializes a new instance of the FileAssociation class
        /// </summary>
        public FileAssociation()
        {
        }

        [XmlIgnore]
        public string DefaultIcon
        {
            get { return _defaultIcon; }
            set { _defaultIcon = value; }
        }

        [XmlIgnore]
        public string Description
        {
            get { return _description; }
            set { _description = value; }
        }

        [XmlIgnore]
        public string Extension
        {
            get { return _extension; }
            set { _extension = value; }
        }

        [XmlIgnore]
        public string ProgId
        {
            get { return _progid; }
            set { _progid = value; }
        }

        #region " XmlSerializer "

        [Browsable(false)]
        [EditorBrowsable(EditorBrowsableState.Never)]
        [XmlAttribute("DefaultIcon")]
        public string XmlDefaultIcon
        {
            get { return _defaultIcon; }
            set { _defaultIcon = value; }
        }

        [Browsable(false)]
        [EditorBrowsable(EditorBrowsableState.Never)]
        [XmlAttribute("Description")]
        public string XmlDescription
        {
            get { return _description; }
            set { _description = value; }
        }

        [Browsable(false)]
        [EditorBrowsable(EditorBrowsableState.Never)]
        [XmlAttribute("Extension")]
        public string XmlExtension
        {
            get { return _extension; }
            set { _extension = value; }
        }

        [Browsable(false)]
        [EditorBrowsable(EditorBrowsableState.Never)]
        [XmlAttribute("Progid")]
        public string XmlProgId
        {
            get { return _progid; }
            set { _progid = value; }
        }

        #endregion
    }
}
