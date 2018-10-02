// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Reflection;
using System.Resources;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;

// This is the assembly-level GUID, and the GUID for the TypeLib associated with
// this assembly.  We should specify this explicitly, as opposed to letting 
// tlbexp just pick whatever it wants.  
[assembly: GuidAttribute("E3D4D3B9-944C-407b-A82E-B19719EA7FB3")]

[assembly: InternalsVisibleTo("Microsoft.Build.Tasks.UnitTests, PublicKey=002400000480000094000000060200000024000052534131000400000100010015c01ae1f50e8cc09ba9eac9147cf8fd9fce2cfe9f8dce4f7301c4132ca9fb50ce8cbf1df4dc18dd4d210e4345c744ecb3365ed327efdbc52603faa5e21daa11234c8c4a73e51f03bf192544581ebe107adee3a34928e39d04e524a9ce729d5090bfd7dad9d10c722c0def9ccc08ff0a03790e48bcd1f9b6c476063e1966a1c4")]
[assembly: InternalsVisibleTo("Microsoft.Build.Tasks.Whidbey.Unittest, PublicKey=002400000480000094000000060200000024000052534131000400000100010015c01ae1f50e8cc09ba9eac9147cf8fd9fce2cfe9f8dce4f7301c4132ca9fb50ce8cbf1df4dc18dd4d210e4345c744ecb3365ed327efdbc52603faa5e21daa11234c8c4a73e51f03bf192544581ebe107adee3a34928e39d04e524a9ce729d5090bfd7dad9d10c722c0def9ccc08ff0a03790e48bcd1f9b6c476063e1966a1c4")]

// This will enable passing the SafeDirectories flag to any P/Invoke calls/implementations within the assembly, 
// so that we don't run into known security issues with loading libraries from unsafe locations 
[assembly: DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]

// Needed for the "hub-and-spoke model to locate and retrieve localized resources": https://msdn.microsoft.com/en-us/library/21a15yht(v=vs.110).aspx
// We want "en" to require a satellite assembly for debug builds in order to flush out localization
// issues, but we want release builds to work without it. Also, .net core does not have resource fallbacks
#if (DEBUG && !RUNTIME_TYPE_NETCORE)
[assembly: NeutralResourcesLanguage("en", UltimateResourceFallbackLocation.Satellite)]
#else
[assembly: NeutralResourcesLanguage("en")]
#endif

[assembly: ComVisible(false)]

[assembly: CLSCompliant(true)]

[assembly: AssemblyTitle("Microsoft.Build.Tasks.Core.dll")]
[assembly: AssemblyDescription("Microsoft.Build.Tasks.Core.dll")]
[assembly: AssemblyCompany("Microsoft Corporation")]
[assembly: AssemblyProduct("Microsoft® Build Tools®")]
[assembly: AssemblyCopyright("© Microsoft Corporation. All rights reserved.")]
