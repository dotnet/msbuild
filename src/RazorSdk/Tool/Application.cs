// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.NET.Sdk.Razor.Tool.CommandLineUtils;

namespace Microsoft.NET.Sdk.Razor.Tool
{
    internal class Application : CommandLineApplication
    {
        public Application(
            CancellationToken cancellationToken,
            ExtensionAssemblyLoader loader,
            ExtensionDependencyChecker checker,
            Func<string, MetadataReferenceProperties, PortableExecutableReference> assemblyReferenceProvider,
            TextWriter output = null,
            TextWriter error = null)
        {
            CancellationToken = cancellationToken;
            Checker = checker;
            Loader = loader;
            AssemblyReferenceProvider = assemblyReferenceProvider;
            Out = output ?? Out;
            Error = error ?? Error;

            Name = "rzc";
            FullName = "Microsoft ASP.NET Core Razor CLI tool";
            Description = "CLI interface to perform Razor operations.";
            ShortVersionGetter = GetInformationalVersion;

            HelpOption("-?|-h|--help");

            Commands.Add(new ServerCommand(this));
            Commands.Add(new ShutdownCommand(this));
            Commands.Add(new DiscoverCommand(this));
            Commands.Add(new GenerateCommand(this));
            Commands.Add(new RewriteCssCommand(this));
        }

        public CancellationToken CancellationToken { get; }

        public ExtensionAssemblyLoader Loader { get; }

        public ExtensionDependencyChecker Checker { get; }

        public Func<string, MetadataReferenceProperties, PortableExecutableReference> AssemblyReferenceProvider { get; }

        public new int Execute(params string[] args)
        {
            try
            {
                return base.Execute(ExpandResponseFiles(args));
            }
            catch (AggregateException ex) when (ex.InnerException != null)
            {
                foreach (var innerException in ex.Flatten().InnerExceptions)
                {
                    Error.WriteLine(innerException.Message);
                    Error.WriteLine(innerException.StackTrace);
                }
                return 1;
            }
            catch (CommandParsingException ex)
            {
                // Don't show a call stack when we have unneeded arguments, just print the error message.
                // The code that throws this exception will print help, so no need to do it here.
                Error.WriteLine(ex.Message);
                return 1;
            }
            catch (OperationCanceledException)
            {
                // This is a cancellation, not a failure.
                Error.WriteLine("Cancelled");
                return 1;
            }
            catch (Exception ex)
            {
                Error.WriteLine(ex.Message);
                Error.WriteLine(ex.StackTrace);
                return 1;
            }
        }

        private string GetInformationalVersion()
        {
            var assembly = typeof(Application).Assembly;
            var attribute = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>();
            return attribute.InformationalVersion;
        }

        private static string[] ExpandResponseFiles(string[] args)
        {
            var expandedArgs = new List<string>(args.Length);
            foreach (var arg in args)
            {
                if (!arg.StartsWith("@", StringComparison.Ordinal))
                {
                    expandedArgs.Add(arg);
                }
                else
                {
                    var fileName = arg.Substring(1);
                    expandedArgs.AddRange(File.ReadLines(fileName));
                }
            }

            return expandedArgs.ToArray();
        }
    }
}
