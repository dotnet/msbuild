// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Security.Cryptography;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Razor;
using Microsoft.CodeAnalysis.Razor.Serialization;
using Microsoft.NET.Sdk.Razor.Tool.CommandLineUtils;
using Newtonsoft.Json;

namespace Microsoft.NET.Sdk.Razor.Tool
{
    internal class DiscoverCommand : CommandBase
    {
        public DiscoverCommand(Application parent)
            : base(parent, "discover")
        {
            Assemblies = Argument("assemblies", "assemblies to search for tag helpers", multipleValues: true);
            TagHelperManifest = Option("-o", "output file", CommandOptionType.SingleValue);
            ProjectDirectory = Option("-p", "project root directory", CommandOptionType.SingleValue);
            Version = Option("-v|--version", "Razor language version", CommandOptionType.SingleValue);
            Configuration = Option("-c", "Razor configuration name", CommandOptionType.SingleValue);
            ExtensionNames = Option("-n", "extension name", CommandOptionType.MultipleValue);
            ExtensionFilePaths = Option("-e", "extension file path", CommandOptionType.MultipleValue);
        }

        public CommandArgument Assemblies { get; }

        public CommandOption TagHelperManifest { get; }

        public CommandOption ProjectDirectory { get; }

        public CommandOption Version { get; }

        public CommandOption Configuration { get; }

        public CommandOption ExtensionNames { get; }

        public CommandOption ExtensionFilePaths { get; }

        protected override bool ValidateArguments()
        {
            if (string.IsNullOrEmpty(TagHelperManifest.Value()))
            {
                Error.WriteLine($"{TagHelperManifest.Description} must be specified.");
                return false;
            }

            if (Assemblies.Values.Count == 0)
            {
                Error.WriteLine($"{Assemblies.Name} must have at least one value.");
                return false;
            }

            if (string.IsNullOrEmpty(ProjectDirectory.Value()))
            {
                ProjectDirectory.Values.Add(Environment.CurrentDirectory);
            }

            if (string.IsNullOrEmpty(Version.Value()))
            {
                Error.WriteLine($"{Version.Description} must be specified.");
                return false;
            }
            else if (!RazorLanguageVersion.TryParse(Version.Value(), out _))
            {
                Error.WriteLine($"Invalid option {Version.Value()} for Razor language version --version; must be Latest or a valid version in range {RazorLanguageVersion.Version_1_0} to {RazorLanguageVersion.Latest}.");
                return false;
            }

            if (string.IsNullOrEmpty(Configuration.Value()))
            {
                Error.WriteLine($"{Configuration.Description} must be specified.");
                return false;
            }

            if (ExtensionNames.Values.Count != ExtensionFilePaths.Values.Count)
            {
                Error.WriteLine($"{ExtensionNames.Description} and {ExtensionFilePaths.Description} should have the same number of values.");
            }

            foreach (var filePath in ExtensionFilePaths.Values)
            {
                if (!Path.IsPathRooted(filePath))
                {
                    Error.WriteLine($"Extension file paths must be fully-qualified, absolute paths.");
                    return false;
                }
            }

            PatchExtensions(ExtensionNames, ExtensionFilePaths, Error);

            return true;
        }

        /// <summary>
        /// Replaces the assembly for MVC extension v1 or v2 with the one shipped alongside SDK (as opposed to the one from NuGet).
        /// </summary>
        /// <remarks>
        /// Needed so the Razor compiler can change its APIs without breaking legacy MVC scenarios.
        /// </remarks>
        internal static void PatchExtensions(CommandOption extensionNames, CommandOption extensionFilePaths, TextWriter error)
        {
            string currentDirectory = null;

            for (int i = 0; i < extensionNames.Values.Count; i++)
            {
                var extensionName = extensionNames.Values[i];
                var replacementFileName = extensionName switch
                {
                    "MVC-1.0" or "MVC-1.1" => "Microsoft.AspNetCore.Mvc.Razor.Extensions.Version1_X.dll",
                    "MVC-2.0" or "MVC-2.1" => "Microsoft.AspNetCore.Mvc.Razor.Extensions.Version2_X.dll",
                    _ => null,
                };

                if (replacementFileName != null)
                {
                    var extensionFilePath = extensionFilePaths.Values[i];
                    if (!HasExpectedFileName(extensionFilePath))
                    {
                        error.WriteLine($"Extension '{extensionName}' has unexpected path '{extensionFilePath}'.");
                    }
                    else
                    {
                        currentDirectory ??= Path.GetDirectoryName(typeof(Application).Assembly.Location);
                        extensionFilePaths.Values[i] = Path.Combine(currentDirectory, replacementFileName);
                    }
                }
            }

            static bool HasExpectedFileName(string filePath)
            {
                return "Microsoft.AspNetCore.Mvc.Razor.Extensions".Equals(Path.GetFileNameWithoutExtension(filePath), StringComparison.OrdinalIgnoreCase);
            }
        }

        protected override Task<int> ExecuteCoreAsync()
        {
            if (!Parent.Checker.Check(ExtensionFilePaths.Values))
            {
                Error.WriteLine($"Extensions could not be loaded. See output for details.");
                return Task.FromResult(ExitCodeFailure);
            }

            // Loading all of the extensions should succeed as the dependency checker will have already
            // loaded them.
            var extensions = new RazorExtension[ExtensionNames.Values.Count];
            for (var i = 0; i < ExtensionNames.Values.Count; i++)
            {
                extensions[i] = new AssemblyExtension(ExtensionNames.Values[i], Parent.Loader.LoadFromPath(ExtensionFilePaths.Values[i]));
            }

            var version = RazorLanguageVersion.Parse(Version.Value());
            var configuration = RazorConfiguration.Create(version, Configuration.Value(), extensions);

            var result = ExecuteCore(
                configuration: configuration,
                projectDirectory: ProjectDirectory.Value(),
                outputFilePath: TagHelperManifest.Value(),
                assemblies: Assemblies.Values.ToArray());

            return Task.FromResult(result);
        }

        private int ExecuteCore(RazorConfiguration configuration, string projectDirectory, string outputFilePath, string[] assemblies)
        {
            outputFilePath = Path.Combine(projectDirectory, outputFilePath);

            var metadataReferences = new MetadataReference[assemblies.Length];
            for (var i = 0; i < assemblies.Length; i++)
            {
                metadataReferences[i] = Parent.AssemblyReferenceProvider(assemblies[i], default(MetadataReferenceProperties));
            }

            var engine = RazorProjectEngine.Create(configuration, RazorProjectFileSystem.Empty, b =>
            {
                b.Features.Add(new DefaultMetadataReferenceFeature() { References = metadataReferences });
                b.Features.Add(new CompilationTagHelperFeature());
                b.Features.Add(new DefaultTagHelperDescriptorProvider());

                CompilerFeatures.Register(b);
            });

            var feature = engine.Engine.Features.OfType<ITagHelperFeature>().Single();
            var tagHelpers = feature.GetDescriptors();

            using (var stream = new MemoryStream())
            {
                Serialize(stream, tagHelpers);

                stream.Position = 0;

                var newHash = Hash(stream);
                var existingHash = Hash(outputFilePath);

                if (!HashesEqual(newHash, existingHash))
                {
                    stream.Position = 0;
                    using (var output = File.Open(outputFilePath, FileMode.Create))
                    {
                        stream.CopyTo(output);
                    }
                }
            }

            return ExitCodeSuccess;
        }

        private static byte[] Hash(string path)
        {
            if (!File.Exists(path))
            {
                return Array.Empty<byte>();
            }

            using (var stream = File.OpenRead(path))
            {
                return Hash(stream);
            }
        }

        private static byte[] Hash(Stream stream)
        {
            using (var sha = SHA256.Create())
            {
                sha.ComputeHash(stream);
                return sha.Hash;
            }
        }

        private bool HashesEqual(byte[] x, byte[] y)
        {
            if (x.Length != y.Length)
            {
                return false;
            }

            for (var i = 0; i < x.Length; i++)
            {
                if (x[i] != y[i])
                {
                    return false;
                }
            }

            return true;
        }

        private static void Serialize(Stream stream, IReadOnlyList<TagHelperDescriptor> tagHelpers)
        {
            using (var writer = new StreamWriter(stream, Encoding.UTF8, bufferSize: 4096, leaveOpen: true))
            {
                var serializer = new JsonSerializer();
                serializer.Converters.Add(new TagHelperDescriptorJsonConverter());
                serializer.Converters.Add(new RazorDiagnosticJsonConverter());

                serializer.Serialize(writer, tagHelpers);
            }
        }
    }
}
