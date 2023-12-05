// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
namespace Microsoft.Build.Evaluation;
[System.CodeDom.Compiler.GeneratedCode("GenerateAppDomainConfig", "1.0")]
internal sealed partial class NuGetFrameworkWrapper
{
    private const string _bindingRedirect32 = """
<dependentAssembly xmlns="urn:schemas-microsoft-com:asm.v1">
  <assemblyIdentity name="Microsoft.Build" culture="neutral" publicKeyToken="b03f5f7f11d50a3a" />
  <bindingRedirect oldVersion="0.0.0.0-99.9.9.9" newVersion="15.1.0.0" />
</dependentAssembly>
""";
    private const string _bindingRedirect64 = """
<dependentAssembly xmlns="urn:schemas-microsoft-com:asm.v1">
  <assemblyIdentity name="Microsoft.Build" culture="neutral" publicKeyToken="b03f5f7f11d50a3a" />
  <bindingRedirect oldVersion="0.0.0.0-99.9.9.9" newVersion="15.1.0.0" />
  <codeBase version="15.1.0.0" href="..\Microsoft.Build.dll" />
</dependentAssembly>
""";
}
