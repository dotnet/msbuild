using System;
using System.Configuration.Assemblies;
using System.Reflection;

using Bond.Tag;

namespace Microsoft.Build.Tasks
{
    internal sealed partial class SystemState
    {
        [Bond.Schema]
        private class SystemStateCachePayload : StateFileCachePayload
        {
            [Bond.Id(0), Bond.Type(typeof(nullable<FileStatePayload[]>))]
            internal FileStatePayload[] InstanceLocalFileStateCache { get; set; }
        }

        [Bond.Schema]
        private class FileStatePayload
        {
            [Bond.Id(0), Bond.Type(typeof(nullable<string>))]
            internal string Path { get; set; }

            [Bond.Id(1)]
            internal DateTimePayload LastModified { get; set; }

            [Bond.Id(2), Bond.Type(typeof(nullable<AssemblyNameExtensionPayload>))]
            internal AssemblyNameExtensionPayload Assembly { get; set; }

            [Bond.Id(3), Bond.Type(typeof(nullable<AssemblyNameExtensionPayload[]>))]
            internal AssemblyNameExtensionPayload[] Dependencies { get; set; }

            [Bond.Id(4), Bond.Type(typeof(nullable<string[]>))]
            internal string[] ScatterFiles { get; set; }

            [Bond.Id(5), Bond.Type(typeof(nullable<string>))]
            internal string RuntimeVersion { get; set; }

            [Bond.Id(6), Bond.Type(typeof(nullable<FrameworkNamePayload>))]
            internal FrameworkNamePayload FrameworkName { get; set; }
        }

        [Bond.Schema]
        private class DateTimePayload
        {
            [Bond.Id(0)]
            internal long Ticks { get; set; }

            [Bond.Id(1)]
            internal DateTimeKind Kind { get; set; }
        }

        [Bond.Schema]
        private class AssemblyNameExtensionPayload
        {
            [Bond.Id(0), Bond.Type(typeof(nullable<AssemblyNamePayload>))]
            internal AssemblyNamePayload AsAssemblyName { get; set; }

            [Bond.Id(1), Bond.Type(typeof(nullable<string>))]
            internal string AsString { get; set; }

            [Bond.Id(2)]
            internal bool IsSimpleName { get; set; }

            [Bond.Id(3)]
            internal bool HasProcessorArchitectureInFusionName { get; set; }

            [Bond.Id(4)]
            internal bool Immutable { get; set; }

            [Bond.Id(5), Bond.Type(typeof(nullable<AssemblyNameExtensionPayload[]>))]
            internal AssemblyNameExtensionPayload[] RemappedFrom { get; set; }
        }

        [Bond.Schema]
        private class AssemblyNamePayload
        {
            [Bond.Id(0), Bond.Type(typeof(nullable<string>))]
            internal string Name { get; set; }

            [Bond.Id(1), Bond.Type(typeof(nullable<byte[]>))]
            internal byte[] PublicKey { get; set; }

            [Bond.Id(2), Bond.Type(typeof(nullable<byte[]>))]
            internal byte[] PublicKeyToken { get; set; }

            [Bond.Id(3), Bond.Type(typeof(nullable<VersionPayload>))]
            internal VersionPayload Version { get; set; }

            [Bond.Id(4)]
            internal AssemblyNameFlags Flags { get; set; }

            [Bond.Id(5)]
            internal ProcessorArchitecture ProcessorArchitecture { get; set; }

            [Bond.Id(6), Bond.Type(typeof(nullable<CultureInfoPayload>))]
            internal CultureInfoPayload CultureInfo { get; set; }

            [Bond.Id(7)]
            internal System.Configuration.Assemblies.AssemblyHashAlgorithm HashAlgorithm { get; set; }

            [Bond.Id(8)]
            internal AssemblyVersionCompatibility VersionCompatibility { get; set; }

            [Bond.Id(9), Bond.Type(typeof(nullable<string>))]
            internal string CodeBase { get; set; }

            [Bond.Id(10), Bond.Type(typeof(nullable<StrongNameKeyPairPayload>))]
            internal StrongNameKeyPairPayload KeyPair { get; set; }
        }

        [Bond.Schema]
        private class FrameworkNamePayload
        {
            [Bond.Id(0), Bond.Type(typeof(nullable<string>))]
            internal string Version { get; set; }

            [Bond.Id(1), Bond.Type(typeof(nullable<string>))]
            internal string Identifier { get; set; }

            [Bond.Id(2), Bond.Type(typeof(nullable<string>))]
            internal string Profile { get; set; }
        }


        [Bond.Schema]
        private class CultureInfoPayload
        {
            [Bond.Id(0)]
            internal int LCID { get; set; }
        }

        [Bond.Schema]
        private class VersionPayload
        {
            [Bond.Id(0)]
            internal int Major;

            [Bond.Id(1)]
            internal int Minor;

            [Bond.Id(2)]
            internal int Build;

            [Bond.Id(3)]
            internal int Revision;
        }

        [Bond.Schema]
        private class StrongNameKeyPairPayload
        {
            [Bond.Id(0), Bond.Type(typeof(nullable<byte[]>))]
            internal byte[] PublicKey { get; set; }
        }
    }
}
