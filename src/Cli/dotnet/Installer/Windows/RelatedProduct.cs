// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Microsoft.DotNet.Installer.Windows
{
    /// <summary>
    /// Describes a single row from the MSI <see href="https://docs.microsoft.com/en-us/windows/win32/msi/upgrade-table">Upgrade</see> 
    /// table.
    /// </summary>
    internal class RelatedProduct
    {
        private IEnumerable<int> _languages = null;

        /// <summary>
        /// Additional flags that determine how other columns from the upgrade table are interpreted.
        /// </summary>
        public UpgradeAttributes Attributes
        {
            get;
            set;
        }

        /// <summary>
        /// A comma separated list of languages identifiers (LANGID) that can be detected. When empty, all languages can be 
        /// detected. If msidbUpgradeAttributesLanguagesExclusive is set, the list becomes exclusive.
        /// </summary>
        public string Language
        {
            get;
            set;
        }

        [JsonIgnore]
        public IEnumerable<int> Languages
        {
            get
            {
                if (_languages == null)
                {
                    _languages = string.IsNullOrEmpty(Language)
                        ? Enumerable.Empty<int>()
                        : Language.Split(',').Select(lang => Convert.ToInt32(lang));
                }

                return _languages;
            }
        }

        /// <summary>
        /// The upgrade code of the related product. 
        /// </summary>
        public string UpgradeCode
        {
            get;
            set;
        }

        /// <summary>
        /// Upper boundary of the range of product versions detected by the FindRelatedProducts action.
        /// </summary>
        [JsonConverter(typeof(VersionConverter))]
        public Version VersionMax
        {
            get;
            set;
        }

        /// <summary>
        /// The lower boundary of the range of product versions detected by the FindRelatedProducts action.
        /// </summary>
        [JsonConverter(typeof(VersionConverter))]
        public Version VersionMin
        {
            get;
            set;
        }

        /// <summary>
        /// Determines whether the specified language code is excluded.
        /// </summary>
        /// <param name="lcid">The language to verify.</param>
        /// <returns><see langword="true"/> if the language is exclude; <see langword="false"/> otherwise.</returns>
        public bool ExcludesLanguage(int lcid)
        {
            return Languages.Any() &&
                ((Languages.Contains(lcid) && Attributes.HasFlag(UpgradeAttributes.LanguagesExclusive)) ||
                (!Languages.Contains(lcid) && !Attributes.HasFlag(UpgradeAttributes.LanguagesExclusive)));
        }

        /// <summary>
        /// Determines whether <see cref="VersionMax"/> excludes the specified version.
        /// </summary>
        /// <param name="version">The version to compare.</param>
        /// <returns><see langword="true" /> if the specified version is excluded; <see langword="false"/> otherwise.</returns>
        public bool ExcludesMaxVersion(Version version)
        {
            return (VersionMax != null) && (Attributes.HasFlag(UpgradeAttributes.VersionMaxInclusive)
                ? version > VersionMax : version >= VersionMax);
        }

        /// <summary>
        /// Determines whether <see cref="VersionMin"/> excludes the specified version.
        /// </summary>
        /// <param name="version">The version to compare.</param>
        /// <returns><see langword="true" /> if the specified version is excluded; <see langword="false"/> otherwise.</returns>
        public bool ExcludesMinVersion(Version version)
        {
            return (VersionMin != null) && (Attributes.HasFlag(UpgradeAttributes.VersionMinInclusive)
                ? version < VersionMin : version <= VersionMin);
        }
    }
}
