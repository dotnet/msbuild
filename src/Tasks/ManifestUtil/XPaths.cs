// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Build.Tasks.Deployment.ManifestUtilities
{
    internal static class XPaths
    {
        public const string applicationRequestMinimumElement = "asmv2:applicationRequestMinimum";
        public const string assemblyElement = "asmv1:assembly";
        public const string assemblyIdentityPath = "/asmv1:assembly/asmv1:assemblyIdentity|/asmv1:assembly/asmv2:assemblyIdentity";
        public const string clsidAttribute = "asmv1:comClass/@clsid";
        public const string comFilesPath = "/asmv1:assembly/asmv1:file[asmv1:typelib or asmv1:comClass]";
        public const string configBindingRedirect = "configuration/runtime/asmv1:assemblyBinding/asmv1:dependentAssembly/asmv1:bindingRedirect";
        public const string defaultAssemblyRequestElement = "asmv2:defaultAssemblyRequest";
        public const string dependencyPublicKeyTokenAttribute = "asmv2:assemblyIdentity/@publicKeyToken";
        public const string fileNameAttribute = "@name";
        public const string fileSizeAttribute = "asmv2:size";
        public const string hashElement = "asmv2:hash/dsig:DigestValue";
        public const string idAttribute = "asmv2:ID";
        public const string languageAttribute1 = "asmv1:assemblyIdentity/@language";
        public const string languageAttribute2 = "asmv2:assemblyIdentity/@language";
        public const string manifestTrustInfoPath = "/asmv1:assembly/asmv2:trustInfo";
        public const string permissionIdentityQuery = "asmv2:IPermission[@class='{0}']";
        public const string permissionClassAttributeQuery = "asmv2:IPermission/@class";
        public const string permissionSetElement = "asmv2:PermissionSet";
        public const string permissionSetReferenceAttribute = "asmv2:permissionSetReference";
        public const string publicKeyTokenAttribute = "asmv2:publicKeyToken";
        public const string requestedExecutionLevelPath = "/asmv1:assembly/asmv2:trustInfo/asmv2:security/asmv3:requestedPrivileges/asmv3:requestedExecutionLevel";
        public const string requestedPrivilegeElement = "asmv3:requestedPrivileges";
        public const string requestedExecutionLevelElement = "asmv3:requestedExecutionLevel";
        public const string sameSiteAttribute = "asmv2:SameSite";
        public const string securityElement = "asmv2:security";
        public const string signaturePath = "/asmv1:assembly/dsig:Signature";
        public const string tlbidAttribute = "asmv1:typelib/@tlbid";
        public const string trustInfoElement = "asmv2:trustInfo";
        public const string trustInfoPath = "/asmv2:trustInfo";
        public const string unrestrictedAttribute = "asmv2:Unrestricted";

        // List of paths where codebase may be found in a manifest.
        // Used by Manifest class.
        // In order of most likely occurance....
        public static readonly string[] codebasePaths =
        {
            "/asmv1:assembly/asmv2:dependency/asmv2:dependentAssembly/@codebase",
            "/asmv1:assembly/asmv1:dependency/asmv1:dependentAssembly/@asmv2:codebase",
            "/asmv1:assembly/asmv1:dependency/asmv2:dependentAssembly/@codebase",
            "/asmv1:assembly/asmv2:dependency/asmv1:dependentAssembly/@asmv2:codebase"
        };

        // List of attributes that are to be filtered out if empty.
        // Used by ManifestFormatter class.
        // These must be defined in sorted order!
        public static readonly string[] emptyAttributeList =
        {
            "asmv1:assemblyIdentity/@language",
            "asmv1:assemblyIdentity/@processorArchitecture",
            "asmv1:assemblyIdentity/@publicKeyToken",
            "asmv1:assemblyIdentity/@type",
            "asmv1:comClass/@description",
            "asmv1:comClass/@progid",
            "asmv1:comClass/@threadingModel",
            "asmv1:dependency/@optional",
            "asmv1:dependentAssembly/@asmv2:codebase",
            "asmv1:dependentAssembly/@asmv2:group",
            "asmv1:dependentAssembly/@asmv2:hash",
            "asmv1:dependentAssembly/@asmv2:hashalg",
            "asmv1:dependentAssembly/@asmv2:optional",
            "asmv1:dependentAssembly/@asmv2:resourceFallbackCulture",
            "asmv1:dependentAssembly/@asmv2:resourceFallbackCultureInternal",
            "asmv1:dependentAssembly/@asmv2:resourceType",
            "asmv1:dependentAssembly/@asmv2:size",
            "asmv1:description/@asmv2:iconFile",
            "asmv1:description/@asmv2:product",
            "asmv1:description/@asmv2:publisher",
            "asmv1:description/@asmv2:supportUrl",
            "asmv1:description/@co.v1:errorReportUrl",
            "asmv1:description/@co.v1:suiteName",
            "asmv1:file/@asmv2:group",
            "asmv1:file/@asmv2:optional",
            "asmv1:file/@asmv2:writeableType",
            "asmv1:typelib/@flags",
            "asmv2:assemblyIdentity/@language",
            "asmv2:assemblyIdentity/@processorArchitecture",
            "asmv2:assemblyIdentity/@publicKeyToken",
            "asmv2:assemblyIdentity/@type",
            "asmv2:dependency/@optional",
            "asmv2:dependentAssembly/@codebase",
            "asmv2:dependentAssembly/@group",
            "asmv2:dependentAssembly/@hash",
            "asmv2:dependentAssembly/@hashalg",
            "asmv2:dependentAssembly/@optional",
            "asmv2:dependentAssembly/@resourceFallbackCulture",
            "asmv2:dependentAssembly/@resourceFallbackCultureInternal",
            "asmv2:dependentAssembly/@resourceType",
            "asmv2:dependentAssembly/@size",
            "asmv2:dependentOS/@description",
            "asmv2:dependentOS/@supportUrl",
            "asmv2:deployment/@co.v1:createDesktopShortcut",
            "asmv2:deployment/@disallowUrlActivation",
            "asmv2:deployment/@install",
            "asmv2:deployment/@mapFileExtensions",
            "asmv2:deployment/@minimumRequiredVersion",
            "asmv2:deployment/@trustURLParameters",
            "asmv2:description/@iconFile",
            "asmv2:description/@product",
            "asmv2:description/@publisher",
            "asmv2:description/@supportUrl",
            "asmv2:file/@group",
            "asmv2:file/@optional",
            "asmv2:file/@writeableType",
        };
    }
}
