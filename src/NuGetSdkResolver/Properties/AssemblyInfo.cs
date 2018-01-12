// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//-----------------------------------------------------------------------
// </copyright>
// <summary>Assembly info.</summary>
//-----------------------------------------------------------------------

using System;
using System.Reflection;
#if (LOCALIZED_BUILD)
using System.Resources;
#endif

#if STATIC_VERSION_NUMBER
[assembly: AssemblyVersion(Microsoft.Build.Shared.MSBuildConstants.CurrentAssemblyVersion)]
[assembly: AssemblyFileVersion(Microsoft.Build.Shared.MSBuildConstants.CurrentAssemblyFileVersion)]
#endif

#if (LOCALIZED_BUILD)
// Needed for the "hub-and-spoke model to locate and retrieve localized resources": https://msdn.microsoft.com/en-us/library/21a15yht(v=vs.110).aspx
// We want "en" to require a satellite assembly for debug builds in order to flush out localization
// issues, but we want release builds to work without it. Also, .net core does not have resource fallbacks
#if (DEBUG && !RUNTIME_TYPE_NETCORE)
[assembly: NeutralResourcesLanguage("en", UltimateResourceFallbackLocation.Satellite)]
#else
[assembly: NeutralResourcesLanguage("en")]
#endif
#endif

[assembly: CLSCompliant(true)]

[assembly: AssemblyTitle("NuGet.MSBuildSdkResolver.dll")]
[assembly: AssemblyDescription("NuGet.MSBuildSdkResolver.dll")]
[assembly: AssemblyCompany("Microsoft Corporation")]
[assembly: AssemblyProduct("Microsoft® Build Tools®")]
[assembly: AssemblyCopyright("© Microsoft Corporation. All rights reserved.")]
