using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
using Microsoft.TemplateEngine.Utils;
using Xunit;

namespace Microsoft.TemplateEngine.Cli.UnitTests
{
    public class PrecedenceSelectionTests
    {
        [Theory(DisplayName = nameof(VerifyTemplateContent))]
        [InlineData("mvc", "MvcNoAuthTest.json", "MvcFramework20Test.json")]
        [InlineData("mvc -au individual", "MvcIndAuthTest.json", "MvcFramework20Test.json")]

        [InlineData("mvc -f netcoreapp1.0", "MvcNoAuthTest.json", "MvcFramework10Test.json")]
        [InlineData("mvc -au individual -f netcoreapp1.0", "MvcIndAuthTest.json", "MvcFramework10Test.json")]

        [InlineData("mvc -f netcoreapp1.1", "MvcNoAuthTest.json", "MvcFramework11Test.json")]
        [InlineData("mvc -au individual -f netcoreapp1.1", "MvcIndAuthTest.json", "MvcFramework11Test.json")]

        [InlineData("mvc -f netcoreapp2.0", "MvcNoAuthTest.json", "MvcFramework20Test.json")]
        [InlineData("mvc -au individual -f netcoreapp2.0", "MvcIndAuthTest.json", "MvcFramework20Test.json")]
        public void VerifyTemplateContent(string args, params string[] scripts)
        {
            string codebase = typeof(PrecedenceSelectionTests).GetTypeInfo().Assembly.CodeBase;
            Uri cb = new Uri(codebase);
            string asmPath = cb.LocalPath;
            string dir = Path.GetDirectoryName(asmPath);

            string harnessPath = Path.Combine(dir, "..", "..", "..", "..", "Microsoft.TemplateEngine.EndToEndTestHarness");
            int scriptCount = scripts.Length;
            StringBuilder builder = new StringBuilder();
            builder.Append(scriptCount);
            builder.Append(" ");

            foreach (string script in scripts)
            {
                string testScript = Path.Combine(dir, script);
                builder.Append($"\"{testScript}\" ");
            }

            string outputPath = Path.Combine(Directory.GetCurrentDirectory(), "temp");

            Process p = Process.Start(new ProcessStartInfo
            {
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = false,
                WorkingDirectory = harnessPath,
                FileName = "dotnet",
                Arguments = $"run {builder} \"{outputPath}\" {args} -o \"{outputPath}\""
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
