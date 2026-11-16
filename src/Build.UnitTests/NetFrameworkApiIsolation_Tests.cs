// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using Microsoft.Build.BackEnd.SdkResolution;
using Microsoft.Build.Execution;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using Shouldly;
using Xunit;

namespace Microsoft.Build.Engine.UnitTests;

/// <summary>
/// Tests for the .NET Framework flavor of MSBuild when its assemblies are loaded into a .NET
/// process. See documentation/NETFramework-MSBuild-on-NET.md.
/// </summary>
public class NetFrameworkApiIsolation_Tests
{
    [Fact]
    public void RuntimeCapabilitiesMatchTheRuntime()
    {
#if NETFRAMEWORK
        RuntimeCapabilities.SupportsPipeOptionsCurrentUserOnly.ShouldBeFalse();
        RuntimeCapabilities.CurrentUserOnlyIfSupported.ShouldBe(PipeOptions.None);
        RuntimeCapabilities.AssemblyLoadUsesCodeBase.ShouldBeTrue();
        RuntimeCapabilities.SupportsAssemblyNameKeyPair.ShouldBeTrue();
        RuntimeCapabilities.SupportsAppDomains.ShouldBeTrue();
        RuntimeCapabilities.SupportsRemoting.ShouldBeTrue();
#else
        RuntimeCapabilities.SupportsPipeOptionsCurrentUserOnly.ShouldBeTrue();
        RuntimeCapabilities.CurrentUserOnlyIfSupported.ShouldBe(PipeOptions.CurrentUserOnly);
        RuntimeCapabilities.AssemblyLoadUsesCodeBase.ShouldBeFalse();
        RuntimeCapabilities.SupportsAssemblyNameKeyPair.ShouldBeFalse();
#endif
    }

#if !FEATURE_ASSEMBLYLOADCONTEXT
    /// <summary>
    /// On .NET Framework the choice is the same as before the runtime was taken into account:
    /// Assembly.Load in MSBuild.exe and Visual Studio, so that a resolver that fails to load still
    /// produces MSB4243. When the runtime ignores the CodeBase, Assembly.LoadFrom is used everywhere.
    /// </summary>
    [Theory]
    [InlineData(false, false, true, false)]
    [InlineData(true, true, true, false)]
    [InlineData(true, false, true, true)]
    [InlineData(false, false, false, true)]
    [InlineData(true, true, false, true)]
    [InlineData(true, false, false, true)]
    public void SdkResolverIsLoadedFromPathWhenTheRuntimeIgnoresCodeBase(
        bool standalone,
        bool runningInMSBuildExe,
        bool assemblyLoadUsesCodeBase,
        bool expected)
    {
        BuildEnvironmentMode mode = standalone ? BuildEnvironmentMode.Standalone : BuildEnvironmentMode.VisualStudio;
        SdkResolverLoader.ShouldLoadResolverFromPath(mode, runningInMSBuildExe, assemblyLoadUsesCodeBase).ShouldBe(expected);
    }
#endif

#if FEATURE_APPDOMAIN
    /// <summary>
    /// Methods that reference a .NET Framework-only API without being marked NoInlining, because
    /// they run only in a .NET Framework process: remoting calls them, or they run in an MSBuild.exe
    /// node or task host and never in the process that loaded the MSBuild assemblies.
    /// </summary>
    private static readonly HashSet<string> s_methodsThatRunOnlyOnNetFramework = new(StringComparer.Ordinal)
    {
        // MarshalByRefObject overrides that only .NET Remoting calls.
        "Microsoft.Build.BackEnd.TaskHost::InitializeLifetimeService",
        "Microsoft.Build.BackEnd.TaskFactoryEngineContext::InitializeLifetimeService",
        "Microsoft.Build.Utilities.TaskLoggingHelper::InitializeLifetimeService",

        // Server ends of pipes, created in MSBuild.exe nodes and in the RAR node.
        "Microsoft.Build.BackEnd.NodeEndpointOutOfProcBase::InternalConstruct",
        "Microsoft.Build.Internal.NodePipeServer::.ctor",
    };

    private static readonly Dictionary<short, OpCode> s_opCodes = typeof(OpCodes)
        .GetFields(BindingFlags.Public | BindingFlags.Static)
        .Select(field => (OpCode)field.GetValue(null)!)
        .ToDictionary(opCode => opCode.Value);

    /// <summary>
    /// A method that references a type or member that .NET does not have throws when it is
    /// compiled on .NET, even if the reference is in a branch that does not run. Each such reference
    /// in the assemblies a .NET process loads must be in a method marked NoInlining (and called only
    /// when RuntimeCapabilities reports the API as available) or in a method that runs only on
    /// .NET Framework. This test reads the IL of every method of those assemblies.
    /// </summary>
    [Fact]
    public void NetFrameworkOnlyApisAreReferencedOnlyFromIsolatedMethods()
    {
        Assembly[] assemblies =
        {
            typeof(BuildManager).Assembly,
            typeof(ITask).Assembly,
            typeof(Microsoft.Build.Tasks.GenerateResource).Assembly,
            typeof(Microsoft.Build.Utilities.ToolTask).Assembly,
        };

        var violations = new List<string>();
        foreach (Assembly assembly in assemblies)
        {
            foreach (Type type in GetLoadableTypes(assembly))
            {
                foreach (MethodBase method in GetMethodsWithBodies(type))
                {
                    string methodName = $"{type.FullName}::{method.Name}";
                    if ((method.GetMethodImplementationFlags() & MethodImplAttributes.NoInlining) != 0 ||
                        s_methodsThatRunOnlyOnNetFramework.Contains(methodName))
                    {
                        continue;
                    }

                    foreach (string reference in GetNetFrameworkOnlyReferences(method))
                    {
                        violations.Add($"{assembly.GetName().Name}: {methodName} references {reference}");
                    }
                }
            }
        }

        violations.Distinct().ShouldBeEmpty();
    }

    private static IEnumerable<Type> GetLoadableTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            return ex.Types.Where(type => type != null)!;
        }
    }

    private static IEnumerable<MethodBase> GetMethodsWithBodies(Type type)
    {
        const BindingFlags AllDeclared = BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        IEnumerable<MethodBase> methods = type.GetMethods(AllDeclared).Cast<MethodBase>().Concat(type.GetConstructors(AllDeclared));
        foreach (MethodBase method in methods)
        {
            MethodBody? body = null;
            try
            {
                body = method.GetMethodBody();
            }
            catch (Exception ex) when (ex is TypeLoadException or FileNotFoundException)
            {
            }

            if (body != null)
            {
                yield return method;
            }
        }
    }

    private static IEnumerable<string> GetNetFrameworkOnlyReferences(MethodBase method)
    {
        MethodBody body = method.GetMethodBody()!;
        foreach (LocalVariableInfo local in body.LocalVariables)
        {
            if (IsRemotingType(local.LocalType))
            {
                yield return $"a local of type {local.LocalType.FullName}";
            }
        }

        Type[] typeArguments = method.DeclaringType!.IsGenericType ? method.DeclaringType.GetGenericArguments() : Type.EmptyTypes;
        Type[] methodArguments = method.IsGenericMethod ? method.GetGenericArguments() : Type.EmptyTypes;
        foreach (int token in GetMemberTokens(body.GetILAsByteArray()!))
        {
            // A member whose signature names an assembly that is not in the test output cannot be one
            // of the .NET Framework APIs this test looks for, all of which are in the framework itself.
            string? description = null;
            try
            {
                MemberInfo? member = method.Module.ResolveMember(token, typeArguments, methodArguments);
                description = member == null ? null : DescribeIfNetFrameworkOnly(member);
            }
            catch (Exception ex) when (ex is ArgumentException or TypeLoadException or FileNotFoundException or FileLoadException or MissingMemberException or BadImageFormatException)
            {
            }

            if (description != null)
            {
                yield return description;
            }
        }
    }

    private static string? DescribeIfNetFrameworkOnly(MemberInfo member)
    {
        Type? declaringType = member as Type ?? member.DeclaringType;
        if (declaringType != null && IsRemotingType(declaringType))
        {
            return $"{declaringType.FullName}::{member.Name}";
        }

        if (member is FieldInfo field && IsRemotingType(field.FieldType))
        {
            return $"field {field.DeclaringType?.FullName}::{field.Name} of type {field.FieldType.FullName}";
        }

        if (member is MethodBase method)
        {
            ParameterInfo[] parameters = method.GetParameters();
            if (declaringType == typeof(AppDomain) && method.Name == nameof(AppDomain.CreateDomain) && parameters.Length > 1)
            {
                return $"AppDomain.CreateDomain({string.Join(", ", parameters.Select(parameter => parameter.ParameterType.Name))})";
            }

            if (declaringType == typeof(AppDomainSetup) &&
                (method.IsConstructor || method.Name is nameof(AppDomainSetup.GetConfigurationBytes) or nameof(AppDomainSetup.SetConfigurationBytes)))
            {
                return $"AppDomainSetup::{method.Name}";
            }

            if (typeof(PipeStream).IsAssignableFrom(declaringType) && method.Name == "GetAccessControl" && !method.IsStatic)
            {
                return "PipeStream.GetAccessControl()";
            }

            if (declaringType == typeof(NamedPipeServerStream) && method.IsConstructor &&
                parameters.Any(parameter => parameter.ParameterType == typeof(PipeSecurity)))
            {
                return "NamedPipeServerStream(..., PipeSecurity, ...)";
            }
        }

        return null;
    }

    private static bool IsRemotingType(Type type)
    {
        while (type.HasElementType)
        {
            type = type.GetElementType()!;
        }

        return type.Namespace?.StartsWith("System.Runtime.Remoting", StringComparison.Ordinal) == true;
    }

    private static IEnumerable<int> GetMemberTokens(byte[] il)
    {
        int position = 0;
        while (position < il.Length)
        {
            short value = il[position++];
            if (value == 0xFE)
            {
                value = unchecked((short)(0xFE00 | il[position++]));
            }

            OpCode opCode = s_opCodes[value];
            switch (opCode.OperandType)
            {
                case OperandType.InlineMethod:
                case OperandType.InlineField:
                case OperandType.InlineType:
                case OperandType.InlineTok:
                    yield return BitConverter.ToInt32(il, position);
                    position += 4;
                    break;
                case OperandType.InlineNone:
                    break;
                case OperandType.ShortInlineBrTarget:
                case OperandType.ShortInlineI:
                case OperandType.ShortInlineVar:
                    position += 1;
                    break;
                case OperandType.InlineVar:
                    position += 2;
                    break;
                case OperandType.InlineI8:
                case OperandType.InlineR:
                    position += 8;
                    break;
                case OperandType.InlineSwitch:
                    position += 4 + (4 * BitConverter.ToInt32(il, position));
                    break;
                default:
                    position += 4;
                    break;
            }
        }
    }
#endif
}
