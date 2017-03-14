using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using Microsoft.TemplateEngine.Utils;
using Xunit;

namespace Microsoft.TemplateEngine.Cli.UnitTests
{
    public class PrecedenceSelectionTests
    {
        [Theory(DisplayName = nameof(MvcCorrectlyDisambiguatesPrecedenceTest))]
        [InlineData("MvcNoAuthTest.json", "mvc")]
        [InlineData("MvcFramework20Test.json", "mvc")]
        [InlineData("MvcIndAuthTest.json", "mvc -au individual")]
        [InlineData("MvcFramework20Test.json", "mvc -au individual")]

        [InlineData("MvcNoAuthTest.json", "mvc -f netcoreapp1.0")]
        [InlineData("MvcFramework10Test.json", "mvc -f netcoreapp1.0")]
        [InlineData("MvcIndAuthTest.json", "mvc -au individual -f netcoreapp1.0")]
        [InlineData("MvcFramework10Test.json", "mvc -au individual -f netcoreapp1.0")]

        [InlineData("MvcNoAuthTest.json", "mvc -f netcoreapp1.1")]
        [InlineData("MvcFramework11Test.json", "mvc -f netcoreapp1.1")]
        [InlineData("MvcIndAuthTest.json", "mvc -au individual -f netcoreapp1.1")]
        [InlineData("MvcFramework11Test.json", "mvc -au individual -f netcoreapp1.1")]

        [InlineData("MvcNoAuthTest.json", "mvc -f netcoreapp2.0")]
        [InlineData("MvcFramework20Test.json", "mvc -f netcoreapp2.0")]
        [InlineData("MvcIndAuthTest.json", "mvc -au individual -f netcoreapp2.0")]
        [InlineData("MvcFramework20Test.json", "mvc -au individual -f netcoreapp2.0")]
        public void MvcCorrectlyDisambiguatesPrecedenceTest(string script, string args)
        {
            string codebase = typeof(PrecedenceSelectionTests).GetTypeInfo().Assembly.CodeBase;
            Uri cb = new Uri(codebase);
            string asmPath = cb.LocalPath;
            string dir = Path.GetDirectoryName(asmPath);

            string harnessPath = Path.Combine(dir, "..", "..", "..", "..", "Microsoft.TemplateEngine.EndToEndTestHarness");
            string testScript = Path.Combine(dir, script);
            string outputPath = Path.Combine(Directory.GetCurrentDirectory(), "temp");

            Process p = Process.Start(new ProcessStartInfo
            {
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = false,
                WorkingDirectory = harnessPath,
                FileName = "dotnet",
                Arguments = $"run \"{testScript}\" \"{outputPath}\" {args} -o \"{outputPath}\""
            });

            p.WaitForExit();

            string output = p.StandardOutput.ReadToEnd();
            string error = p.StandardError.ReadToEnd();
            Assert.True(0 == p.ExitCode, $@"stdout:
{output}

stderr:
{error}");
        }
    }
}
