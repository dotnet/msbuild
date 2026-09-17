# Polyfills for Older TFMs

`DynamicallyAccessedMembersAttribute` shipped in .NET 5. For projects targeting netstandard2.0 or net472, you need a polyfill. The trimmer recognizes the attribute by name, so a local copy works:

```csharp
#if !NET
namespace System.Diagnostics.CodeAnalysis
{
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.ReturnValue |
                    AttributeTargets.GenericParameter | AttributeTargets.Parameter |
                    AttributeTargets.Property, Inherited = false)]
    internal sealed class DynamicallyAccessedMembersAttribute : Attribute
    {
        public DynamicallyAccessedMembersAttribute(DynamicallyAccessedMemberTypes memberTypes)
            => MemberTypes = memberTypes;
        public DynamicallyAccessedMemberTypes MemberTypes { get; }
    }

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
        All = ~None // Discouraged — prefer specific flags
    }
}
#endif
```

Similarly for `RequiresUnreferencedCodeAttribute` and `UnconditionalSuppressMessageAttribute` if needed on older TFMs.
