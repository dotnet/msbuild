// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.ProjectModel;
using Microsoft.DotNet.ProjectModel.Compilation;
using Microsoft.DotNet.ProjectModel.Resources;

namespace Microsoft.DotNet.Tools.Compiler
{
    public abstract class Compiler : ICompiler
    {
        public abstract bool Compile(ProjectContext context, CompilerCommandApp args);

        protected static bool PrintSummary(List<DiagnosticMessage> diagnostics, Stopwatch sw, bool success = true)
        {
            PrintDiagnostics(diagnostics);

            Reporter.Output.WriteLine();

            var errorCount = diagnostics.Count(d => d.Severity == DiagnosticMessageSeverity.Error);
            var warningCount = diagnostics.Count(d => d.Severity == DiagnosticMessageSeverity.Warning);

            if (errorCount > 0 || !success)
            {
                Reporter.Output.WriteLine("Compilation failed.".Red());
                success = false;
            }
            else
            {
                Reporter.Output.WriteLine("Compilation succeeded.".Green());
            }

            Reporter.Output.WriteLine($"    {warningCount} Warning(s)");
            Reporter.Output.WriteLine($"    {errorCount} Error(s)");

            Reporter.Output.WriteLine();

            Reporter.Output.WriteLine($"Time elapsed {sw.Elapsed}");

            return success;
        }

        protected static bool AddNonCultureResources(Project project, List<string> compilerArgs, string intermediateOutputPath)
        {
            var resgenFiles = CompilerUtil.GetNonCultureResources(project, intermediateOutputPath);

            foreach (var resgenFile in resgenFiles)
            {
                if (ResourceUtility.IsResxFile(resgenFile.InputFile))
                {
                    var arguments = new[]
                    {
                        $"\"{resgenFile.InputFile}\"",
                        $"-o:\"{resgenFile.OutputFile}\"",
                        $"-v:{project.Version.Version}"
                    };

                    var rsp = Path.Combine(intermediateOutputPath, $"dotnet-resgen-resx.rsp");
                    File.WriteAllLines(rsp, arguments);

                    var result = Resgen.ResgenCommand.Run(new[] { $"@{rsp}" });

                    if (result != 0)
                    {
                        return false;
                    }

                    compilerArgs.Add($"--resource:\"{resgenFile.OutputFile}\",{Path.GetFileName(resgenFile.MetadataName)}");
                }
                else
                {
                    compilerArgs.Add($"--resource:\"{resgenFile.InputFile}\",{Path.GetFileName(resgenFile.MetadataName)}");
                }
            }

            return true;
        }

        protected static bool GenerateCultureResourceAssemblies(
            Project project,
            List<LibraryExport> dependencies,
            string intermediateOutputPath,
            string outputPath)
        {
            var referencePaths = CompilerUtil.GetReferencePathsForCultureResgen(dependencies);
            var resgenReferenceArgs = referencePaths.Select(path => $"-r:\"{path}\"").ToList();
            var cultureResgenFiles = CompilerUtil.GetCultureResources(project, outputPath);

            foreach (var resgenFile in cultureResgenFiles)
            {
                var resourceOutputPath = Path.GetDirectoryName(resgenFile.OutputFile);

                if (!Directory.Exists(resourceOutputPath))
                {
                    Directory.CreateDirectory(resourceOutputPath);
                }

                var arguments = new List<string>();

                arguments.AddRange(resgenReferenceArgs);
                arguments.Add($"-o:\"{resgenFile.OutputFile}\"");
                arguments.Add($"-c:{resgenFile.Culture}");
                arguments.Add($"-v:{project.Version.Version}");
                arguments.AddRange(resgenFile.InputFileToMetadata.Select(fileToMetadata => $"\"{fileToMetadata.Key}\",{fileToMetadata.Value}"));
                var rsp = Path.Combine(intermediateOutputPath, $"dotnet-resgen.rsp");
                File.WriteAllLines(rsp, arguments);

                var result = Resgen.ResgenCommand.Run(new[] { $"@{rsp}" });
                if (result != 0)
                {
                    return false;
                }
            }

            return true;
        }

        protected static DiagnosticMessage ParseDiagnostic(string projectRootPath, string line)
        {
            var error = CanonicalError.Parse(line);

            if (error != null)
            {
                var severity = error.category == CanonicalError.Parts.Category.Error ?
                DiagnosticMessageSeverity.Error : DiagnosticMessageSeverity.Warning;

                return new DiagnosticMessage(
                    error.code,
                    error.text,
                    Path.IsPathRooted(error.origin) ? line : projectRootPath + Path.DirectorySeparatorChar + line,
                    Path.Combine(projectRootPath, error.origin),
                    severity,
                    error.line,
                    error.column,
                    error.endColumn,
                    error.endLine,
                    source: null);
            }

            return null;
        }

        private static void PrintDiagnostics(List<DiagnosticMessage> diagnostics)
        {
            foreach (var diag in diagnostics)
            {
                PrintDiagnostic(diag);
            }
        }

        private static void PrintDiagnostic(DiagnosticMessage diag)
        {
            switch (diag.Severity)
            {
                case DiagnosticMessageSeverity.Info:
                    Reporter.Error.WriteLine(diag.FormattedMessage);
                    break;
                case DiagnosticMessageSeverity.Warning:
                    Reporter.Error.WriteLine(diag.FormattedMessage.Yellow().Bold());
                    break;
                case DiagnosticMessageSeverity.Error:
                    Reporter.Error.WriteLine(diag.FormattedMessage.Red().Bold());
                    break;
            }
        }

        private static void CopyFiles(IEnumerable<LibraryAsset> files, string outputPath)
        {
            foreach (var file in files)
            {
                File.Copy(file.ResolvedPath, Path.Combine(outputPath, Path.GetFileName(file.ResolvedPath)), overwrite: true);
            }
        }

        private static string EnsureTrailingSlash(string path)
        {
            return EnsureTrailingCharacter(path, Path.DirectorySeparatorChar);
        }

        private static string EnsureTrailingCharacter(string path, char trailingCharacter)
        {
            if (path == null)
            {
                throw new ArgumentNullException(nameof(path));
            }

            // if the path is empty, we want to return the original string instead of a single trailing character.
            if (path.Length == 0 || path[path.Length - 1] == trailingCharacter)
            {
                return path;
            }

            return path + trailingCharacter;
        }
    }
}
