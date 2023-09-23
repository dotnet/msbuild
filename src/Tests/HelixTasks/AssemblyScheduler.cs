// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

namespace Microsoft.DotNet.SdkCustomHelix.Sdk
{
    public readonly struct AssemblyPartitionInfo
    {
        internal readonly string AssemblyPath;
        internal readonly string DisplayName;
        internal readonly string ClassListArgumentString;

        public AssemblyPartitionInfo(
            string assemblyPath,
            string displayName,
            string classListArgumentString)
        {
            AssemblyPath = assemblyPath;
            DisplayName = displayName;
            ClassListArgumentString = classListArgumentString;
        }

        public AssemblyPartitionInfo(string assemblyPath)
        {
            AssemblyPath = assemblyPath;
            DisplayName = Path.GetFileName(assemblyPath);
            ClassListArgumentString = string.Empty;
        }

        public override string ToString() => DisplayName;
    }

    public sealed class AssemblyScheduler
    {
        /// <summary>
        /// This is a test class inserted into assemblies to guard against a .NET desktop bug.  The tests
        /// inside of it counteract the underlying issue.  If this test is included in any assembly it 
        /// must be added to every partition to ensure the work around is present
        /// 
        /// https://github.com/dotnet/corefx/issues/3793
        /// https://github.com/dotnet/roslyn/issues/8936
        /// </summary>
        private const string EventListenerGuardFullName = "Microsoft.CodeAnalysis.UnitTests.EventListenerGuard";

        private readonly struct TypeInfo
        {
            internal readonly string FullName;
            internal readonly int MethodCount;

            internal TypeInfo(string fullName, int methodCount)
            {
                FullName = fullName;
                MethodCount = methodCount;
            }
        }

        private readonly struct Partition
        {
            internal readonly string AssemblyPath;
            internal readonly int Id;
            internal readonly List<TypeInfo> TypeInfoList;

            internal Partition(string assemblyPath, int id, List<TypeInfo> typeInfoList)
            {
                AssemblyPath = assemblyPath;
                Id = id;
                TypeInfoList = typeInfoList;
            }
        }

        private sealed class AssemblyInfoBuilder
        {
            private readonly List<Partition> _partitionList = new();
            private readonly List<AssemblyPartitionInfo> _assemblyInfoList = new();
            private readonly StringBuilder _builder = new();
            private readonly string _assemblyPath;
            private readonly int _methodLimit;
            private readonly bool _hasEventListenerGuard;
            private readonly bool _netFramework;
            private int _currentId;
            private List<TypeInfo> _currentTypeInfoList = new();

            private AssemblyInfoBuilder(string assemblyPath, int methodLimit, bool hasEventListenerGuard, bool netFramework = false)
            {
                _assemblyPath = assemblyPath;
                _methodLimit = methodLimit;
                _hasEventListenerGuard = hasEventListenerGuard;
                _netFramework = netFramework;
            }

            internal static void Build(string assemblyPath, int methodLimit, List<TypeInfo> typeInfoList, out List<Partition> partitionList, out List<AssemblyPartitionInfo> assemblyInfoList, bool netFramework = false)
            {
                var hasEventListenerGuard = typeInfoList.Any(x => x.FullName == EventListenerGuardFullName);
                var builder = new AssemblyInfoBuilder(assemblyPath, methodLimit, hasEventListenerGuard, netFramework);
                builder.Build(typeInfoList);
                partitionList = builder._partitionList;
                assemblyInfoList = builder._assemblyInfoList;
            }

            private void Build(List<TypeInfo> typeInfoList)
            {
                BeginPartition();

                foreach (var typeInfo in typeInfoList)
                {
                    _currentTypeInfoList.Add(typeInfo);
                    if (_netFramework)
                    {
                        if (_builder.Length > 0)
                        {
                            _builder.Append("|");
                        }
                        _builder.Append($@"{typeInfo.FullName}");

                    }
                    else
                    {
                        _builder.Append($@"-class ""{typeInfo.FullName}"" ");
                    }
                    CheckForPartitionLimit(done: false);
                }

                CheckForPartitionLimit(done: true);
            }

            private void BeginPartition()
            {
                _currentId++;
                _currentTypeInfoList = new List<TypeInfo>();
                _builder.Length = 0;

                // Ensure the EventListenerGuard is in every partition.
                if (_hasEventListenerGuard)
                {
                    _builder.Append($@"-class ""{EventListenerGuardFullName}"" ");
                }
            }

            private void CheckForPartitionLimit(bool done)
            {
                if (done)
                {
                    // The builder is done looking at types.  If there are any TypeInfo that have not
                    // been added to a partition then do it now.
                    if (_currentTypeInfoList.Count > 0)
                    {
                        FinishPartition();
                    }

                    return;
                }

                // One item we have to consider here is the maximum command line length in 
                // Windows which is 32767 characters (XP is smaller but don't care).  Once
                // we get close then create a partition and move on. 
                if (_currentTypeInfoList.Sum(x => x.MethodCount) >= _methodLimit ||
                    _builder.Length > 25000)
                {
                    FinishPartition();
                    BeginPartition();
                }
            }

            private void FinishPartition()
            {
                var assemblyName = Path.GetFileName(_assemblyPath);
                var displayName = $"{assemblyName}.{_currentId}";
                var assemblyInfo = new AssemblyPartitionInfo(
                    _assemblyPath,
                    displayName,
                    _builder.ToString());

                _partitionList.Add(new Partition(_assemblyPath, _currentId, _currentTypeInfoList));
                _assemblyInfoList.Add(assemblyInfo);
            }
        }

        /// <summary>
        /// Default number of methods to include per partition.
        /// </summary>
        public const int DefaultMethodLimit = 2000;

        private readonly int _methodLimit;

        public AssemblyScheduler(int methodLimit = DefaultMethodLimit)
        {
            _methodLimit = methodLimit;
        }

        internal IEnumerable<AssemblyPartitionInfo> Schedule(IEnumerable<string> assemblyPaths)
        {
            var list = new List<AssemblyPartitionInfo>();
            foreach (var assemblyPath in assemblyPaths)
            {
                list.AddRange(Schedule(assemblyPath));
            }

            return list;
        }

        public IEnumerable<AssemblyPartitionInfo> Schedule(string assemblyPath, bool force = false, bool netFramework = false)
        {
            var typeInfoList = GetTypeInfoList(assemblyPath);
            var assemblyInfoList = new List<AssemblyPartitionInfo>();
            var partitionList = new List<Partition>();
            AssemblyInfoBuilder.Build(assemblyPath, _methodLimit, typeInfoList, out partitionList, out assemblyInfoList, netFramework);

            // If the scheduling didn't actually produce multiple partition then send back an unpartitioned
            // representation.
            if (assemblyInfoList.Count == 1 && !force)
            {
                // Logger.Log($"Assembly schedule produced a single partition {assemblyPath}");
                return new[] { CreateAssemblyInfo(assemblyPath) };
            }

            return assemblyInfoList;
        }

        public AssemblyPartitionInfo CreateAssemblyInfo(string assemblyPath)
        {
            return new AssemblyPartitionInfo(assemblyPath);
        }

        private static List<TypeInfo> GetTypeInfoList(string assemblyPath)
        {
            using (var stream = File.OpenRead(assemblyPath))
            using (var peReader = new PEReader(stream))
            {
                var metadataReader = peReader.GetMetadataReader();
                return GetTypeInfoList(metadataReader);
            }
        }

        private static List<TypeInfo> GetTypeInfoList(MetadataReader reader)
        {
            var list = new List<TypeInfo>();
            foreach (var handle in reader.TypeDefinitions)
            {
                var type = reader.GetTypeDefinition(handle);
                if (!IsValidIdentifier(reader, type.Name))
                {
                    continue;
                }

                var methodCount = GetMethodCount(reader, type);
                if (!ShouldIncludeType(reader, type, methodCount))
                {
                    continue;
                }

                var fullName = GetFullName(reader, type);
                list.Add(new TypeInfo(fullName, methodCount));
            }

            // Ensure we get classes back in a deterministic order.
            list.Sort((x, y) => x.FullName.CompareTo(y.FullName));
            return list;
        }

        /// <summary>
        /// Determine if this type should be one of the <c>class</c> values passed to xunit.  This
        /// code doesn't actually resolve base types or trace through inherrited Fact attributes
        /// hence we have to error on the side of including types with no tests vs. excluding them.
        /// </summary>
        private static bool ShouldIncludeType(MetadataReader reader, TypeDefinition type, int testMethodCount)
        {
            // xunit only handles public, non-abstract classes
            var isPublic =
                TypeAttributes.Public == (type.Attributes & TypeAttributes.Public) ||
                TypeAttributes.NestedPublic == (type.Attributes & TypeAttributes.NestedPublic);
            if (!isPublic ||
                TypeAttributes.Abstract == (type.Attributes & TypeAttributes.Abstract) ||
                TypeAttributes.Class != (type.Attributes & TypeAttributes.Class))
            {
                return false;
            }

            // Compiler generated types / methods have the shape of the heuristic that we are looking
            // at here.  Filter them out as well.
            if (!IsValidIdentifier(reader, type.Name))
            {
                return false;
            }

            if (testMethodCount > 0)
            {
                return true;
            }

            // The case we still have to consider at this point is a class with 0 defined methods, 
            // inheritting from a class with > 0 defined test methods.  That is a completely valid
            // xunit scenario.  For now we're just going to exclude types that inherit from object
            // because they clearly don't fit that category.
            return !(InheritsFromObject(reader, type) ?? false);
        }

        private static int GetMethodCount(MetadataReader reader, TypeDefinition type)
        {
            var count = 0;
            foreach (var handle in type.GetMethods())
            {
                var methodDefinition = reader.GetMethodDefinition(handle);
                if (methodDefinition.GetCustomAttributes().Count == 0 ||
                    !IsValidIdentifier(reader, methodDefinition.Name))
                {
                    continue;
                }

                if (MethodAttributes.Public != (methodDefinition.Attributes & MethodAttributes.Public))
                {
                    continue;
                }

                count++;
            }

            return count;
        }

        private static bool IsValidIdentifier(MetadataReader reader, StringHandle handle)
        {
            var name = reader.GetString(handle);
            for (int i = 0; i < name.Length; i++)
            {
                switch (name[i])
                {
                    case '<':
                    case '>':
                    case '$':
                        return false;
                }
            }

            return true;
        }

        private static bool? InheritsFromObject(MetadataReader reader, TypeDefinition type)
        {
            if (type.BaseType.Kind != HandleKind.TypeReference)
            {
                return null;
            }

            var typeRef = reader.GetTypeReference((TypeReferenceHandle)type.BaseType);
            return
                reader.GetString(typeRef.Namespace) == "System" &&
                reader.GetString(typeRef.Name) == "Object";
        }

        private static string GetFullName(MetadataReader reader, TypeDefinition type)
        {
            var typeName = reader.GetString(type.Name);

            if (TypeAttributes.NestedPublic == (type.Attributes & TypeAttributes.NestedPublic))
            {
                // Need to take into account the containing type.
                var declaringType = reader.GetTypeDefinition(type.GetDeclaringType());
                var declaringTypeFullName = GetFullName(reader, declaringType);
                return $"{declaringTypeFullName}+{typeName}";
            }

            var namespaceName = reader.GetString(type.Namespace);
            if (string.IsNullOrEmpty(namespaceName))
            {
                return typeName;
            }

            return $"{namespaceName}.{typeName}";
        }
    }
}
