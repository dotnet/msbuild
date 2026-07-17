// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

// Polyfills for the trimming / Native AOT analyzer attributes that ship in the runtime
// starting with .NET 5. Microsoft.Build.Framework enables the trim/AOT analyzers
// (IsAotCompatible) on its net8.0+ build, but the annotations themselves are written
// unconditionally so they remain in the public metadata for every target framework.
//
// On net10.0 these types come from the runtime. On net472 and netstandard2.0 they are not
// available to reference: several dependencies (System.Text.Json, System.Collections.Immutable,
// Microsoft.IO.Redist, ...) embed their own *internal* copies of these attributes, so the
// types appear "already declared" - PolySharp therefore skips generating them, yet those
// copies are inaccessible from this assembly. Defining our own internal copies here makes the
// annotations resolve. The trim/AOT analyzer never runs on net472 or netstandard2.0, so these
// definitions only need to exist for the annotations to compile - the trimmer recognizes them
// by full type name on net10.0 where the real runtime types are used.
#if !NET

namespace System.Diagnostics.CodeAnalysis
{
    /// <summary>
    /// Specifies the types of members that are dynamically accessed. This enumeration has a
    /// <see cref="FlagsAttribute"/> attribute that allows a bitwise combination of its member values.
    /// </summary>
    [Flags]
    internal enum DynamicallyAccessedMemberTypes
    {
        None = 0,
        PublicParameterlessConstructor = 0x0001,
        PublicConstructors = 0x0002 | PublicParameterlessConstructor,
        NonPublicConstructors = 0x0004,
        PublicMethods = 0x0008,
        NonPublicMethods = 0x0010,
        PublicFields = 0x0020,
        NonPublicFields = 0x0040,
        PublicNestedTypes = 0x0080,
        NonPublicNestedTypes = 0x0100,
        PublicProperties = 0x0200,
        NonPublicProperties = 0x0400,
        PublicEvents = 0x0800,
        NonPublicEvents = 0x1000,
        Interfaces = 0x2000,
        All = ~None,
    }

    /// <summary>
    /// Indicates that certain members on a specified <see cref="Type"/> are accessed dynamically,
    /// for example, through <see cref="System.Reflection"/>.
    /// </summary>
    [AttributeUsage(
        AttributeTargets.Field | AttributeTargets.ReturnValue | AttributeTargets.GenericParameter |
        AttributeTargets.Parameter | AttributeTargets.Property | AttributeTargets.Method,
        Inherited = false)]
    internal sealed class DynamicallyAccessedMembersAttribute : Attribute
    {
        public DynamicallyAccessedMembersAttribute(DynamicallyAccessedMemberTypes memberTypes) => MemberTypes = memberTypes;

        public DynamicallyAccessedMemberTypes MemberTypes { get; }
    }

    /// <summary>
    /// Indicates that the specified method requires dynamic access to code that is not referenced
    /// statically, for example, through <see cref="System.Reflection"/>.
    /// </summary>
    [AttributeUsage(AttributeTargets.Constructor | AttributeTargets.Method, Inherited = false)]
    internal sealed class RequiresUnreferencedCodeAttribute : Attribute
    {
        public RequiresUnreferencedCodeAttribute(string message) => Message = message;

        public string Message { get; }

        public string? Url { get; set; }
    }

    /// <summary>
    /// Indicates that the specified method requires the ability to generate new code at runtime,
    /// for example, through <see cref="System.Reflection"/>. This is incompatible with Native AOT.
    /// </summary>
    [AttributeUsage(AttributeTargets.Constructor | AttributeTargets.Method, Inherited = false)]
    internal sealed class RequiresDynamicCodeAttribute : Attribute
    {
        public RequiresDynamicCodeAttribute(string message) => Message = message;

        public string Message { get; }

        public string? Url { get; set; }
    }

    /// <summary>
    /// Suppresses reporting of a specific rule violation, allowing multiple suppressions on a single code artifact.
    /// Unlike <see cref="SuppressMessageAttribute"/>, this attribute is preserved in metadata and applied by the trimmer.
    /// </summary>
    [AttributeUsage(AttributeTargets.All, Inherited = false, AllowMultiple = true)]
    internal sealed class UnconditionalSuppressMessageAttribute : Attribute
    {
        public UnconditionalSuppressMessageAttribute(string category, string checkId)
        {
            Category = category;
            CheckId = checkId;
        }

        public string Category { get; }

        public string CheckId { get; }

        public string? Scope { get; set; }

        public string? Target { get; set; }

        public string? MessageId { get; set; }

        public string? Justification { get; set; }
    }

    /// <summary>
    /// States a dependency that one member has on another, ensuring the dependency is kept by the trimmer.
    /// </summary>
    [AttributeUsage(
        AttributeTargets.Method | AttributeTargets.Constructor | AttributeTargets.Field,
        AllowMultiple = true,
        Inherited = false)]
    internal sealed class DynamicDependencyAttribute : Attribute
    {
        public DynamicDependencyAttribute(string memberSignature) => MemberSignature = memberSignature;

        public DynamicDependencyAttribute(string memberSignature, string typeName, string assemblyName)
        {
            MemberSignature = memberSignature;
            TypeName = typeName;
            AssemblyName = assemblyName;
        }

        public DynamicDependencyAttribute(DynamicallyAccessedMemberTypes memberTypes, Type type)
        {
            MemberTypes = memberTypes;
            Type = type;
        }

        public string? MemberSignature { get; }

        public DynamicallyAccessedMemberTypes MemberTypes { get; }

        public Type? Type { get; }

        public string? TypeName { get; }

        public string? AssemblyName { get; }

        public string? Condition { get; set; }
    }

    /// <summary>
    /// Indicates that a boolean property or field is a feature switch, mapped to the named
    /// AppContext switch. The trimmer can substitute a constant value for the switch and remove
    /// guarded, statically unreachable branches.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, Inherited = false)]
    internal sealed class FeatureSwitchDefinitionAttribute : Attribute
    {
        public FeatureSwitchDefinitionAttribute(string switchName) => SwitchName = switchName;

        public string SwitchName { get; }
    }

    /// <summary>
    /// Indicates that the annotated static boolean property guards access to a feature that requires
    /// the referenced capability (for example <see cref="RequiresUnreferencedCodeAttribute"/> or
    /// <see cref="RequiresDynamicCodeAttribute"/>). The trim/AOT analyzer treats a check of the property
    /// as a guard, so calls to the referenced capability inside the guarded branch do not warn.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property, Inherited = false, AllowMultiple = true)]
    internal sealed class FeatureGuardAttribute : Attribute
    {
        public FeatureGuardAttribute(Type featureType) => FeatureType = featureType;

        public Type FeatureType { get; }
    }
}

#endif
