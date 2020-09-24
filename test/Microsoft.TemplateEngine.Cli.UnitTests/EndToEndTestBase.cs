using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
using Xunit;

namespace Microsoft.TemplateEngine.Cli.UnitTests
{
    public abstract class EndToEndTestBase
    {
        public void Run(string args, params string[] scripts)
        {
            string codebase = typeof(EndToEndTestBase).GetTypeInfo().Assembly.CodeBase;
            Uri cb = new Uri(codebase);
            string asmPath = cb.LocalPath;
            string dir = Path.GetDirectoryName(asmPath);

#if DEBUG
            string configuration = "Debug";
#else
            string configuration = "Release";
#endif
            string harnessPath = Path.Combine(dir, "..", "..", "..", "..", "bin", "Microsoft.TemplateEngine.EndToEndTestHarness", configuration, "netcoreapp3.1");
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
                Arguments = $"Microsoft.TemplateEngine.EndToEndTestHarness.dll {builder} \"{outputPath}\" {args} -o \"{outputPath}\""
            });

            StringBuilder errorData = new StringBuilder();
            StringBuilder outputData = new StringBuilder();

            p.ErrorDataReceived += (sender, e) =>
            {
                errorData.AppendLine(e.Data);
            };

            p.OutputDataReceived += (sender, e) =>
            {
                outputData.AppendLine(e.Data);
            };

            p.BeginErrorReadLine();
            p.BeginOutputReadLine();
            p.WaitForExit();

            string output = outputData.ToString();
            string error = errorData.ToString();
            Assert.True(0 == p.ExitCode, $@"stdout:
{output}

stderr:
{error}");
        }
    }
}
