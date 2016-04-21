using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.Extensions.CommandLineUtils;
using Mutant.Chicken.Abstractions;

namespace dotnet_new3
{
    internal class ListComponentCommand
    {
        internal static void Configure(CommandLineApplication app)
        {
            app.OnExecute(() =>
            {
                bool anyWritten = false;
                foreach (IComponent component in Program.Broker.ComponentRegistry.OfType<ITemplateSource>())
                {
                    anyWritten = true;
                    Reporter.Output.WriteLine($"{component.Name} - Template Source - {component.GetType().AssemblyQualifiedName}");
                }

                foreach (IComponent component in Program.Broker.ComponentRegistry.OfType<IGenerator>())
                {
                    anyWritten = true;
                    Reporter.Output.WriteLine($"{component.Name} - Generator - {component.GetType().AssemblyQualifiedName}");
                }

                if (!anyWritten)
                {
                    Reporter.Output.WriteLine("(No components installed)");
                }

                return 0;
            });

        }
    }

    internal class AddComponentCommand
    {
        internal static void Configure(CommandLineApplication app)
        {
            var assemblyName = app.Argument("assembly", "The assembly containing components to add.");

            app.OnExecute(() =>
            {
                Assembly asm = null;
                try
                {
                    AssemblyName name = new AssemblyName(assemblyName.Value);
                    asm = Assembly.Load(name);
                }
                catch
                {
                }

                if(asm == null)
                {
                    Assembly entry = Assembly.GetEntryAssembly();
                    Uri codebase = new Uri(entry.CodeBase, UriKind.Absolute);
                    string localPath = codebase.LocalPath;
                    string dir = Path.GetDirectoryName(localPath);
                    string sourceFile = assemblyName.Value;
                    if (!Path.IsPathRooted(sourceFile))
                    {
                        sourceFile = Path.Combine(Directory.GetCurrentDirectory(), sourceFile);
                    }

                    string file = Path.GetFileName(sourceFile);
                    File.Copy(sourceFile, Path.Combine(dir, file), true);
                    asm = Assembly.Load(new AssemblyName(file));
                }

                foreach (Type type in asm.ExportedTypes)
                {
                    if (typeof(ITemplateSource).IsAssignableFrom(type))
                    {
                        Program.Broker.ComponentRegistry.Register<ITemplateSource>(type); 
                    }

                    if (typeof(IGenerator).IsAssignableFrom(type))
                    {
                        Program.Broker.ComponentRegistry.Register<IGenerator>(type);
                    }
                }

                return 0;
            });
        }
    }
}