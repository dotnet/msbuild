// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using Microsoft.CodeAnalysis;

namespace Microsoft.Build.TaskAuthoring.Analyzer
{
    /// <summary>
    /// Extends the call graph into referenced assemblies by reading IL from PE files.
    /// When the transitive analyzer's BFS encounters a method with no source code
    /// (i.e., defined in a referenced assembly), this class reads the method's IL
    /// to discover outgoing call edges and banned API violations.
    /// </summary>
    internal sealed class ILCallGraphExtender : IDisposable
    {
        /// <summary>
        /// Types whose internal method implementations are considered safe.
        /// Violations found inside these types are suppressed because they handle
        /// paths safely internally (e.g., AbsolutePath.GetCanonicalForm calls Path.GetFullPath
        /// on an already-absolute path).
        /// </summary>
        private static readonly HashSet<string> SafeTypeMetadataNames = new HashSet<string>(StringComparer.Ordinal)
        {
            "Microsoft.Build.Framework.AbsolutePath",
            "Microsoft.Build.Framework.TaskEnvironment",
            "Microsoft.Build.BackEnd.TaskExecutionHost.MultiThreadedTaskEnvironmentDriver",
            "Microsoft.Build.BackEnd.TaskExecutionHost.MultiProcessTaskEnvironmentDriver",
        };

        /// <summary>
        /// Assembly name prefixes for BCL assemblies where we stop IL traversal.
        /// </summary>
        private static readonly string[] BclPrefixes =
        {
            "System.",
            "System,",
            "mscorlib",
            "netstandard",
            "Microsoft.CSharp",
            "Microsoft.VisualBasic",
            "Microsoft.Win32",
            "WindowsBase",
        };

        private readonly Compilation _compilation;

        /// <summary>
        /// Cache of PEReader instances keyed by assembly identity.
        /// </summary>
        private readonly ConcurrentDictionary<string, PEReaderEntry?> _peReaderCache
            = new ConcurrentDictionary<string, PEReaderEntry?>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Set of methods already analyzed via IL to avoid redundant work.
        /// Keyed by a string representation: "TypeMetadataName.MethodName(ParamTypes)".
        /// </summary>
        private readonly ConcurrentDictionary<string, bool> _analyzedMethods
            = new ConcurrentDictionary<string, bool>(StringComparer.Ordinal);

        /// <summary>
        /// Maps assembly name to its IAssemblySymbol for symbol resolution.
        /// </summary>
        private readonly Dictionary<string, IAssemblySymbol> _assemblySymbolMap;

        public ILCallGraphExtender(Compilation compilation)
        {
            _compilation = compilation;
            _assemblySymbolMap = BuildAssemblySymbolMap(compilation);
        }

        /// <summary>
        /// Determines whether a method is in a referenced assembly (no source code).
        /// </summary>
        public static bool IsExternalMethod(IMethodSymbol method)
        {
            return method.DeclaringSyntaxReferences.IsEmpty;
        }

        /// <summary>
        /// Determines whether the containing type of a method is on the safe-type list.
        /// </summary>
        public static bool IsInSafeType(IMethodSymbol method)
        {
            var containingType = method.ContainingType;
            if (containingType is null)
            {
                return false;
            }

            string metadataName = GetTypeMetadataName(containingType);
            return SafeTypeMetadataNames.Contains(metadataName);
        }

        /// <summary>
        /// Determines whether a method is in a BCL assembly where we should stop traversal.
        /// </summary>
        public static bool IsInBclAssembly(IMethodSymbol method)
        {
            var assembly = method.ContainingAssembly;
            if (assembly is null)
            {
                return true; // treat unknown as BCL to be safe
            }

            return IsAssemblyBcl(assembly.Identity.Name);
        }

        /// <summary>
        /// Analyzes a method's IL to discover outgoing call edges.
        /// Returns a list of (callee IMethodSymbol, isViolation, violationInfo) tuples.
        /// </summary>
        public List<ILCallTarget> GetCallTargets(IMethodSymbol method)
        {
            var results = new List<ILCallTarget>();

            var methodKey = GetMethodKey(method);
            if (!_analyzedMethods.TryAdd(methodKey, true))
            {
                return results; // already analyzed
            }

            var containingType = method.ContainingType;
            if (containingType is null)
            {
                return results;
            }

            var assembly = containingType.ContainingAssembly;
            if (assembly is null)
            {
                return results;
            }

            var peEntry = GetOrLoadPEReader(assembly);
            if (peEntry is null)
            {
                return results;
            }

            var reader = peEntry.MetadataReader;

            // Find the MethodDefinition for this IMethodSymbol
            var methodDef = FindMethodDefinition(reader, method);
            if (methodDef is null)
            {
                return results;
            }

            // Read IL body
            var methodBody = GetMethodBody(peEntry.PEReader, reader, methodDef.Value);
            if (methodBody is null)
            {
                return results;
            }

            // Parse IL opcodes to find call targets
            ParseILForCalls(peEntry, reader, methodBody, assembly, results);

            return results;
        }

        private void ParseILForCalls(
            PEReaderEntry peEntry,
            MetadataReader reader,
            MethodBodyBlock methodBody,
            IAssemblySymbol containingAssembly,
            List<ILCallTarget> results)
        {
            var ilBytes = methodBody.GetILBytes();
            if (ilBytes is null || ilBytes.Length == 0)
            {
                return;
            }

            int offset = 0;
            while (offset < ilBytes.Length)
            {
                int opcodeStart = offset;
                ushort opcodeValue;

                byte first = ilBytes[offset++];
                if (first == 0xFE && offset < ilBytes.Length)
                {
                    byte second = ilBytes[offset++];
                    opcodeValue = (ushort)(0xFE00 | second);
                }
                else
                {
                    opcodeValue = first;
                }

                // Determine operand size based on opcode
                int operandSize = GetOperandSize(opcodeValue, ilBytes, offset);
                bool isCall = opcodeValue == 0x28   // call
                           || opcodeValue == 0x6F   // callvirt
                           || opcodeValue == 0x73   // newobj
                           || opcodeValue == 0xFE06 // ldftn
                           || opcodeValue == 0xFE07; // ldvirtftn

                if (isCall && operandSize == 4 && offset + 4 <= ilBytes.Length)
                {
                    int token = ilBytes[offset]
                        | (ilBytes[offset + 1] << 8)
                        | (ilBytes[offset + 2] << 16)
                        | (ilBytes[offset + 3] << 24);

                    try
                    {
                        var handle = MetadataTokens.EntityHandle(token);
                        var resolved = ResolveCallTarget(reader, handle, containingAssembly);
                        if (resolved is not null)
                        {
                            results.Add(resolved);
                        }
                    }
                    catch
                    {
                        // Invalid token — skip
                    }
                }

                offset += operandSize;

                // Safety: prevent infinite loops from bad IL
                if (offset <= opcodeStart)
                {
                    break;
                }
            }
        }

        /// <summary>
        /// Gets the operand size for an IL opcode.
        /// Uses ECMA-335 Partition III opcode definitions.
        /// </summary>
        private static int GetOperandSize(ushort opcode, byte[] ilBytes, int offset)
        {
            // Switch table (InlineSwitch) — variable length
            if (opcode == 0x45)
            {
                if (offset + 4 > ilBytes.Length)
                {
                    return 0;
                }

                int count = ilBytes[offset]
                    | (ilBytes[offset + 1] << 8)
                    | (ilBytes[offset + 2] << 16)
                    | (ilBytes[offset + 3] << 24);
                return 4 + count * 4;
            }

            // Single-byte opcodes
            if (opcode <= 0xFF)
            {
                return GetSingleByteOperandSize((byte)opcode);
            }

            // Two-byte opcodes (0xFE prefix)
            return GetFEPrefixOperandSize((byte)(opcode & 0xFF));
        }

        /// <summary>
        /// Resolves a metadata token from an IL call instruction to an IMethodSymbol.
        /// </summary>
        private ILCallTarget? ResolveCallTarget(
            MetadataReader reader,
            EntityHandle handle,
            IAssemblySymbol containingAssembly)
        {
            string? typeName = null;
            string? methodName = null;
            string? assemblyName = null;
            int paramCount = -1; // -1 means unknown

            switch (handle.Kind)
            {
                case HandleKind.MethodDefinition:
                {
                    var methodDef = reader.GetMethodDefinition((MethodDefinitionHandle)handle);
                    methodName = reader.GetString(methodDef.Name);
                    var declaringType = reader.GetTypeDefinition(methodDef.GetDeclaringType());
                    typeName = GetFullTypeName(reader, declaringType);
                    assemblyName = containingAssembly.Identity.Name;
                    // Count parameters from PE metadata
                    paramCount = CountMethodDefParams(reader, methodDef);
                    break;
                }

                case HandleKind.MemberReference:
                {
                    var memberRef = reader.GetMemberReference((MemberReferenceHandle)handle);
                    methodName = reader.GetString(memberRef.Name);
                    (typeName, assemblyName) = ResolveMemberRefParent(reader, memberRef.Parent, containingAssembly);
                    // Extract parameter count from MemberRef signature blob
                    paramCount = GetParamCountFromSignature(reader, memberRef.Signature);
                    break;
                }

                case HandleKind.MethodSpecification:
                {
                    var methodSpec = reader.GetMethodSpecification((MethodSpecificationHandle)handle);
                    // Recurse into the underlying method
                    return ResolveCallTarget(reader, methodSpec.Method, containingAssembly);
                }

                default:
                    return null;
            }

            if (typeName is null || methodName is null)
            {
                return null;
            }

            // Resolve to IMethodSymbol via the compilation
            var resolvedSymbol = ResolveToMethodSymbol(typeName, methodName, assemblyName, paramCount);
            if (resolvedSymbol is null)
            {
                return null;
            }

            return new ILCallTarget(resolvedSymbol);
        }

        /// <summary>
        /// Counts the parameters of a MethodDefinition, excluding the return type pseudo-parameter.
        /// </summary>
        private static int CountMethodDefParams(MetadataReader reader, MethodDefinition methodDef)
        {
            // Read param count from the method signature blob to avoid the return-type
            // pseudo-parameter issue in GetParameters()
            return GetParamCountFromSignature(reader, methodDef.Signature);
        }

        /// <summary>
        /// Extracts the parameter count from a method signature blob (ECMA-335 II.23.2.1/II.23.2.2).
        /// Format: CallingConvention GenParamCount? ParamCount RetType Param*
        /// </summary>
        private static int GetParamCountFromSignature(MetadataReader reader, BlobHandle signatureHandle)
        {
            try
            {
                var blobReader = reader.GetBlobReader(signatureHandle);
                byte callingConvention = blobReader.ReadByte();
                // Check for generic method — if so, skip GenParamCount
                if ((callingConvention & 0x10) != 0) // IMAGE_CEE_CS_CALLCONV_GENERIC
                {
                    blobReader.ReadCompressedInteger(); // GenParamCount
                }
                return blobReader.ReadCompressedInteger(); // ParamCount
            }
            catch
            {
                return -1;
            }
        }

        private (string? typeName, string? assemblyName) ResolveMemberRefParent(
            MetadataReader reader,
            EntityHandle parent,
            IAssemblySymbol containingAssembly)
        {
            switch (parent.Kind)
            {
                case HandleKind.TypeReference:
                {
                    var typeRef = reader.GetTypeReference((TypeReferenceHandle)parent);
                    string ns = reader.GetString(typeRef.Namespace);
                    string name = reader.GetString(typeRef.Name);
                    string typeName = string.IsNullOrEmpty(ns) ? name : $"{ns}.{name}";

                    // Resolve the assembly from ResolutionScope
                    string? asmName = ResolveAssemblyFromScope(reader, typeRef.ResolutionScope, containingAssembly);
                    return (typeName, asmName);
                }

                case HandleKind.TypeDefinition:
                {
                    var typeDef = reader.GetTypeDefinition((TypeDefinitionHandle)parent);
                    string typeName = GetFullTypeName(reader, typeDef);
                    return (typeName, containingAssembly.Identity.Name);
                }

                case HandleKind.TypeSpecification:
                {
                    // Generic type instantiation — try to resolve the underlying type
                    var typeSpec = reader.GetTypeSpecification((TypeSpecificationHandle)parent);
                    // For simplicity, try to decode the blob to get the base type
                    // This is complex; skip for now
                    return (null, null);
                }

                default:
                    return (null, null);
            }
        }

        private string? ResolveAssemblyFromScope(
            MetadataReader reader,
            EntityHandle scope,
            IAssemblySymbol containingAssembly)
        {
            switch (scope.Kind)
            {
                case HandleKind.AssemblyReference:
                {
                    var asmRef = reader.GetAssemblyReference((AssemblyReferenceHandle)scope);
                    return reader.GetString(asmRef.Name);
                }

                case HandleKind.ModuleDefinition:
                    return containingAssembly.Identity.Name;

                case HandleKind.TypeReference:
                {
                    // Nested type — resolve parent
                    var parentRef = reader.GetTypeReference((TypeReferenceHandle)scope);
                    return ResolveAssemblyFromScope(reader, parentRef.ResolutionScope, containingAssembly);
                }

                default:
                    return containingAssembly.Identity.Name;
            }
        }

        /// <summary>
        /// Resolves a type+method name to an IMethodSymbol via the Compilation.
        /// </summary>
        private IMethodSymbol? ResolveToMethodSymbol(string typeName, string methodName, string? assemblyName, int paramCount = -1)
        {
            // First try to find the type in the compilation
            var type = _compilation.GetTypeByMetadataName(typeName);

            // If not found and we have an assembly name, try assembly-specific lookup
            if (type is null && assemblyName is not null)
            {
                if (_assemblySymbolMap.TryGetValue(assemblyName, out var asmSymbol))
                {
                    type = asmSymbol.GetTypeByMetadataName(typeName);
                }
            }

            if (type is null)
            {
                return null;
            }

            // Find the method by name, using parameter count for disambiguation when available
            IMethodSymbol? firstMatch = null;
            foreach (var member in type.GetMembers(methodName))
            {
                if (member is IMethodSymbol method)
                {
                    if (paramCount >= 0 && method.Parameters.Length == paramCount)
                    {
                        return method.OriginalDefinition; // exact param count match
                    }

                    firstMatch ??= method;
                }
            }

            // If no exact param match, return first match (fallback)
            if (firstMatch is not null)
            {
                return firstMatch.OriginalDefinition;
            }

            // Check for constructors
            if (methodName == ".ctor" || methodName == ".cctor")
            {
                // Prefer constructors with path-like string parameters
                IMethodSymbol? bestMatch = null;
                foreach (var ctor in type.Constructors)
                {
                    if (bestMatch is null)
                    {
                        bestMatch = ctor;
                    }

                    // Check if this constructor has a string parameter with a path-like name
                    foreach (var param in ctor.Parameters)
                    {
                        if (param.Type.SpecialType == SpecialType.System_String &&
                            IsPathParameterName(param.Name))
                        {
                            return ctor.OriginalDefinition;
                        }
                    }
                }

                return bestMatch?.OriginalDefinition;
            }

            // Check for property getters/setters
            foreach (var member in type.GetMembers())
            {
                if (member is IPropertySymbol prop)
                {
                    if (prop.GetMethod?.Name == methodName)
                    {
                        return prop.GetMethod.OriginalDefinition;
                    }

                    if (prop.SetMethod?.Name == methodName)
                    {
                        return prop.SetMethod.OriginalDefinition;
                    }
                }
            }

            return null;
        }

        #region PE Reader Management

        private PEReaderEntry? GetOrLoadPEReader(IAssemblySymbol assembly)
        {
            string name = assembly.Identity.Name;

            if (IsAssemblyBcl(name))
            {
                return null;
            }

            return _peReaderCache.GetOrAdd(name, _ => LoadPEReader(assembly));
        }

        private PEReaderEntry? LoadPEReader(IAssemblySymbol assembly)
        {
            // Find the PortableExecutableReference for this assembly
            foreach (var reference in _compilation.References)
            {
                if (reference is PortableExecutableReference peRef)
                {
                    var refSymbol = _compilation.GetAssemblyOrModuleSymbol(peRef);
                    if (refSymbol is IAssemblySymbol asmSymbol &&
                        string.Equals(asmSymbol.Identity.Name, assembly.Identity.Name, StringComparison.OrdinalIgnoreCase))
                    {
                        string? filePath = peRef.FilePath;
                        if (filePath is null)
                        {
                            continue;
                        }

                        try
                        {
                            // Use FileStream to read PE data — this is necessary for IL analysis
                            // and is the only way to get method bodies from referenced assemblies.
#pragma warning disable RS1035 // File IO is required here to read PE metadata for cross-assembly IL analysis
                            var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
#pragma warning restore RS1035
                            var peReader = new PEReader(stream);
                            if (peReader.HasMetadata)
                            {
                                var metadataReader = peReader.GetMetadataReader();
                                return new PEReaderEntry(peReader, metadataReader, stream);
                            }

                            peReader.Dispose();
                            stream.Dispose();
                        }
                        catch
                        {
                            // Silently skip assemblies we can't read
                        }
                    }
                }
            }

            return null;
        }

        #endregion

        #region MethodDefinition Lookup

        /// <summary>
        /// Finds the MethodDefinition in the PE metadata that corresponds to the given IMethodSymbol.
        /// </summary>
        private MethodDefinitionHandle? FindMethodDefinition(MetadataReader reader, IMethodSymbol method)
        {
            var containingType = method.ContainingType;
            if (containingType is null)
            {
                return null;
            }

            string typeMetadataName = GetTypeMetadataName(containingType);

            // Search all type definitions for a matching type
            foreach (var typeDefHandle in reader.TypeDefinitions)
            {
                var typeDef = reader.GetTypeDefinition(typeDefHandle);
                string fullName = GetFullTypeName(reader, typeDef);

                if (!string.Equals(fullName, typeMetadataName, StringComparison.Ordinal))
                {
                    continue;
                }

                // Found the type — now find the method
                foreach (var methodDefHandle in typeDef.GetMethods())
                {
                    var methodDef = reader.GetMethodDefinition(methodDefHandle);
                    string name = reader.GetString(methodDef.Name);

                    if (string.Equals(name, method.Name, StringComparison.Ordinal) ||
                        (method.MethodKind == MethodKind.Constructor && name == ".ctor") ||
                        (method.MethodKind == MethodKind.StaticConstructor && name == ".cctor"))
                    {
                        // Match parameter count for basic overload disambiguation
                        var paramHandles = methodDef.GetParameters();
                        int paramCount = 0;
                        foreach (var _ in paramHandles)
                        {
                            paramCount++;
                        }

                        if (paramCount == method.Parameters.Length ||
                            paramCount == method.Parameters.Length + 1) // +1 for return type pseudo-param
                        {
                            return methodDefHandle;
                        }
                    }
                }

                // If we found the type but no exact method match, try without param count check
                foreach (var methodDefHandle in typeDef.GetMethods())
                {
                    var methodDef = reader.GetMethodDefinition(methodDefHandle);
                    string name = reader.GetString(methodDef.Name);

                    if (string.Equals(name, method.Name, StringComparison.Ordinal) ||
                        (method.MethodKind == MethodKind.Constructor && name == ".ctor"))
                    {
                        return methodDefHandle;
                    }
                }
            }

            return null;
        }

        private static MethodBodyBlock? GetMethodBody(PEReader peReader, MetadataReader reader, MethodDefinitionHandle methodDef)
        {
            var def = reader.GetMethodDefinition(methodDef);
            if (def.RelativeVirtualAddress == 0)
            {
                return null; // abstract/extern/interface method
            }

            try
            {
                return peReader.GetMethodBody(def.RelativeVirtualAddress);
            }
            catch
            {
                return null;
            }
        }

        #endregion

        #region Helpers

        private static bool IsPathParameterName(string paramName)
        {
            return paramName.IndexOf("path", StringComparison.OrdinalIgnoreCase) >= 0
                || paramName.IndexOf("file", StringComparison.OrdinalIgnoreCase) >= 0
                || paramName.IndexOf("dir", StringComparison.OrdinalIgnoreCase) >= 0
                || paramName.IndexOf("folder", StringComparison.OrdinalIgnoreCase) >= 0
                || paramName.IndexOf("source", StringComparison.OrdinalIgnoreCase) >= 0
                || paramName.IndexOf("dest", StringComparison.OrdinalIgnoreCase) >= 0
                || paramName.IndexOf("uri", StringComparison.OrdinalIgnoreCase) >= 0
                || paramName.IndexOf("url", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string GetFullTypeName(MetadataReader reader, TypeDefinition typeDef)
        {
            string name = reader.GetString(typeDef.Name);
            string ns = reader.GetString(typeDef.Namespace);

            // Check for nested type
            if (typeDef.IsNested)
            {
                var declaringTypeHandle = typeDef.GetDeclaringType();
                var declaringType = reader.GetTypeDefinition(declaringTypeHandle);
                string parentName = GetFullTypeName(reader, declaringType);
                return $"{parentName}+{name}";
            }

            return string.IsNullOrEmpty(ns) ? name : $"{ns}.{name}";
        }

        private static string GetTypeMetadataName(INamedTypeSymbol type)
        {
            var parts = new List<string>();
            var current = type;

            while (current is not null)
            {
                parts.Add(current.MetadataName);
                if (current.ContainingType is not null)
                {
                    current = current.ContainingType;
                }
                else
                {
                    break;
                }
            }

            // Build namespace prefix
            var ns = type.ContainingNamespace;
            var nsParts = new List<string>();
            while (ns is not null && !ns.IsGlobalNamespace)
            {
                nsParts.Add(ns.Name);
                ns = ns.ContainingNamespace;
            }

            nsParts.Reverse();
            parts.Reverse();

            string nsPrefix = nsParts.Count > 0 ? string.Join(".", nsParts) + "." : "";
            return nsPrefix + string.Join("+", parts);
        }

        private static string GetMethodKey(IMethodSymbol method)
        {
            return $"{GetTypeMetadataName(method.ContainingType)}.{method.Name}({method.Parameters.Length})";
        }

        private static bool IsAssemblyBcl(string assemblyName)
        {
            foreach (var prefix in BclPrefixes)
            {
                if (assemblyName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            // Also skip runtime assemblies
            if (assemblyName.Equals("System", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return false;
        }

        private static Dictionary<string, IAssemblySymbol> BuildAssemblySymbolMap(Compilation compilation)
        {
            var map = new Dictionary<string, IAssemblySymbol>(StringComparer.OrdinalIgnoreCase);

            foreach (var reference in compilation.References)
            {
                var symbol = compilation.GetAssemblyOrModuleSymbol(reference);
                if (symbol is IAssemblySymbol asm)
                {
                    map[asm.Identity.Name] = asm;
                }
            }

            return map;
        }

        /// <summary>
        /// Returns the operand size for a single-byte opcode (excluding 0xFE prefix).
        /// Based on ECMA-335 Partition III, Table III.1.
        /// </summary>
        private static int GetSingleByteOperandSize(byte opcode)
        {
            // Organized by operand type for clarity and correctness
            switch (opcode)
            {
                // InlineNone (0 bytes): no operand
                case 0x00: // nop
                case 0x01: // break
                case 0x02: case 0x03: case 0x04: case 0x05: // ldarg.0-3
                case 0x06: case 0x07: case 0x08: case 0x09: // ldloc.0-3
                case 0x0A: case 0x0B: case 0x0C: case 0x0D: // stloc.0-3
                case 0x14: // ldnull
                case 0x15: case 0x16: case 0x17: case 0x18: // ldc.i4.m1, ldc.i4.0-2
                case 0x19: case 0x1A: case 0x1B: case 0x1C: // ldc.i4.3-6
                case 0x1D: case 0x1E: // ldc.i4.7, ldc.i4.8
                case 0x25: // dup
                case 0x26: // pop
                case 0x2A: // ret
                case 0x46: case 0x47: case 0x48: case 0x49: // ldind.i1..u2
                case 0x4A: case 0x4B: case 0x4C: case 0x4D: // ldind.u2..r4
                case 0x4E: case 0x4F: case 0x50: // ldind.r8, ldind.ref, stind.ref
                case 0x51: case 0x52: case 0x53: case 0x54: // stind.i1..r4
                case 0x55: case 0x56: // stind.r8, stind.i (0x55=stind.r8, 0x56=?)
                case 0x57: // (unused placeholder, safe as 0)
                case 0x58: case 0x59: case 0x5A: case 0x5B: // add, sub, mul, div
                case 0x5C: case 0x5D: case 0x5E: case 0x5F: // div.un, rem, rem.un, and
                case 0x60: case 0x61: case 0x62: case 0x63: // or, xor, shl, shr
                case 0x64: case 0x65: case 0x66: case 0x67: // shr.un, neg, not, conv.i1
                case 0x68: case 0x69: case 0x6A: case 0x6B: // conv.i2, conv.i4, conv.i8, conv.r4
                case 0x6C: case 0x6D: case 0x6E: // conv.r8, conv.u4, conv.u8
                case 0x76: case 0x77: case 0x78: // conv.r.un, (unused), (unused)
                case 0x82: case 0x83: case 0x84: case 0x85: // conv.ovf.i1..u2
                case 0x86: case 0x87: case 0x88: case 0x89: // conv.ovf.u2..i8
                case 0x8A: case 0x8B: // conv.ovf.u8, (unused)
                case 0x8E: // ldlen
                case 0x90: case 0x91: case 0x92: case 0x93: // ldelem.i1..u1
                case 0x94: case 0x95: case 0x96: case 0x97: // ldelem.u2..r4
                case 0x98: case 0x99: case 0x9A: // ldelem.r8, ldelem.i, ldelem.ref
                case 0x9B: case 0x9C: case 0x9D: case 0x9E: // stelem.i, stelem.i1..i4
                case 0x9F: case 0xA0: case 0xA1: case 0xA2: // stelem.i8..ref
                case 0xB3: case 0xB4: case 0xB5: case 0xB6: // conv.ovf.i1.un..u2.un
                case 0xB7: case 0xB8: case 0xB9: case 0xBA: // conv.ovf.i4.un..u8.un
                case 0xC3: // ckfinite
                case 0xD1: case 0xD2: case 0xD3: // conv.u2, conv.u1, conv.i
                case 0xDC: // endfinally
                case 0xE0: // (prefix — treated as 0 operand)
                    return 0;

                // ShortInlineVar (1 byte)
                case 0x0E: // ldarg.s
                case 0x0F: // ldarga.s
                case 0x10: // starg.s
                case 0x11: // ldloc.s
                case 0x12: // ldloca.s
                case 0x13: // stloc.s
                // ShortInlineI (1 byte)
                case 0x1F: // ldc.i4.s
                // ShortInlineBrTarget (1 byte)
                case 0x2B: // br.s
                case 0x2C: case 0x2D: case 0x2E: case 0x2F: // brfalse.s..bge.s
                case 0x30: case 0x31: case 0x32: case 0x33: // bgt.s..bne.un.s
                case 0x34: case 0x35: case 0x36: case 0x37: // bge.un.s..blt.un.s
                case 0xDD: // leave.s
                    return 1;

                // InlineI (4 bytes)
                case 0x20: // ldc.i4
                    return 4;

                // InlineI8 (8 bytes)
                case 0x21: // ldc.i8
                    return 8;

                // ShortInlineR (4 bytes)
                case 0x22: // ldc.r4
                    return 4;

                // InlineR (8 bytes)
                case 0x23: // ldc.r8
                    return 8;

                // InlineMethod (4 bytes) — call, callvirt, newobj handled specially before this
                case 0x27: // jmp
                case 0x28: // call
                case 0x29: // calli
                case 0x6F: // callvirt
                case 0x73: // newobj
                    return 4;

                // InlineBrTarget (4 bytes)
                case 0x38: // br
                case 0x39: case 0x3A: case 0x3B: case 0x3C: // brfalse..bge
                case 0x3D: case 0x3E: case 0x3F: case 0x40: // bgt..bne.un
                case 0x41: case 0x42: case 0x43: case 0x44: // bge.un..blt.un
                case 0xDB: // leave
                    return 4;

                // InlineType / InlineField / InlineString / InlineTok (all 4 bytes)
                case 0x70: // cpobj
                case 0x71: // ldobj
                case 0x72: // ldstr
                case 0x74: // castclass
                case 0x75: // isinst
                case 0x79: // unbox
                case 0x7B: // ldfld
                case 0x7C: // ldflda
                case 0x7D: // stfld
                case 0x7E: // ldsfld
                case 0x7F: // ldsflda
                case 0x80: // stsfld
                case 0x81: // stobj
                case 0x8C: // box
                case 0x8D: // newarr
                case 0x8F: // ldelema
                case 0xA3: // ldelem
                case 0xA4: // stelem
                case 0xA5: // unbox.any
                case 0xC2: // refanyval
                case 0xC6: // mkrefany
                case 0xD0: // ldtoken
                    return 4;

                // InlineSwitch — handled in GetOperandSize before reaching here
                case 0x45:
                    return 0; // should not reach here

                default:
                    return 0;
            }
        }

        /// <summary>
        /// Returns the operand size for opcodes with 0xFE prefix.
        /// Based on ECMA-335 Partition III, Table III.2.
        /// </summary>
        private static int GetFEPrefixOperandSize(byte opcode2)
        {
            switch (opcode2)
            {
                // InlineNone
                case 0x00: // arglist
                case 0x01: // ceq
                case 0x02: // cgt
                case 0x03: // cgt.un
                case 0x04: // clt
                case 0x05: // clt.un
                case 0x0F: // localloc
                case 0x11: // endfilter
                case 0x12: // volatile. (prefix)
                case 0x13: // tail. (prefix)
                case 0x16: // readonly. (prefix)
                case 0x17: // cpblk
                case 0x18: // initblk
                case 0x1A: // rethrow
                case 0x1D: // refanytype
                    return 0;

                // InlineMethod (4 bytes)
                case 0x06: // ldftn
                case 0x07: // ldvirtftn
                    return 4;

                // InlineType (4 bytes)
                case 0x14: // initobj
                case 0x15: // constrained. (prefix)
                case 0x1C: // sizeof
                    return 4;

                // InlineVar (2 bytes)
                case 0x09: // ldarg
                case 0x0A: // ldarga
                case 0x0B: // starg
                case 0x0C: // ldloc
                case 0x0D: // ldloca
                case 0x0E: // stloc
                    return 2;

                default:
                    return 0;
            }
        }

        #endregion

        public void Dispose()
        {
            foreach (var entry in _peReaderCache.Values)
            {
                entry?.Dispose();
            }

            _peReaderCache.Clear();
        }

        private sealed class PEReaderEntry : IDisposable
        {
            public PEReader PEReader { get; }
            public MetadataReader MetadataReader { get; }
            private readonly FileStream _stream;

            public PEReaderEntry(PEReader peReader, MetadataReader metadataReader, FileStream stream)
            {
                PEReader = peReader;
                MetadataReader = metadataReader;
                _stream = stream;
            }

            public void Dispose()
            {
                PEReader.Dispose();
                _stream.Dispose();
            }
        }
    }

    /// <summary>
    /// Represents a call target discovered via IL analysis.
    /// </summary>
    internal sealed class ILCallTarget
    {
        public IMethodSymbol Method { get; }

        public ILCallTarget(IMethodSymbol method)
        {
            Method = method;
        }
    }
}
