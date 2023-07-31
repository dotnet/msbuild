// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.NET.TestFramework
{
    public class ConflictResolutionAssets
    {
        public static IEnumerable<Tuple<string, string>> ConflictResolutionDependencies
        {
            get
            {
                string netstandardDependenciesXml = @" 
    <group targetFramework="".NETStandard1.3"">
        <!--dependency id=""Microsoft.NETCore.Platforms"" version=""1.1.0"" /-->
        <dependency id=""Microsoft.Win32.Primitives"" version=""4.3.0"" />
        <dependency id=""System.AppContext"" version=""4.3.0"" />
        <dependency id=""System.Collections"" version=""4.3.0"" />
        <dependency id=""System.Collections.Concurrent"" version=""4.3.0"" />
        <dependency id=""System.Console"" version=""4.3.0"" />
        <dependency id=""System.Diagnostics.Debug"" version=""4.3.0"" />
        <dependency id=""System.Diagnostics.Tools"" version=""4.3.0"" />
        <dependency id=""System.Diagnostics.Tracing"" version=""4.3.0"" />
        <dependency id=""System.Globalization"" version=""4.3.0"" />
        <dependency id=""System.Globalization.Calendars"" version=""4.3.0"" />
        <dependency id=""System.IO"" version=""4.3.0"" />
        <dependency id=""System.IO.Compression"" version=""4.3.0"" />
        <dependency id=""System.IO.Compression.ZipFile"" version=""4.3.0"" />
        <dependency id=""System.IO.FileSystem"" version=""4.3.0"" />
        <dependency id=""System.IO.FileSystem.Primitives"" version=""4.3.0"" />
        <dependency id=""System.Linq"" version=""4.3.0"" />
        <dependency id=""System.Linq.Expressions"" version=""4.3.0"" />
        <dependency id=""System.Net.Http"" version=""4.3.0"" />
        <dependency id=""System.Net.Primitives"" version=""4.3.0"" />
        <dependency id=""System.Net.Sockets"" version=""4.3.0"" />
        <dependency id=""System.ObjectModel"" version=""4.3.0"" />
        <dependency id=""System.Reflection"" version=""4.3.0"" />
        <dependency id=""System.Reflection.Extensions"" version=""4.3.0"" />
        <dependency id=""System.Reflection.Primitives"" version=""4.3.0"" />
        <dependency id=""System.Resources.ResourceManager"" version=""4.3.0"" />
        <dependency id=""System.Runtime"" version=""4.3.0"" />
        <dependency id=""System.Runtime.Extensions"" version=""4.3.0"" />
        <dependency id=""System.Runtime.Handles"" version=""4.3.0"" />
        <dependency id=""System.Runtime.InteropServices"" version=""4.3.0"" />
        <dependency id=""System.Runtime.InteropServices.RuntimeInformation"" version=""4.3.0"" />
        <dependency id=""System.Runtime.Numerics"" version=""4.3.0"" />
        <dependency id=""System.Security.Cryptography.Algorithms"" version=""4.3.0"" />
        <dependency id=""System.Security.Cryptography.Encoding"" version=""4.3.0"" />
        <dependency id=""System.Security.Cryptography.Primitives"" version=""4.3.0"" />
        <dependency id=""System.Security.Cryptography.X509Certificates"" version=""4.3.0"" />
        <dependency id=""System.Text.Encoding"" version=""4.3.0"" />
        <dependency id=""System.Text.Encoding.Extensions"" version=""4.3.0"" />
        <dependency id=""System.Text.RegularExpressions"" version=""4.3.0"" />
        <dependency id=""System.Threading"" version=""4.3.0"" />
        <dependency id=""System.Threading.Tasks"" version=""4.3.0"" />
        <dependency id=""System.Threading.Timer"" version=""4.3.0"" />
        <dependency id=""System.Xml.ReaderWriter"" version=""4.3.0"" />
        <dependency id=""System.Xml.XDocument"" version=""4.3.0"" />
      </group>";

                XElement netStandardDependencies = XElement.Parse(netstandardDependenciesXml);

                foreach (var dependency in netStandardDependencies.Elements("dependency"))
                {
                    yield return Tuple.Create(dependency.Attribute("id").Value, dependency.Attribute("version").Value);
                }

                yield return Tuple.Create("System.Diagnostics.TraceSource", "4.0.0");
            }
        }

        public static string ConflictResolutionTestMethod
        {
            get
            {
                return @"
    public static void TestConflictResolution()
    {
        new System.Diagnostics.TraceSource(""ConflictTest"");
    }";
            }
        }
    }
}
