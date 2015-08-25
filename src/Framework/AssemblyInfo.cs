// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//-----------------------------------------------------------------------
// </copyright>
// <summary>Assembly info.</summary>
//-----------------------------------------------------------------------

using System.Reflection;
using System.Security.Permissions;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using System.Windows.Markup;

// A combination of RequestMinimum and RequestOptional causes the permissions granted to 
// the assembly to only be the permission requested (like a PermitOnly). More generally
// the equation for the PermissionSet granted at load time is:
//
//        Granted = (MaxGrant intersect (ReqMin union ReqOpt)) - ReqRefuse
//
// Where,
//        MaxGrant -- the permissions granted by policy.
//        ReqMin -- the permissions that RequestMinimum is specified for.
//        ReqOpt -- the permissions that RequestOptional is specified for.
//        ReqRefuse -- the permissions that Request refuse is specified for.
//
// Note that if ReqOpt is the empty set, then it is consider to be "FullTrust" and this 
// equation becomes:
//
//        Granted = MaxGrant - ReqRefuse
//
// Regardless of whether ReqMin is empty or not.
#pragma warning disable 618
[assembly: SecurityPermission(SecurityAction.RequestMinimum, Flags = SecurityPermissionFlag.Execution)]
#pragma warning restore 618
[assembly: InternalsVisibleTo("Microsoft.Build.Framework.Unittest, PublicKey=002400000480000094000000060200000024000052534131000400000100010007d1fa57c4aed9f0a32e84aa0faefd0de9e8fd6aec8f87fb03766c834c99921eb23be79ad9d5dcc1dd9ad236132102900b723cf980957fc4e177108fc607774f29e8320e92ea05ece4e821c0a5efe8f1645c4c0c93c1ab99285d622caa652c1dfad63d745d6f2de5f17e5eaf0fc4963d261c8a12436518206dc093344d5ad293")]
[assembly: InternalsVisibleTo("Microsoft.Build.Framework.Whidbey.Unittest, PublicKey=002400000480000094000000060200000024000052534131000400000100010007d1fa57c4aed9f0a32e84aa0faefd0de9e8fd6aec8f87fb03766c834c99921eb23be79ad9d5dcc1dd9ad236132102900b723cf980957fc4e177108fc607774f29e8320e92ea05ece4e821c0a5efe8f1645c4c0c93c1ab99285d622caa652c1dfad63d745d6f2de5f17e5eaf0fc4963d261c8a12436518206dc093344d5ad293")]
#if STANDALONEBUILD
[assembly: AssemblyVersion("14.1.0.0")]
[assembly: InternalsVisibleTo("Microsoft.Build.Framework.UnitTests, PublicKey=002400000480000094000000060200000024000052534131000400000100010007d1fa57c4aed9f0a32e84aa0faefd0de9e8fd6aec8f87fb03766c834c99921eb23be79ad9d5dcc1dd9ad236132102900b723cf980957fc4e177108fc607774f29e8320e92ea05ece4e821c0a5efe8f1645c4c0c93c1ab99285d622caa652c1dfad63d745d6f2de5f17e5eaf0fc4963d261c8a12436518206dc093344d5ad293")]
[assembly: InternalsVisibleTo("Microsoft.Build.Tasks.UnitTests, PublicKey=002400000480000094000000060200000024000052534131000400000100010007d1fa57c4aed9f0a32e84aa0faefd0de9e8fd6aec8f87fb03766c834c99921eb23be79ad9d5dcc1dd9ad236132102900b723cf980957fc4e177108fc607774f29e8320e92ea05ece4e821c0a5efe8f1645c4c0c93c1ab99285d622caa652c1dfad63d745d6f2de5f17e5eaf0fc4963d261c8a12436518206dc093344d5ad293")]
#endif

// This is the assembly-level GUID, and the GUID for the TypeLib associated with
// this assembly.  We should specify this explicitly, as opposed to letting 
// tlbexp just pick whatever it wants.
[assembly: GuidAttribute("D8A9BA71-4724-481d-9CA7-0DA23A1D615C")]

[assembly: XmlnsDefinition("http://schemas.microsoft.com/build/2009/properties", "Microsoft.Build.Framework.XamlTypes")]

// This will enable passing the SafeDirectories flag to any P/Invoke calls/implementations within the assembly, 
// so that we don't run into known security issues with loading libraries from unsafe locations 
[assembly: DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
