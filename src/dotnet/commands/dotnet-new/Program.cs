using Microsoft.Dnx.Runtime.Common.CommandLine;
using Microsoft.DotNet.Cli.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

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

        public int CreateEmptyProject(string languageName, string templateDir)
        {
            var thisAssembly = typeof(NewCommand).GetTypeInfo().Assembly;
            var resources = from resourceName in thisAssembly.GetManifestResourceNames()
                            where resourceName.Contains(templateDir)
                            select resourceName;

            var resourceNameToFileName = new Dictionary<string, string>();
            bool hasFilesToOverride = false;
            foreach (string resourceName in resources)
            {
                string fileName = GetFileNameFromResourceName(resourceName);

                resourceNameToFileName.Add(resourceName, fileName);
                if (File.Exists(fileName))
                {
                    Reporter.Error.WriteLine($"Creating new {languageName} project would override file {fileName}.");
                    hasFilesToOverride = true;
                }
            }

            if (hasFilesToOverride)
            {
                Reporter.Error.WriteLine($"Creating new {languageName} project failed.");
                return 1;
            }

            foreach (var kv in resourceNameToFileName)
            {
                using (var fileStream = File.Create(kv.Value))
                {
                    using (var resource = thisAssembly.GetManifestResourceStream(kv.Key))
                    {
                        resource.CopyTo(fileStream);
                    }
                }
            }

            Reporter.Output.WriteLine($"Created new {languageName} project in {Directory.GetCurrentDirectory()}.");

            return 0;
        }

        public static int Run(string[] args)
        {
            DebugHelper.HandleDebugSwitch(ref args);

            var app = new CommandLineApplication();
            app.Name = "dotnet new";
            app.FullName = ".NET Initializer";
            app.Description = "Initializes empty project for .NET Platform";
            app.HelpOption("-h|--help");

            var lang = app.Option("-l|--lang <LANGUAGE>", "Language of project [C#|F#]", CommandOptionType.SingleValue);
            var type = app.Option("-t|--type <TYPE>", "Type of project", CommandOptionType.SingleValue);

            var dotnetNew = new NewCommand();
            app.OnExecute(() => {

                var csharp = new { Name = "C#", Alias = new[] { "c#", "cs", "csharp" }, TemplatePrefix = "CSharp", Templates = new[] { "Console" } };
                var fsharp = new { Name = "F#", Alias = new[] { "f#", "fs", "fsharp" }, TemplatePrefix = "FSharp", Templates = new[] { "Console" } };

                string languageValue = lang.Value() ?? csharp.Name;

                var language = new[] { csharp, fsharp }
                    .FirstOrDefault(l => l.Alias.Contains(languageValue, StringComparer.OrdinalIgnoreCase));

                if (language == null)
                {
                    Reporter.Error.WriteLine($"Unrecognized language: {languageValue}".Red());
                    return -1;
                }

                string typeValue = type.Value() ?? language.Templates.First();

                string templateName = language.Templates.FirstOrDefault(t => StringComparer.OrdinalIgnoreCase.Equals(typeValue, t));
                if (templateName == null)
                {
                    Reporter.Error.WriteLine($"Unrecognized type: {typeValue}".Red());
                    Reporter.Error.WriteLine($"Avaiable types for {language.Name} :".Red());
                    foreach (var t in language.Templates)
                    {
                        Reporter.Error.WriteLine($"- {t}".Red());
                    }
                    return -1;
                }

                string templateDir = $"{language.TemplatePrefix}_{templateName}";

                return dotnetNew.CreateEmptyProject(language.Name, templateDir);
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
