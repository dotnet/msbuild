using System;
using System.IO;
using System.Reflection;
using Microsoft.Extensions.CommandLineUtils;

namespace dotnet_new3
{
    internal class ResetCommand
    {
        internal static void Configure(CommandLineApplication app)
        {
            CommandOption help = app.Help();

            app.OnExecute(() =>
            {
                if (help.HasValue())
                {
                    app.ShowHelp();
                    return 0;
                }

                Assembly asm = Assembly.GetEntryAssembly();
                Uri codebase = new Uri(asm.CodeBase, UriKind.Absolute);
                string localPath = codebase.LocalPath;
                string dir = Path.GetDirectoryName(localPath);
                string manifest = Path.Combine(dir, "component_registry.json");
                string sources = Path.Combine(dir, "template_sources.json");

                if (File.Exists(manifest))
                {
                    File.Delete(manifest);
                }

                if (File.Exists(sources))
                {
                    File.Delete(sources);
                }

                return 0;
            });
        }
    }
}