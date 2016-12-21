// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using Microsoft.DotNet.Cli.CommandLine;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.PlatformAbstractions;

namespace Microsoft.DotNet.Tools.New
{
    public class NewCommand
    {
        private static string GetFileNameFromResourceName(string s)
        {
            // A.B.C.D.filename.extension
            string[] parts = s.Split(new char[] { '.' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2)
            {
                return null;
            }

            // filename.extension
            return parts[parts.Length - 2] + "." + parts[parts.Length - 1];
        }

        public int CreateEmptyProject(string languageName, string templateDir, bool isMsBuild)
        {
            // Check if project.json exists in the folder
            if (File.Exists(Path.Combine(Directory.GetCurrentDirectory(), "project.json")) && !isMsBuild)
            {
                Reporter.Error.WriteLine(string.Format(LocalizableStrings.ProjectExistsError, languageName));
                
                return 1;
            }

            var thisAssembly = typeof(NewCommand).GetTypeInfo().Assembly;
            var resources = from resourceName in thisAssembly.GetManifestResourceNames()
                            where resourceName.Contains(templateDir)
                            select resourceName;

            var resourceNameToFileName = new Dictionary<string, string>();
            bool hasFilesToOverride = false;
            foreach (string resourceName in resources)
            {
                string fileName = GetFileNameFromResourceName(resourceName);

                using (var resource = thisAssembly.GetManifestResourceStream(resourceName))
                {
                    var archive = new ZipArchive(resource);

                    try
                    {
                        // Check if other files from the template exists already, before extraction
                        IEnumerable<string> fileNames = archive.Entries.Select(e => e.FullName);
                        foreach (var entry in fileNames)
                        {
                            if (File.Exists(Path.Combine(Directory.GetCurrentDirectory(), entry)))
                            {
                                Reporter.Error.WriteLine(string.Format(LocalizableStrings.ProjectContainsError, languageName, entry));
                                return 1;
                            }
                        }

                        string projectDirectory = Directory.GetCurrentDirectory();

                        archive.ExtractToDirectory(projectDirectory);

                        ReplaceFileTemplateNames(projectDirectory);

                        if (!isMsBuild)
                        {
                            ReplaceProjectJsonTemplateValues(projectDirectory);
                        }
                    }
                    catch (IOException ex)
                    {
                        Reporter.Error.WriteLine(ex.Message);
                        hasFilesToOverride = true;
                    }
                }
            }

            if (hasFilesToOverride)
            {
                Reporter.Error.WriteLine(string.Format(LocalizableStrings.ProjectFailedError, languageName));
                return 1;
            }

            Reporter.Output.WriteLine(string.Format(LocalizableStrings.CreatedNewProject, languageName, Directory.GetCurrentDirectory()));

            return 0;
        }

        private static void ReplaceProjectJsonTemplateValues(string projectDirectory)
        {
            string projectJsonFile = Path.Combine(projectDirectory, "project.json");
            string projectJsonTemplateFile = Path.Combine(projectDirectory, "project.json.template");

            if(File.Exists(projectJsonTemplateFile))
            {
                File.Move(projectJsonTemplateFile, projectJsonFile);
            }
        }

        private static void ReplaceFileTemplateNames(string projectDirectory)
        {
            string projectName = new DirectoryInfo(projectDirectory).Name;
            foreach (string file in Directory.GetFiles(projectDirectory, "*", SearchOption.AllDirectories))
            {
                if (Path.GetFileNameWithoutExtension(file) == "$projectName$")
                {
                    string extension = Path.GetExtension(file);

                    File.Move(
                        file,
                        Path.Combine(Path.GetDirectoryName(file), $"{projectName}{extension}"));
                }
            }
        }

        public static int Run(string[] args)
        {
            DebugHelper.HandleDebugSwitch(ref args);

            var app = new CommandLineApplication();
            app.Name = "dotnet new";
            app.FullName = LocalizableStrings.AppFullName;
            app.Description = LocalizableStrings.AppDescription;
            app.HelpOption("-h|--help");

            var csharp = new { Name = "C#", Alias = new[] { "c#", "cs", "csharp" }, TemplatePrefix = "CSharp", 
                               Templates = new[] 
                               { 
                                   new { Name = "Console", isMsBuild = true }, 
                                   new { Name = "Web", isMsBuild = true }, 
                                   new { Name = "Lib", isMsBuild = true },
                                   new { Name = "Mstest", isMsBuild = true },
                                   new { Name = "Xunittest", isMsBuild = true },
                                   new { Name = "Sln", isMsBuild = true }
                               }
            };

            var fsharp = new { Name = "F#", Alias = new[] { "f#", "fs", "fsharp" }, TemplatePrefix = "FSharp", 
                               Templates = new[] 
                               { 
                                   new { Name = "Console", isMsBuild = true }, 
                                   new { Name = "Web", isMsBuild = true }, 
                                   new { Name = "Lib", isMsBuild = true },
                                   new { Name = "Mstest", isMsBuild = true },
                                   new { Name = "Xunittest", isMsBuild = true },
                                   new { Name = "Sln", isMsBuild = true }
                               }
            };

            var languages = new[] { csharp, fsharp };

            string langValuesString = string.Join(", ", languages.Select(l => l.Name));
            var typeValues = 
                from l in languages
                let values = string.Join(", ", l.Templates.Select(t => t.Name))
                select string.Format(LocalizableStrings.ValidValuesText, l.Name, values);
            string typeValuesString = string.Join(" ", typeValues);

            var lang = app.Option(
                $"-l|--lang <{LocalizableStrings.Language}>", 
                string.Format(LocalizableStrings.LanguageOfProject, langValuesString), 
                CommandOptionType.SingleValue);
            var type = app.Option(
                $"-t|--type <{LocalizableStrings.Type}>", 
                string.Format(LocalizableStrings.TypeOfProject, typeValuesString), 
                CommandOptionType.SingleValue);

            var dotnetNew = new NewCommand();
            app.OnExecute(() =>
            {
                string languageValue = lang.Value() ?? csharp.Name;

                var language = languages
                    .FirstOrDefault(l => l.Alias.Contains(languageValue, StringComparer.OrdinalIgnoreCase));

                if (language == null)
                {
                    Reporter.Error.WriteLine(string.Format(LocalizableStrings.UnrecognizedLanguage, languageValue).Red());
                    return -1;
                }

                string typeValue = type.Value() ?? language.Templates.First().Name;

                var template = language.Templates.FirstOrDefault(t => StringComparer.OrdinalIgnoreCase.Equals(typeValue, t.Name));
                if (template == null)
                {
                    Reporter.Error.WriteLine(string.Format(LocalizableStrings.UnrecognizedType, typeValue).Red());
                    Reporter.Error.WriteLine(string.Format(LocalizableStrings.AvailableTypes, language.Name).Red());
                    foreach (var t in language.Templates)
                    {
                        Reporter.Error.WriteLine($"- {t}".Red());
                    }
                    return -1;
                }

                string templateDir = $"{language.TemplatePrefix}_{template.Name}";

                return dotnetNew.CreateEmptyProject(language.Name, templateDir, template.isMsBuild);
            });

            try
            {
                return app.Execute(args);
            }
            catch (Exception ex)
            {
#if DEBUG
                Reporter.Error.WriteLine(ex.ToString());
#else
                Reporter.Error.WriteLine(ex.Message);
#endif
                return 1;
            }
        }
    }
}
