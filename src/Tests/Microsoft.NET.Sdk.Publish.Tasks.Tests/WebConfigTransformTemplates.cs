// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.NET.Sdk.Publish.Tasks.Tests
{
    internal class WebConfigTransformTemplates
    {
        public static XDocument WebConfigTemplate => XDocument.Parse(
@"<configuration>
  <location path=""."" inheritInChildApplications=""false"">
      <system.webServer>
        <handlers>
          <add name=""aspNetCore"" path=""*"" verb=""*"" modules=""AspNetCoreModule"" resourceType=""Unspecified""/>
        </handlers>
        <aspNetCore processPath="".\test.exe"" stdoutLogEnabled=""false"" stdoutLogFile="".\logs\stdout"" />
      </system.webServer>
  </location >
</configuration>");

        public static XDocument WebConfigTemplateWithOutExe => XDocument.Parse(
@"<configuration>
  <location path=""."" inheritInChildApplications=""false"">
      <system.webServer>
        <handlers>
          <add name=""aspNetCore"" path=""*"" verb=""*"" modules=""AspNetCoreModule"" resourceType=""Unspecified""/>
        </handlers>
        <aspNetCore processPath="".\test"" stdoutLogEnabled=""false"" stdoutLogFile="".\logs\stdout"" />
      </system.webServer>
  </location >
</configuration>");

        public static XDocument WebConfigTemplatePortable => XDocument.Parse(
@"<configuration>
  <location path=""."" inheritInChildApplications=""false"">
      <system.webServer>
        <handlers>
          <add name=""aspNetCore"" path=""*"" verb=""*"" modules=""AspNetCoreModule"" resourceType=""Unspecified""/>
        </handlers>
        <aspNetCore processPath=""dotnet"" arguments="".\test.dll"" stdoutLogEnabled=""false"" stdoutLogFile="".\logs\stdout"" />
      </system.webServer>
  </location >
</configuration>");

        public static XDocument WebConfigTemplateWithProjectGuid => XDocument.Parse(
@"<configuration>
  <location path=""."" inheritInChildApplications=""false"">
      <system.webServer>
        <handlers>
          <add name=""aspNetCore"" path=""*"" verb=""*"" modules=""AspNetCoreModule"" resourceType=""Unspecified""/>
        </handlers>
        <aspNetCore processPath="".\test.exe"" stdoutLogEnabled=""false"" stdoutLogFile="".\logs\stdout"" />
      </system.webServer>
  </location >
</configuration>
<!--ProjectGuid: 66964EC2-712A-451A-AB4F-33F18D8F54F1-->");

        public static XDocument WebConfigTemplateWithEnvironmentVariable => XDocument.Parse(
@"<configuration>
  <location path=""."" inheritInChildApplications=""false"">
      <system.webServer>
        <handlers>
          <add name=""aspNetCore"" path=""*"" verb=""*"" modules=""AspNetCoreModule"" resourceType=""Unspecified""/>
        </handlers>
        <aspNetCore processPath="".\test.exe"" stdoutLogEnabled=""false"" stdoutLogFile="".\logs\stdout"">
          <environmentVariables>
            <environmentVariable name=""ASPNETCORE_ENVIRONMENT"" value=""Production"" />
          </environmentVariables>
        </aspNetCore>
      </system.webServer>
  </location >
</configuration>");

        public static XDocument WebConfigTemplateWithoutLocation => XDocument.Parse(
@"<configuration>
      <system.webServer>
        <handlers>
          <add name=""aspNetCore"" path=""*"" verb=""*"" modules=""AspNetCoreModule"" resourceType=""Unspecified""/>
        </handlers>
        <aspNetCore processPath="".\test.exe"" stdoutLogEnabled=""false"" stdoutLogFile="".\logs\stdout"" />
      </system.webServer>
</configuration>");

        public static XDocument WebConfigTemplateWithNonRelevantLocationFirst => XDocument.Parse(
@"<configuration>
  <location path=""wwwroot/css"">
    <system.webServer>
      <handlers>
        <remove name=""aspNetCore"" />
      </handlers>
    </system.webServer>
  </location>
  <location path=""wwwroot/css/bundles"">
    <system.webServer>
      <handlers>
        <add name=""aspNetCore"" path=""*"" verb=""*"" modules=""AspNetCoreModule"" resourceType=""Unspecified"" />
      </handlers>
    </system.webServer>
  </location>
      <system.webServer>
        <handlers>
          <add name=""aspNetCore"" path=""*"" verb=""*"" modules=""AspNetCoreModule"" resourceType=""Unspecified""/>
        </handlers>
        <aspNetCore processPath="".\test.exe"" stdoutLogEnabled=""false"" stdoutLogFile="".\logs\stdout"" />
      </system.webServer>
</configuration>");

        public static XDocument WebConfigTemplateWithNonRelevantLocationLast => XDocument.Parse(
@"<configuration>
      <system.webServer>
        <handlers>
          <add name=""aspNetCore"" path=""*"" verb=""*"" modules=""AspNetCoreModule"" resourceType=""Unspecified""/>
        </handlers>
        <aspNetCore processPath="".\test.exe"" stdoutLogEnabled=""false"" stdoutLogFile="".\logs\stdout"" />
      </system.webServer>
  <location path=""wwwroot/css"">
    <system.webServer>
      <handlers>
        <remove name=""aspNetCore"" />
      </handlers>
    </system.webServer>
  </location>
  <location path=""wwwroot/css/bundles"">
    <system.webServer>
      <handlers>
        <add name=""aspNetCore"" path=""*"" verb=""*"" modules=""AspNetCoreModule"" resourceType=""Unspecified"" />
      </handlers>
    </system.webServer>
  </location>
</configuration>");

        public static XDocument WebConfigTemplateWithRelevantLocationFirst => XDocument.Parse(
@"<configuration>
  <location path=""wwwroot/css/bundles"">
    <system.webServer>
      <handlers>
        <add name=""aspNetCore"" path=""*"" verb=""*"" modules=""AspNetCoreModule"" resourceType=""Unspecified"" />
      </handlers>
       <aspNetCore processPath="".\test.exe"" stdoutLogEnabled=""false"" stdoutLogFile="".\logs\stdout"" />
    </system.webServer>
  </location>
      <system.webServer>
        <handlers>
          <add name=""aspNetCore"" path=""*"" verb=""*"" modules=""AspNetCoreModule"" resourceType=""Unspecified""/>
        </handlers>
        <aspNetCore processPath="".\thisshouldnotbechanged.exe"" stdoutLogEnabled=""false"" stdoutLogFile="".\logs\stdout"" />
      </system.webServer>
  <location path=""wwwroot/css"">
    <system.webServer>
      <handlers>
        <remove name=""aspNetCore"" />
      </handlers>
    </system.webServer>
  </location>
</configuration>");

        public static XDocument WebConfigTemplateWithRelevantLocationLast => XDocument.Parse(
@"<configuration>
      <system.webServer>
        <handlers>
          <add name=""aspNetCore"" path=""*"" verb=""*"" modules=""AspNetCoreModule"" resourceType=""Unspecified""/>
        </handlers>
        <aspNetCore processPath="".\test.exe"" stdoutLogEnabled=""false"" stdoutLogFile="".\logs\stdout"" />
      </system.webServer>
  <location path=""wwwroot/css/bundles"">
    <system.webServer>
      <handlers>
        <add name=""aspNetCore"" path=""*"" verb=""*"" modules=""AspNetCoreModule"" resourceType=""Unspecified"" />
      </handlers>
       <aspNetCore processPath="".\thisshouldnotbechanged.exe"" stdoutLogEnabled=""false"" stdoutLogFile="".\logs\stdout"" />
    </system.webServer>
  </location>
  <location path=""wwwroot/css"">
    <system.webServer>
      <handlers>
        <remove name=""aspNetCore"" />
      </handlers>
    </system.webServer>
  </location>
</configuration>");
    }
}
