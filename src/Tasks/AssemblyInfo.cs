// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

global using NativeMethodsShared = Microsoft.Build.Framework.NativeMethods;
using System;
using System.Resources;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

// This is the assembly-level GUID, and the GUID for the TypeLib associated with
// this assembly.  We should specify this explicitly, as opposed to letting
// tlbexp just pick whatever it wants.
[assembly: GuidAttribute("E3D4D3B9-944C-407b-A82E-B19719EA7FB3")]

[assembly: InternalsVisibleTo("Microsoft.Build.Tasks.UnitTests, PublicKey=002400000480000094000000060200000024000052534131000400000100010015c01ae1f50e8cc09ba9eac9147cf8fd9fce2cfe9f8dce4f7301c4132ca9fb50ce8cbf1df4dc18dd4d210e4345c744ecb3365ed327efdbc52603faa5e21daa11234c8c4a73e51f03bf192544581ebe107adee3a34928e39d04e524a9ce729d5090bfd7dad9d10c722c0def9ccc08ff0a03790e48bcd1f9b6c476063e1966a1c4")]
[assembly: InternalsVisibleTo("MSBuild.Benchmarks, PublicKey=002400000480000094000000060200000024000052534131000400000100010007d1fa57c4aed9f0a32e84aa0faefd0de9e8fd6aec8f87fb03766c834c99921eb23be79ad9d5dcc1dd9ad236132102900b723cf980957fc4e177108fc607774f29e8320e92ea05ece4e821c0a5efe8f1645c4c0c93c1ab99285d622caa652c1dfad63d745d6f2de5f17e5eaf0fc4963d261c8a12436518206dc093344d5ad293")]

// This will enable passing the SafeDirectories flag to any P/Invoke calls/implementations within the assembly,
// so that we don't run into known security issues with loading libraries from unsafe locations
[assembly: DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]

[assembly: NeutralResourcesLanguage("en")]

[assembly: ComVisible(false)]

[assembly: CLSCompliant(true)]
