// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

global using NativeMethodsShared = Microsoft.Build.Framework.NativeMethods;

using System;
using System.Resources;
#if FEATURE_SECURITY_PERMISSIONS
using System.Security.Permissions;
#endif
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

#if FEATURE_SECURITY_PERMISSIONS
#pragma warning disable 618
[assembly: SecurityPermission(SecurityAction.RequestMinimum, Flags = SecurityPermissionFlag.Execution)]
#pragma warning restore 618
#endif

[assembly: InternalsVisibleTo("Microsoft.Build.Utilities.UnitTests, PublicKey=002400000480000094000000060200000024000052534131000400000100010015c01ae1f50e8cc09ba9eac9147cf8fd9fce2cfe9f8dce4f7301c4132ca9fb50ce8cbf1df4dc18dd4d210e4345c744ecb3365ed327efdbc52603faa5e21daa11234c8c4a73e51f03bf192544581ebe107adee3a34928e39d04e524a9ce729d5090bfd7dad9d10c722c0def9ccc08ff0a03790e48bcd1f9b6c476063e1966a1c4")]

// This will enable passing the SafeDirectories flag to any P/Invoke calls/implementations within the assembly,
// so that we don't run into known security issues with loading libraries from unsafe locations
[assembly: DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]

[assembly: NeutralResourcesLanguage("en")]

[assembly: ComVisible(false)]

[assembly: CLSCompliant(true)]
