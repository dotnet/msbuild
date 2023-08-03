// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Win32.Msi
{
    /// <summary>
    /// Describes different flags when reinstalling (repairing) a product.
    /// </summary>
    [Flags]
    public enum ReinstallMode
    {
        /// <summary>
        /// Reserved bit - currently ignored.
        /// </summary>
        REPAIR = 0x00000001,

        /// <summary>
        /// Reinstall only if the file is missing.
        /// </summary>
        FILEMISSING = 0x00000002,

        /// <summary>
        /// Reinstall if the file is missing or is an older version.
        /// </summary>
        FILEOLDERVERSION = 0x00000004,

        /// <summary>
        /// Reinstall if the file is missing, or is an equal or older version.
        /// </summary>
        FILEEQUALVERSION = 0x00000008,

        /// <summary>
        /// Reinstall if the file is missing or is a different version.
        /// </summary>
        FILEEXACT = 0x00000010,

        /// <summary>
        /// Verify the checksum values and reinstall the file if they are missing or corrupt. This flag 
        /// only repairs files that have msidbFileAttributesChecksum in the Attributes column of the File table.
        /// </summary>
        FILEVERIFY = 0x00000020,

        /// <summary>
        /// Force all files to be reinstalled, regardless of checksum or version.
        /// </summary>
        FILEREPLACE = 0x00000040,

        /// <summary>
        /// Rewrite all required registry entries from the Registry Table that go to the HKEY_LOCAL_MACHINE
        /// or HKEY_CLASSES_ROOT registry hive. Rewrite all information from the Class Table, Verb Table, 
        /// PublishComponent Table, ProgID Table, MIMET Table, Icon Table, Extension Table, and AppID Table regardless of 
        /// machine or user assignment. Reinstall all qualified components.
        /// </summary>
        MACHINEDATA = 0x00000080,

        /// <summary>
        /// Rewrite all required registry entries from the Registry Table that go to the HKEY_CURRENT_USER or HKEY_USERS
        /// registry hive.
        /// </summary>
        USERDATA = 0x00000100,

        /// <summary>
        /// Reinstall all shortcuts and re-cache all icons overwriting any existing shortcuts and icons.
        /// </summary>
        SHORTCUT = 0x00000200,

        /// <summary>
        /// Use to run from the source package and re-cache the local package. Do not use for the first
        /// installation of an application or feature.
        /// </summary>
        PACKAGE = 0x00000400,
    }
}
