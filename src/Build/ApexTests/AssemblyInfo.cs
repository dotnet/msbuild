//-----------------------------------------------------------------------
// <copyright file="Assemblyinfo.cs" company="Microsoft">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
// <summary>AssemblyInfo for Apex test extensions.</summary>
//-----------------------------------------------------------------------
using System.ComponentModel.Composition;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security.Permissions;
using Microsoft.Test.Apex;

// Apex specific Assembly attributes.
[assembly: AssemblyApexTestExtensionLibrary()]

// Friend the MSBuild assembly so that the BackEnd callback works.
[assembly: InternalsVisibleTo("Microsoft.Build.Engine, PublicKey=002400000480000094000000060200000024000052534131000400000100010015c01ae1f50e8cc09ba9eac9147cf8fd9fce2cfe9f8dce4f7301c4132ca9fb50ce8cbf1df4dc18dd4d210e4345c744ecb3365ed327efdbc52603faa5e21daa11234c8c4a73e51f03bf192544581ebe107adee3a34928e39d04e524a9ce729d5090bfd7dad9d10c722c0def9ccc08ff0a03790e48bcd1f9b6c476063e1966a1c4")]
[assembly: InternalsVisibleTo("Microsoft.Build, PublicKey=002400000480000094000000060200000024000052534131000400000100010015c01ae1f50e8cc09ba9eac9147cf8fd9fce2cfe9f8dce4f7301c4132ca9fb50ce8cbf1df4dc18dd4d210e4345c744ecb3365ed327efdbc52603faa5e21daa11234c8c4a73e51f03bf192544581ebe107adee3a34928e39d04e524a9ce729d5090bfd7dad9d10c722c0def9ccc08ff0a03790e48bcd1f9b6c476063e1966a1c4")]