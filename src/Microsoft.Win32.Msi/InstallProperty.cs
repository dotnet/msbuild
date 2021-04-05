// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Win32.Msi
{
    /// <summary>
    /// Well known properties for installed or advertised products that can be retrieved using 
    /// <see cref="WindowsInstaller.GetProductInfo(string, string, out string)"/>. Only 
    /// <seealso href="https://docs.microsoft.com/en-us/windows/win32/msi/required-properties">required properties</seealso> are 
    /// guaranteed to be available. Other properties are only available if defined. 
    /// </summary>
    public static class InstallProperty
    {
        /// <summary>
        /// The state of the product, returned in string form as "1" for advertised and "5" for installed.
        /// </summary>
        /// <remarks>
        /// Requires Windows Installer version 3.0 or later.
        /// </remarks>
        public const string PRODUCTSTATE = "State";

        /// <summary>
        /// Support link. See the <see href="https://docs.microsoft.com/en-us/windows/desktop/Msi/arphelplink">ARPHELPLINK</see> 
        /// property for additional information.
        /// </summary>
        public const string HELPLINK = "HelpLink";

        /// <summary>
        /// Support telephone. See the <see href="https://docs.microsoft.com/en-us/windows/desktop/Msi/arphelptelephone">ARPHELPTELEPHONE</see>
        /// property for additional information.
        /// </summary>
        public const string HELPTELEPHONE = "HelpTelephone";

        /// <summary>
        /// The last time the product was serviced by installing a patch or performing a repair.
        /// </summary>
        public const string INSTALLDATE = "InstallDate";

        /// <summary>
        /// Installed language of the product.
        /// </summary>
        /// <remarks>
        /// Requires Windows Installer version 5.0 or later.
        /// </remarks>
        public const string INSTALLEDLANGUAGE = "InstalledLanguage";

        /// <summary>
        /// Product info attributes: installed information
        /// </summary>
        public const string INSTALLEDPRODUCTNAME = "InstalledProductName";

        /// <summary>
        /// The installation location. See the <see href="https://docs.microsoft.com/en-us/windows/desktop/Msi/arpinstalllocation">ARPINSTALLLOCATION</see> property for more information.
        /// </summary>
        public const string INSTALLLOCATION = "InstallLocation";

        /// <summary>
        /// The installation source. For more information see the <see href="https://docs.microsoft.com/en-us/windows/desktop/Msi/sourcedir">SourceDir</see> property.
        /// </summary>
        public const string INSTALLSOURCE = "InstallSource";

        /// <summary>
        /// The local cached package.
        /// </summary>
        public const string LOCALPACKAGE = "LocalPackage";

        /// <summary>
        /// The publisher. For more informaiton see the <see href="https://docs.microsoft.com/en-us/windows/desktop/Msi/manufacturer">Manufacturer</see> property.
        /// </summary>
        public const string PUBLISHER = "Publisher";

        /// <summary>
        /// URL information. See the <see href="https://docs.microsoft.com/en-us/windows/desktop/Msi/arpurlinfoabout">ARPURLINFOABOUT</see> property for more information.
        /// </summary>
        public const string URLINFOABOUT = "URLInfoAbout";

        /// <summary>
        /// URL update information. See the <see href="https://docs.microsoft.com/en-us/windows/desktop/Msi/arpurlupdateinfo">ARPURLUPDATEINFO</see> property for more information.
        /// </summary>
        public const string URLUPDATEINFO = "URLUpdateInfo";

        /// <summary>
        /// The minor product version derived from the <see href="https://docs.microsoft.com/en-us/windows/desktop/Msi/productversion">ProductVersion</see> property.
        /// </summary>
        public const string VERSIONMINOR = "VersionMinor";

        /// <summary>
        /// The major product version derived from the <see href="https://docs.microsoft.com/en-us/windows/desktop/Msi/productversion">ProductVersion</see> property.
        /// </summary>
        public const string VERSIONMAJOR = "VersionMajor";

        /// <summary>
        /// The product version. For more information, see the <see href="https://docs.microsoft.com/en-us/windows/desktop/Msi/productversion">ProductVersion</see> property.
        /// </summary>
        public const string VERSIONSTRING = "VersionString";

        // 

        /// <summary>
        /// Advertised package name.
        /// </summary>
        public const string PACKAGENAME = "PackageName";

        /// <summary>
        /// Advertised transforms.
        /// </summary>
        public const string TRANSFORMS = "Transforms";

        /// <summary>
        /// Advertised language.
        /// </summary>
        public const string LANGUAGE = "Language";

        /// <summary>
        /// Advertised product name.
        /// </summary>
        public const string PRODUCTNAME = "ProductName";

        /// <summary>
        /// 0 if the product is advertized or installed per-user. 1 if the product is advertised or installed for all users.
        /// </summary>
        public const string ASSIGNMENTTYPE = "AssignmentType";

        /// <summary>
        /// Advertised instance type. Requires Windows Installer verions 1.5 or later.
        /// </summary>
        public const string INSTANCETYPE = "InstanceType";

        /// <summary>
        /// Avertised authorized LUA application. Requires Windows Installer version 3.0 or later. 
        /// </summary>
        public const string AUTHORIZED_LUA_APP = "AuthorizedLUAApp";

        /// <summary>
        /// Advertised package code. 
        /// </summary>
        public const string PACKAGECODE = "PackageCode";

        /// <summary>
        /// Advertised version.
        /// </summary>
        public const string VERSION = "Version";

        /// <summary>
        /// Advertised product icon. Requires Windows Installer version 1.1 or later.
        /// </summary>
        public const string PRODUCTICON = "ProductIcon";

        

        

        

        

        

        

        

        

        



        /// <summary>
        /// The product ID used to register the product. 
        /// See <see href="https://docs.microsoft.com/en-us/windows/win32/msi/productid">ProductID</see> for more information.
        /// </summary>
        public const string PRODUCTID = "ProductID";

        /// <summary>
        /// The company registered to use this product.
        /// </summary>
        public const string REGCOMPANY = "RegCompany";

        /// <summary>
        /// The owner registered to use this product.
        /// </summary>
        public const string REGOWNER = "RegOwner";

        

        /// <summary>
        /// </summary>
        /// <remarks>
        /// Requires Windows Installer version 3.0 or later.
        /// </remarks>
        public const string UNINSTALLABLE = "Uninstallable";



        /// <summary>
        /// </summary>
        /// <remarks>
        /// Requires Windows Installer version 3.0 or later.
        /// </remarks>
        public const string PATCHSTATE = "State";

        /// <summary>
        /// </summary>
        /// <remarks>
        /// Requires Windows Installer version 3.0 or later.
        /// </remarks>
        public const string PATCHTYPE = "PatchType";

        /// <summary>
        /// </summary>
        /// <remarks>
        /// Requires Windows Installer version 3.0 or later.
        /// </remarks>
        public const string LUAENABLED = "LUAEnabled";

        /// <summary>
        /// </summary>
        /// <remarks>
        /// Requires Windows Installer version 3.0 or later.
        /// </remarks>
        public const string DISPLAYNAME = "DisplayName";

        /// <summary>
        /// </summary>
        /// <remarks>
        /// Requires Windows Installer version 3.0 or later.
        /// </remarks>
        public const string MOREINFOURL = "MoreInfoURL";

        // Advertised information attributes
        /// <summary>
        /// </summary>
        /// <remarks>
        /// Requires Windows Installer version 3.0 or later.
        /// </remarks>
        public const string LASTUSEDSOURCE = "LastUsedSource";

        /// <summary>
        /// </summary>
        /// <remarks>
        /// Requires Windows Installer version 3.0 or later.
        /// </remarks>
        public const string LASTUSEDTYPE = "LastUsedType";

        /// <summary>
        /// </summary>
        /// <remarks>
        /// Requires Windows Installer version 3.0 or later.
        /// </remarks>
        public const string MEDIAPACKAGEPATH = "MediaPackagePath";

        /// <summary>
        /// </summary>
        /// <remarks>
        /// Requires Windows Installer version 3.0 or later.
        /// </remarks>
        public const string DISKPROMPT = "DiskPrompt";
    }
}
