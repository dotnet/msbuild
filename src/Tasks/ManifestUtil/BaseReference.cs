// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Xml.Serialization;

namespace Microsoft.Build.Tasks.Deployment.ManifestUtilities
{
    /// <summary>
    /// Describes base functionality common to both file and assembly references.
    /// </summary>
    /// <remarks>Note derived classes are serialization formats. Do not rename or remove private members.</remarks>
    [ComVisible(false)]
    public abstract class BaseReference
    {
        private bool _includeHash = true;
        private string _group;
        private string _hash;
        private string _hashAlgorithm;
        private string _isOptional;
        private string _resolvedPath;
        private string _size;
        private string _sourcePath;
        private string _targetPath;

        protected internal BaseReference() // only internal classes can extend this class
        {
        }

        protected internal BaseReference(string path) // only internal classes can extend this class
        {
            _sourcePath = path;
            _targetPath = GetDefaultTargetPath(path);
        }

        internal static string GetDefaultTargetPath(string path)
        {
            if (String.IsNullOrEmpty(path))
            {
                return path;
            }

            if (path.EndsWith(Constants.DeployFileExtension, StringComparison.OrdinalIgnoreCase))
            {
                path = path.Substring(0, path.Length - Constants.DeployFileExtension.Length);
            }

            if (!Path.IsPathRooted(path))
            {
                return path;
            }

            return Path.GetFileName(path);
        }

        internal bool IncludeHash
        {
            get => _includeHash;
            set => _includeHash = value;
        }

        /// <summary>
        /// Specifies the group for on-demand download functionality. A blank string indicates a primary file.
        /// </summary>
        [XmlIgnore]
        public string Group
        {
            get => _group;
            set => _group = value;
        }

        /// <summary>
        /// Specifies the SHA1 hash of the file.
        /// </summary>
        [XmlIgnore]
        public string Hash
        {
            get
            {
                if (!IncludeHash)
                {
                    return string.Empty;
                }
                return _hash;
            }
            set => _hash = value;
        }

        /// <summary>
        /// Specifies whether the file is optional for on-deman download functionality.
        /// </summary>
        [XmlIgnore]
        public bool IsOptional
        {
            get => ConvertUtil.ToBoolean(_isOptional);
            set => _isOptional = value ? "true" : null;
            // NOTE: optional=false is implied, and Fusion prefers them to be unspecified
        }

        /// <summary>
        /// Specifies the resolved path to the file. This path is determined by the Resolve method, and is used to compute the file information by the UpdateFileInfo method.
        /// </summary>
        [XmlIgnore]
        public string ResolvedPath
        {
            get => _resolvedPath;
            set => _resolvedPath = value;
        }

        /// <summary>
        /// Specifies the file size in bytes.
        /// </summary>
        [XmlIgnore]
        public long Size
        {
            get => Convert.ToInt64(_size, CultureInfo.InvariantCulture);
            set => _size = Convert.ToString(value, CultureInfo.InvariantCulture);
        }

        protected internal abstract string SortName { get; }

        /// <summary>
        /// Specifies the source path of the file.
        /// </summary>
        [XmlIgnore]
        public string SourcePath
        {
            get => _sourcePath;
            set => _sourcePath = value;
        }

        /// <summary>
        /// Specifies the target path of the file. This is the path that is used for specification in the generated manifest.
        /// </summary>
        [XmlIgnore]
        public string TargetPath
        {
            get => _targetPath;
            set => _targetPath = value;
        }

        public override string ToString()
        {
            if (!String.IsNullOrEmpty(_sourcePath))
            {
                return _sourcePath;
            }

            if (!String.IsNullOrEmpty(_resolvedPath))
            {
                return _resolvedPath;
            }

            if (!String.IsNullOrEmpty(_targetPath))
            {
                return _targetPath;
            }
            return String.Empty;
        }

        #region " XmlSerializer "

        [Browsable(false)]
        [EditorBrowsable(EditorBrowsableState.Never)]
        [XmlAttribute("Group")]
        public string XmlGroup
        {
            get => _group;
            set => _group = value;
        }

        [Browsable(false)]
        [EditorBrowsable(EditorBrowsableState.Never)]
        [XmlAttribute("Hash")]
        public string XmlHash
        {
            get => Hash;
            set => _hash = value;
        }

        [Browsable(false)]
        [EditorBrowsable(EditorBrowsableState.Never)]
        [XmlAttribute("HashAlg")]
        public string XmlHashAlgorithm
        {
            get => _hashAlgorithm;
            set => _hashAlgorithm = value;
        }

        [Browsable(false)]
        [EditorBrowsable(EditorBrowsableState.Never)]
        [XmlAttribute("IsOptional")]
        public string XmlIsOptional
        {
            get => _isOptional?.ToLower(CultureInfo.InvariantCulture);
            set => _isOptional = value;
        }

        [Browsable(false)]
        [EditorBrowsable(EditorBrowsableState.Never)]
        [XmlAttribute("Path")]
        public string XmlPath
        {
            get => _targetPath;
            set => _targetPath = value;
        }

        [Browsable(false)]
        [EditorBrowsable(EditorBrowsableState.Never)]
        [XmlAttribute("Size")]
        public string XmlSize
        {
            get => _size;
            set => _size = value;
        }

        #endregion
    }
}
