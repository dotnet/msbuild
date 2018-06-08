// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

namespace Microsoft.Build.Tasks.Deployment.ManifestUtilities
{
    internal static class Constants
    {
        public const string CLRPlatformAssemblyName = "Microsoft.Windows.CommonLanguageRuntime";
        public const string DeployFileExtension = ".deploy";
        public const string OSVersion_Win9X = "4.10.0.0";
        public const string OSVersion_WinXP = "5.1.2600.0";
        public static readonly Version EntryPointMinimumImageVersion = new Version("2.0.0.0");
        public const string TargetFrameworkVersion20 = "v2.0";
        public const string TargetFrameworkVersion30 = "v3.0";
        public const string TargetFrameworkVersion35 = "v3.5";
        public const string TargetFrameworkVersion40 = "v4.0";
        public static readonly string[] NET30AssemblyIdentity = { "WindowsBase", "3.0.0.0", "31bf3856ad364e35", "neutral", "msil" };
        public static readonly string[] NET35AssemblyIdentity = { "System.Core", "3.5.0.0", "b77a5c561934e089", "neutral", "msil" };
        public static readonly string[] NET35SP1AssemblyIdentity = { "System.Data.Entity", "3.5.0.0", "b77a5c561934e089", "neutral", "msil" };
        public static readonly string[] NET35ClientAssemblyIdentity = { "Sentinel.v3.5Client", "3.5.0.0", "b03f5f7f11d50a3a", "neutral", "msil" };
        public const string UACAsInvoker = "asInvoker";
        public const string UACUIAccess = "false";
        public const int MaxFileAssociationsCount = 8;
        public const int MaxFileAssociationExtensionLength = 24;
        public const string ClientFrameworkSubset = "Client";
        public const string DotNetFrameworkIdentifier = ".NETFramework";
    }
}
