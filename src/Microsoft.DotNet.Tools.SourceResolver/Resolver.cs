using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Dnx.Runtime;

namespace Microsoft.DotNet.Tools.SourceResolver
{
    public static class Resolver
    {
        public static int Execute(string projectPath, string output)
        {
            var projectFile = new FileInfo(projectPath);
            var reader = new ProjectReader();
            var diagnostics = new List<DiagnosticMessage>();
            Project project;
            using (var stream = File.OpenRead(projectPath))
            {
                project = reader.ReadProject(
                    stream,
                    projectFile.Directory.Name,
                    projectFile.FullName,
                    diagnostics);
            }

            foreach (var diagnostic in diagnostics)
            {
                WriteDiagnostic(diagnostic);
            }

            if (diagnostics.HasErrors())
            {
                return 1;
            }

            foreach (var file in project.Files.SourceFiles)
            {
                Console.WriteLine(file);
            }

            if (!string.IsNullOrEmpty(output))
            {
                File.WriteAllLines(output, project.Files.SourceFiles);
            }

            return 0;
        }

        private static void WriteDiagnostic(DiagnosticMessage diagnostic)
        {
            Console.Error.WriteLine($"{diagnostic.Severity}: {diagnostic.FormattedMessage}");
        }
    }
}