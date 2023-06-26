// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.DotNet.ApiSymbolExtensions;
using Microsoft.DotNet.ApiSymbolExtensions.Filtering;
using Microsoft.DotNet.ApiSymbolExtensions.Logging;
using Microsoft.DotNet.ApiSymbolExtensions.Tests;
using Microsoft.DotNet.GenAPI.Filtering;
using Xunit;

namespace Microsoft.DotNet.GenAPI.Tests
{
    public class CSharpFileBuilderTests
    {
        class AllowAllFilter : ISymbolFilter
        {
            public bool Include(ISymbol symbol) => true;
        }

        private static MetadataReference[] MetadataReferences { get; } = new[] { MetadataReference.CreateFromFile(typeof(object).Assembly!.Location!) };

        private static SyntaxTree GetSyntaxTree(string syntax) =>
            CSharpSyntaxTree.ParseText(syntax);

        private void RunTest(string original,
            string expected,
            bool includeInternalSymbols = true,
            bool includeEffectivelyPrivateSymbols = true,
            bool includeExplicitInterfaceImplementationSymbols = true,
            bool allowUnsafe = false,
            [CallerMemberName]string assemblyName = "")
        {
            StringWriter stringWriter = new();

            CompositeSymbolFilter compositeFilter = new CompositeSymbolFilter()
                .Add(new ImplicitSymbolFilter())
                .Add(new AccessibilitySymbolFilter(includeInternalSymbols,
                    includeEffectivelyPrivateSymbols, includeExplicitInterfaceImplementationSymbols));
            IAssemblySymbolWriter csharpFileBuilder = new CSharpFileBuilder(new ConsoleLog(MessageImportance.Low),
                compositeFilter, stringWriter, null, false, MetadataReferences);

            using Stream assemblyStream = SymbolFactory.EmitAssemblyStreamFromSyntax(original, enableNullable: true, allowUnsafe: allowUnsafe, assemblyName: assemblyName);
            AssemblySymbolLoader assemblySymbolLoader = new AssemblySymbolLoader(resolveAssemblyReferences: true, includeInternalSymbols: includeInternalSymbols);
            assemblySymbolLoader.AddReferenceSearchPaths(typeof(object).Assembly!.Location!);            
            IAssemblySymbol assemblySymbol = assemblySymbolLoader.LoadAssembly(assemblyName, assemblyStream);

            csharpFileBuilder.WriteAssembly(assemblySymbol);

            StringBuilder stringBuilder = stringWriter.GetStringBuilder();
            string resultedString = stringBuilder.ToString();

            stringBuilder.Remove(0, stringBuilder.Length);

            SyntaxTree resultedSyntaxTree = GetSyntaxTree(resultedString);
            SyntaxTree expectedSyntaxTree = GetSyntaxTree(expected);

            // compare SyntaxTree and not string representation
            Assert.True(resultedSyntaxTree.IsEquivalentTo(expectedSyntaxTree),
                $"Expected:\n{expected}\nResulted:\n{resultedString}");
        }

        [Fact]
        public void TestNamespaceDeclaration()
        {
            RunTest(original: """
                namespace A
                {
                namespace B {}

                namespace C.D { public struct Bar {} }
                }
                """,
                expected: """
                namespace A.C.D { public partial struct Bar {} }
                """);
        }

        [Fact]
        public void TestClassDeclaration()
        {
            RunTest(original: """
                namespace Foo
                {
                    public class PublicClass { }

                    class InternalClass { }

                    public sealed class PublicSealedClass { }

                    public partial class ProtectedPartialClass { }
                }
                """,
                expected: """
                namespace Foo
                {
                    internal partial class InternalClass { }

                    /// `partial` keyword is not added!
                    public partial class ProtectedPartialClass { }

                    public partial class PublicClass { }

                    public sealed partial class PublicSealedClass { }
                }
                """);
        }

        [Fact(Skip= "https://github.com/dotnet/sdk/issues/32196")]
        public void TestStructDeclaration()
        {
            RunTest(original: """
                namespace Foo
                {
                    public struct PublicStruct { }

                    struct InternalStruct { }

                    readonly struct ReadonlyStruct { }

                    public readonly struct PublicReadonlyStruct { }

                    record struct RecordStruct { }

                    readonly record struct ReadonlyRecordStruct { }

                    public ref struct PublicRefStruct { }

                    public readonly ref struct PublicReadonlyRefStruct { }
                }
                """,
                expected: """
                namespace Foo
                {
                    internal partial struct InternalStruct
                    {
                    }

                    public readonly partial struct PublicReadonlyRefStruct
                    {
                    }

                    public readonly partial struct PublicReadonlyStruct
                    {
                    }

                    public partial struct PublicRefStruct
                    {
                    }

                    public partial struct PublicStruct
                    {
                    }

                    internal readonly record struct ReadonlyRecordStruct : System.IEquatable<ReadonlyRecordStruct>
                    {
                    }

                    internal readonly partial struct ReadonlyStruct
                    {
                    }

                    internal record struct RecordStruct : System.IEquatable<RecordStruct>
                    {
                    }
                }
                """);
        }

        [Fact]
        public void TestInterfaceGeneration()
        {
            RunTest(original: """
                namespace Foo
                {
                    public interface IPoint
                    {
                        // Property signatures:
                        int X { get; set; }
                        int Y { get; set; }

                        double CalculateDistance(IPoint p);
                    }
                }
                """,
                expected: """
                namespace Foo
                {
                    public partial interface IPoint
                    {
                        // Property signatures:
                        int X { get; set; }
                        int Y { get; set; }

                        double CalculateDistance(IPoint p);
                    }
                }
                """);
        }

        [Fact]
        public void TestEnumGeneration()
        {
            RunTest(original: """
                namespace Foo
                {
                    public enum Color
                    {
                        White = 0,
                        Green = 100,
                        Blue = 200
                    }
                }
                """,
                expected: """
                namespace Foo
                {
                    public enum Color
                    {
                        White = 0,
                        Green = 100,
                        Blue = 200
                    }
                }
                """);
        }

        [Fact]
        public void TestPropertyGeneration()
        {
            RunTest(original: """
                namespace Foo
                {
                    public class Car
                    {
                        public int? Drivers { get; }
                        public int Wheels { get => 4; }
                        public bool IsRunning { get; set; }
                        public bool Is4x4 { get => false; set { } }
                    }
                }
                """,
                expected: """
                namespace Foo
                {
                    public partial class Car
                    {
                        public int? Drivers { get { throw null; } }
                        public bool Is4x4 { get { throw null; } set { } }
                        public bool IsRunning { get { throw null; } set { } }
                        public int Wheels { get { throw null; } }
                    }
                }
                """);
        }

        [Fact]
        public void TestAbstractPropertyGeneration()
        {
            RunTest(original: """
                namespace Foo
                {
                    abstract class Car
                    {
                        abstract protected int? Wheels { get; }
                        abstract public bool IsRunning { get; set; }
                    }
                }
                """,
                expected: """
                namespace Foo
                {
                    internal abstract partial class Car
                    {
                        public abstract bool IsRunning { get; set; }
                        protected abstract int? Wheels { get; }
                    }
                }
                """);
        }

        [Fact]
        public void TestExplicitInterfaceImplementation()
        {
            RunTest(original: """
                namespace Foo
                {
                    public interface IControl
                    {
                        void Paint();
                    }
                    public interface ISurface
                    {
                        void Paint();
                    }

                    public class SampleClass : IControl, ISurface
                    {
                        public void Paint()
                        {
                        }
                    }
                }
                """,
                expected: """
                namespace Foo
                {
                    public partial interface IControl
                    {
                        void Paint();
                    }
                    public partial interface ISurface
                    {
                        void Paint();
                    }

                    public partial class SampleClass : IControl, ISurface
                    {
                        public void Paint()
                        {
                        }
                    }
                }
                """);
        }

        [Fact]
        public void TestPartiallySpecifiedGenericClassGeneration()
        {
            RunTest(original: """
                namespace Foo
                {
                    public class BaseNodeMultiple<T, U> { }

                    public class Node4<T> : BaseNodeMultiple<T, int> { }

                    public class Node5<T, U> : BaseNodeMultiple<T, U> { }
                }
                """,
                expected: """
                namespace Foo
                {
                    public partial class BaseNodeMultiple <T, U> { }

                    public partial class Node4 <T> : BaseNodeMultiple<T, int> { }

                    public partial class Node5 <T, U> : BaseNodeMultiple<T, U> { }
                }
                """);
        }

        [Fact]
        public void TestGenericClassWitConstraintsParameterGeneration()
        {
            RunTest(original: """
                namespace Foo
                {
                    public class SuperKeyType<K, V, U>
                        where U : System.IComparable<U>
                        where V : new()
                    { }
                }
                """,
                expected: """
                namespace Foo
                {
                    public partial class SuperKeyType <K, V, U> where V : new()
                        where U : System.IComparable<U>
                    {
                    }
                }
                """);
        }

        [Fact]
        public void TestPublicMembersGeneration()
        {
            RunTest(original: """
                namespace Foo
                {
                    public enum Kind
                    {
                        None = 0,
                        Disable = 1
                    }

                    public readonly struct Options
                    {
                        public readonly bool BoolMember = true;
                        public readonly Kind KindMember = Kind.Disable;

                        public Options(Kind kindVal)
                            : this(kindVal, false)
                        {
                        }

                        public Options(Kind kindVal, bool boolVal)
                        {
                            BoolMember = boolVal;
                            KindMember = kindVal;
                        }
                    }
                }
                """,
                expected: """
                namespace Foo
                {
                    public enum Kind
                    {
                        None = 0,
                        Disable = 1
                    }

                    public readonly partial struct Options
                    {
                        public readonly bool BoolMember;
                        public readonly Kind KindMember;
                        public Options(Kind kindVal, bool boolVal) { }
                        public Options(Kind kindVal) { }
                    }
                }
                """);
        }

        [Fact]
        public void TestDelegateGeneration()
        {
            RunTest(original: """
                namespace Foo
                {
                    public delegate bool SyntaxReceiverCreator(int a, bool b);
                }
                """,
                expected: """
                namespace Foo
                {
                    public delegate bool SyntaxReceiverCreator(int a, bool b);
                }
                """);
        }

        [Fact]
        public void TestAbstractEventGeneration()
        {
            RunTest(original: """
                namespace Foo
                {
                    public abstract class AbstractEvents
                    {
                        public abstract event System.EventHandler<bool> TextChanged;
                    }

                    public class Events
                    {
                        public event System.EventHandler<string> OnNewMessage { add { } remove { } }
                    }
                }
                """,
                expected: """
                namespace Foo
                {
                    public abstract partial class AbstractEvents
                    {
                        public abstract event System.EventHandler<bool> TextChanged;
                    }

                    public partial class Events
                    {
                        public event System.EventHandler<string> OnNewMessage { add { } remove { } }
                    }
                }
                """);
        }

        [Fact]
        public void TestCustomAttributeGeneration()
        {
            RunTest(original: """
                using System;
                using System.Diagnostics;

                namespace Foo
                {
                    public enum Animal
                    {
                        Dog = 1,
                        Cat,
                        Bird,
                    }

                    public class AnimalTypeAttribute : Attribute
                    {
                        protected Animal thePet;

                        public AnimalTypeAttribute(Animal pet)
                        {
                            thePet = pet;
                        }

                        public Animal Pet
                        {
                            get { return thePet; }
                            set { thePet = value; }
                        }
                    }

                    [AnimalType(Animal.Cat)]
                    public class Creature
                    {
                        [AnimalType(Animal.Cat)]
                        [Conditional("DEBUG"), Conditional("TEST1")]
                        public void SayHello() { }
                    }
                }
                """,
                expected: """
                namespace Foo
                {
                    public enum Animal
                    {
                        Dog = 1,
                        Cat = 2,
                        Bird = 3
                    }

                    public partial class AnimalTypeAttribute : System.Attribute
                    {
                        protected Animal thePet;
                        public AnimalTypeAttribute(Animal pet) { }
                        public Animal Pet { get { throw null; } set { } }
                    }

                    [AnimalType(Animal.Cat)]
                    public partial class Creature
                    {
                        [AnimalType(Animal.Cat)]
                        [System.Diagnostics.Conditional("DEBUG")]
                        [System.Diagnostics.Conditional("TEST1")]
                        public void SayHello() { }
                    }
                }
                """);
        }

        [Fact]
        public void TestFullyQualifiedNamesForDefaultEnumParameters()
        {
            RunTest(original: """
                namespace Foo
                {
                    public enum Animal
                    {
                        Dog = 1,
                        Cat = 2,
                        Bird = 3
                    }

                    public class AnimalProperty {
                        public Animal _animal;

                        public AnimalProperty(Animal animal = Animal.Cat)
                        {
                            _animal = animal;
                        }

                        public int Execute(int p = 42) { return p; }
                    }
                }
                """,
                expected: """
                namespace Foo
                {
                    public enum Animal
                    {
                        Dog = 1,
                        Cat = 2,
                        Bird = 3
                    }

                    public partial class AnimalProperty {
                        public Animal _animal;

                        public AnimalProperty(Animal animal = Animal.Cat) { }

                        public int Execute(int p = 42) { throw null; }
                    }
                }
                """);
        }

        [Fact]
        public void TestCustomComparisonOperatorGeneration()
        {
            RunTest(original: """
                namespace Foo
                {
                    public class Car : System.IEquatable<Car>
                    {
                        public bool Equals(Car? c) { return true; }
                        public override bool Equals(object? o) { return true; }
                        public override int GetHashCode() => 0;
                        public static bool operator ==(Car lhs, Car rhs) { return true; }
                        public static bool operator !=(Car lhs, Car rhs) { return false; }
                    }
                }
                """,
                expected: """
                namespace Foo
                {
                    public partial class Car : System.IEquatable<Car>
                    {
                        public bool Equals(Car? c) { throw null; }
                        public override bool Equals(object? o) { throw null; }
                        public override int GetHashCode() { throw null; }
                        public static bool operator ==(Car lhs, Car rhs) { throw null; }
                        public static bool operator !=(Car lhs, Car rhs) { throw null; }
                    }
                }
                """);
        }

        [Fact]
        public void TestNestedClassGeneration()
        {
            RunTest(original: """
                namespace Foo
                {
                    public class Car
                    {
                        public class Engine
                        {
                            public class Cylinder
                            { }
                        }
                    }
                }
                """,
                expected: """
                namespace Foo
                {
                    public partial class Car
                    {
                        public partial class Engine
                        {
                            public partial class Cylinder
                            { }
                        }
                    }
                }
                """);
        }
        [Fact]
        public void TestExplicitInterfaceImplementationMethodGeneration()
        {
            RunTest(original: """
                namespace Foo
                {
                    public abstract class MemoryManager : System.IDisposable
                    {
                        void System.IDisposable.Dispose() { }
                    }
                }
                """,
                expected: """
                namespace Foo
                {
                    public abstract partial class MemoryManager : System.IDisposable
                    {
                        void System.IDisposable.Dispose() { }
                    }
                }
                """);
        }

        [Fact]
        public void TestNullabilityGeneration()
        {
            RunTest(original: """
                namespace Foo
                {
                    public class Bar
                    {
                        public int? AMember { get; set; }
                        public string? BMember { get; }

                        public string? Execute(string? a, int? b) { return null; }
                    }
                }
                """,
                expected: """
                namespace Foo
                {
                    public partial class Bar
                    {
                        public int? AMember { get { throw null; } set { } }
                        public string? BMember { get { throw null; } }

                        public string? Execute(string? a, int? b) { throw null; }
                    }
                }
                """);
        }

        [Fact]
        public void TestExtensionMethodsGeneration()
        {
            RunTest(original: """
                namespace Foo
                {
                    public static class MyExtensions
                    {
                        public static int WordCount(this string str) { return 42; }
                    }
                }
                """,
                expected: """
                namespace Foo
                {
                    public static partial class MyExtensions
                    {
                        public static int WordCount(this string str) { throw null; }
                    }
                }
                """);
        }

        [Fact]
        public void TestMethodsWithVariableNumberOfArgumentsGeneration()
        {
            RunTest(original: """
                namespace Foo
                {
                    public class Bar
                    {
                        public void Execute(params int[] list) { }
                    }
                }
                """,
                expected: """
                namespace Foo
                {
                    public partial class Bar
                    {
                        public void Execute(params int[] list) { }
                    }
                }
                """);
        }

        [Fact]
        public void TestConversionOperatorGeneration()
        {
            RunTest(original: """
                namespace Foo
                {
                    public readonly struct Digit
                    {
                        private readonly byte digit;

                        public Digit(byte digit)
                        {
                            this.digit = digit;
                        }

                        public static implicit operator byte(Digit d) => d.digit;
                        public static explicit operator Digit(byte b) => new Digit(b);
                    }
                }
                """,
                expected: """
                namespace Foo
                {
                    public readonly partial struct Digit
                    {
                        private readonly int _dummyPrimitive;

                        public Digit(byte digit) { }
                        public static explicit operator Digit(byte b) { throw null; }

                        public static implicit operator byte(Digit d) { throw null; }
                    }
                }
                """);
        }

        [Fact]
        public void TestDestructorGeneration()
        {
            RunTest(original: """
                namespace Foo
                {
                    public class Bar
                    {
                        ~Bar() {}
                    }
                }
                """,
                expected: """
                namespace Foo
                {
                    public partial class Bar
                    {
                        ~Bar() {}
                    }
                }
                """);
        }

        [Fact]
        public void TestExplicitInterfaceImplementationPropertyGeneration()
        {
            RunTest(original: """
                    namespace Foo
                    {
                        public interface IFoo
                        {
                            int FooField { get; set; }
                            void FooMethod();
                        }

                        public class Bar : IFoo
                        {
                            int BarField { get; set; }
                            int IFoo.FooField { get; set; }

                            void IFoo.FooMethod() { }
                        }
                    }
                    """,
                expected: """
                    namespace Foo
                    {
                        public partial class Bar : IFoo
                        {
                            int IFoo.FooField { get { throw null; } set { } }
                            void IFoo.FooMethod() { }
                        }

                        public partial interface IFoo
                        {
                            int FooField { get; set; }
                            void FooMethod();
                        }
                    }
                    """);
        }

        [Fact]
        public void TestAccessibilityGenerationForPropertyAccessors()
        {
            RunTest(original: """
                    namespace Foo
                    {
                        public class Bar
                        {
                            public int P { get; protected set; }
                        }
                    }
                    """,
                expected: """
                    namespace Foo
                    {
                        public partial class Bar
                        {
                            public int P { get { throw null; } protected set { } }
                        }
                    }
                    """);
        }

        [Fact]
        public void TestConstantFieldGeneration()
        {
            RunTest(original: """
                namespace Foo
                {
                    public class Bar
                    {
                        public const int CurrentEra = 0;
                    }
                }
                """,
                expected: """
                namespace Foo
                {
                    public partial class Bar
                    {
                        public const int CurrentEra = 0;
                    }
                }
                """
            );
        }

        [Fact]
        public void TestTypeParameterVarianceGeneration()
        {
            RunTest(original: """
                namespace Foo
                {
                    public delegate void Action<in T>(T obj);
                    public partial interface IAsyncEnumerable<out T>
                    {
                    }
                }
                """,
                expected: """
                namespace Foo
                {
                    public delegate void Action<in T>(T obj);
                    public partial interface IAsyncEnumerable<out T>
                    {
                    }
                }
                """
            );
        }

        [Fact]
        public void TestRefMembersGeneration()
        {
            RunTest(original: """
                namespace Foo
                {
                    public class Bar<T>
                    {
                    #pragma warning disable CS8597
                        public ref T GetPinnableReference() { throw null; }
                        public ref readonly T this[int index] { get { throw null; } }
                        public ref int P { get { throw null; } }
                    #pragma warning restore CS8597
                    }
                }
                """,
                expected: """
                namespace Foo
                {
                    public partial class Bar<T>
                    {
                    #pragma warning disable CS8597
                        public ref readonly T this[int index] { get { throw null; } }
                        public ref int P { get { throw null; } }
                        public ref T GetPinnableReference() { throw null; }
                    #pragma warning restore CS8597
                    }
                }
                """
            );
        }

        [Fact]
        public void TestDefaultConstraintOnOverrideGeneration()
        {
            RunTest(original: """
                namespace Foo
                {
                    public abstract partial class A
                    {
                        public abstract TResult? Accept<TResult>(int a);
                    }

                    public sealed partial class B : A
                    {
                    #pragma warning disable CS8597
                        public override TResult? Accept<TResult>(int a) where TResult : default { throw null; }
                    #pragma warning restore CS8597
                    }
                }
                """,
                expected: """
                namespace Foo
                {
                    public abstract partial class A
                    {
                        public abstract TResult? Accept<TResult>(int a);
                    }

                    public sealed partial class B : A
                    {
                    #pragma warning disable CS8597
                        public override TResult? Accept<TResult>(int a) where TResult : default { throw null; }
                    #pragma warning restore CS8597
                    }
                }
                """
            );
        }

        [Fact]
        public void TestSynthesizePrivateFieldsForValueTypes()
        {
            RunTest(original: """
                using System;

                namespace Foo
                {
                    public struct Bar
                    {
                        #pragma warning disable 0169
                        private IntPtr intPtr;
                    }
                }
                """,
                expected: """
                namespace Foo
                {
                    public partial struct Bar
                    {
                        private int _dummyPrimitive;
                    }
                }
                """);
        }

        [Fact]
        public void TestSynthesizePrivateFieldsForReferenceTypes()
        {
            RunTest(original: """
                namespace Foo
                {
                    public struct Bar
                    {
                        #pragma warning disable 0169
                        private object field;
                    }
                }
                """,
                expected: """
                namespace Foo
                {
                    public partial struct Bar
                    {
                        private object _dummy;
                        private int _dummyPrimitive;
                    }
                }
                """);
        }

        [Fact]
        public void TestSynthesizePrivateFieldsForGenericTypes()
        {
            RunTest(original: """
                namespace Foo
                {
                    public struct Bar<T>
                    {
                        #pragma warning disable 0169
                        private T _field;
                    }
                }
                """,
            expected: """
                namespace Foo
                {
                    public partial struct Bar<T>
                    {
                        private T _field;
                    }
                }
                """);
        }

        [Fact]
        public void TestSynthesizePrivateFieldsForNestedGenericTypes()
        {
            RunTest(original: """
                using System.Collections.Generic;

                namespace Foo
                {
                    public struct Bar<T> where T : notnull
                    {
                        #pragma warning disable 0169
                        private Dictionary<int, List<T>> _field;
                    }
                }
                """,
            expected: """
                namespace Foo
                {
                    public partial struct Bar<T>
                    {
                        private System.Collections.Generic.Dictionary<int, System.Collections.Generic.List<T>> _field;
                        private object _dummy;
                        private int _dummyPrimitive;
                    }
                }
                """);
        }

        [Fact]
        public void TestSynthesizePrivateFieldsAngleBrackets()
        {
            RunTest(original: """
                using System.Collections.Generic;

                namespace Foo
                {
                    public readonly struct Bar<T> where T : notnull
                    {
                        public List<Bar<T>> Baz { get; }
                    }
                }
                """,
            expected: """
                namespace Foo
                {
                    public readonly partial struct Bar<T>
                    {
                        private readonly System.Collections.Generic.List<Bar<T>> _Baz_k__BackingField;
                        private readonly object _dummy;
                        private readonly int _dummyPrimitive;
                        public System.Collections.Generic.List<Bar<T>> Baz { get { throw null; } }
                    }
                }
                """);
        }

        [Fact]
        public void TestSynthesizePrivateFieldsForInaccessibleNestedGenericTypes()
        {
            RunTest(original: """
                namespace A
                {
                    internal class Bar<T> {}

                    public struct Foo<T>
                    {
                        #pragma warning disable 0169
                        // as the includeInternalSymbols field is set to false and the Bar<> class is internal -
                        //   we must skip generation of the `_field` private field.
                        private Bar<T> _field;
                    }
                }
                """,
            expected: """
                namespace A
                {
                    public partial struct Foo<T>
                    {
                        private object _dummy;
                        private int _dummyPrimitive;
                    }
                }
                """,
                includeInternalSymbols: false);
        }

        [Fact]
        public void TestBaseTypeWithoutExplicitDefault()
        {
            RunTest(original: """
                    namespace A
                    {
                        public class B
                        {
                        }

                        public class C : B
                        {
                            public C() {}
                        }
                    }
                    """,
                expected: """
                    namespace A
                    {
                        public partial class B
                        {
                        }

                        public partial class C : B
                        {
                        }
                    }
                    """);
        }

        [Fact]
        public void TestBaseTypeWithExplicitDefaultConstructor()
        {
            RunTest(original: """
                    namespace A
                    {
                        public class B
                        {
                            public B() {}
                        }

                        public class C : B
                        {
                            public C() {}
                        }
                    }
                    """,
                expected: """
                    namespace A
                    {
                        public partial class B
                        {
                        }

                        public partial class C : B
                        {
                        }
                    }
                    """);
        }

        [Fact]
        public void TestInternalParameterlessConstructors()
        {
            RunTest(original: """
                    namespace A
                    {
                        public class B
                        {
                            internal B() {}
                        }

                        public class C : B
                        {
                            internal C() : base() {}
                        }
                    }
                    """,
                expected: """
                    namespace A
                    {
                        public partial class B
                        {
                            internal B() {}
                        }

                        public partial class C : B
                        {
                            internal C() {}
                        }
                    }
                    """,
                    includeInternalSymbols: false);
        }

        [Fact]
        public void TestInternalParameterizedConstructors()
        {
            RunTest(original: """
                    namespace A
                    {
                        public class B
                        {
                            public B(int i) {}
                        }
                    
                        public class C : B
                        {
                            internal C() : base(0) {}
                        }
                    
                        public class D : B
                        {
                            internal D(int i) : base(i) {}
                        }

                        public class E
                        {
                            internal E(int i) {}
                            internal E(string s) {}
                            internal E(P p) {}
                        }

                        internal class P { }
                    }
                    """,
                expected: """
                    namespace A
                    {
                        public partial class B
                        {
                            public B(int i) {}
                        }
                    
                        public partial class C : B
                        {
                            internal C() : base(default) {}
                        }

                        public partial class D : B
                        {
                            internal D() : base(default) {}
                        }

                        public partial class E
                        {
                            internal E() {}
                        }
                    }
                    """,
                    includeInternalSymbols: false);
        }

        [Fact]
        public void TestInternalParameterizedConstructorsPreserveInternals()
        {
            RunTest(original: """
                    namespace A
                    {
                        public class B
                        {
                            public B(int i) {}
                        }
                    
                        public class C : B
                        {
                            internal C() : base(0) {}
                        }
                    
                        public class D : B
                        {
                            internal D(int i) : base(i) {}
                        }

                        public class E
                        {
                            internal E(int i) {}
                            internal E(string s) {}
                            internal E(P p) {}
                        }

                        internal class P { }
                    }
                    """,
                expected: """
                    namespace A
                    {
                        public partial class B
                        {
                            public B(int i) { }
                        }

                        public partial class C : B
                        {
                            internal C() : base(default) { }
                        }

                        public partial class D : B
                        {
                            internal D(int i) : base(default) { }
                        }

                        public partial class E
                        {
                            internal E(P p) { }

                            internal E(int i) { }

                            internal E(string s) { }
                        }

                        internal partial class P
                        {
                        }
                    }
                    """,
                    includeInternalSymbols: true);
        }

        [Fact]
        public void TestInternalConstructorCallingProtected()
        {
            RunTest(original: """
                    namespace A
                    {
                        public class B
                        {
                            protected B() {}
                        }

                        public class C : B
                        {
                            internal C() {}
                        }
                    }
                    """,
                expected: """
                    namespace A
                    {
                        public partial class B
                        {
                            protected B() {}
                        }                    
                    
                        public partial class C : B
                        {
                            internal C() {}
                        }
                    }
                    """,
                    includeInternalSymbols: false);
        }

        [Fact]
        public void TestBaseTypeWithoutDefaultConstructor()
        {
            RunTest(original: """
                    namespace Foo
                    {
                        public class A
                        {
                        }

                        public class B
                        {
                            public B(int p1, string p2, A p3) { }
                        }

                        public class C : B
                        {
                            public C() : base(1, "", new A()) {}
                        }
                    }
                    """,
                expected: """
                    namespace Foo
                    {
                        public partial class A
                        {
                        }

                        public partial class B
                        {
                            public B(int p1, string p2, A p3) { }
                        }

                        public partial class C : B
                        {
                            public C() : base(default, default!, default!) {}
                        }
                    }
                    """);
        }

        [Fact]
        public void TestBaseTypeWithMultipleNonDefaultConstructors()
        {
            RunTest(original: """
                    namespace Foo
                    {
                        public class A
                        {
                        }

                        public class B
                        {
                            public B(int p1, string p2, A p3) { }
                            public B(int p1, string p2) { }
                            public B(int p1) { }
                        }

                        public class C : B
                        {
                            public C() : base(1, "", new A()) {}
                        }
                    }
                    """,
                expected: """
                    namespace Foo
                    {
                        public partial class A
                        {
                        }

                        public partial class B
                        {
                            public B(int p1, string p2, A p3) { }
                            public B(int p1, string p2) { }
                            public B(int p1) { }
                        }

                        public partial class C : B
                        {
                            public C() : base(default) {}
                        }
                    }
                    """);
        }

        [Fact]
        public void TestBaseTypeWithAmbiguousNonDefaultConstructors()
        {
            RunTest(original: """
                    namespace Foo
                    {
                        public class A
                        {
                            public A(char c) { }
                            public A(int i) { }
                            public A(string s) { }
                        }

                        public class B : A
                        {
                            public B() : base("") {}
                        }
                    }
                    """,
                expected: """
                    namespace Foo
                    {
                        public partial class A
                        {
                            public A(char c) { }
                            public A(int i) { }
                            public A(string s) { }
                        }

                        public partial class B : A
                        {
                            public B() : base(default(char)) {}
                        }
                    }
                    """);
        }

        [Fact()]
        public void TestBaseTypeWithAmbiguousNonDefaultConstructorsRegression31655()
        {
            RunTest(original: """
                    namespace Foo
                    {
                        public class A
                        {
                            public A(Id id, System.Collections.Generic.IEnumerable<D> deps) { }
                            public A(string s, V v) { }
                        }

                        public class B : A
                        {
                            public B() : base(new Id(), new D[0]) {}
                        }

                        public class D { }

                        public class Id { }
                    
                        public class V { }
                    }
                    """,
                expected: """
                    namespace Foo
                    {
                        public partial class A
                        {
                            public A(Id id, System.Collections.Generic.IEnumerable<D> deps) { }
                            public A(string s, V v) { }
                        }

                        public partial class B : A
                        {
                            public B() : base(default!, default(System.Collections.Generic.IEnumerable<D>)!) {}
                        }

                        public partial class D { }

                        public partial class Id { }

                        public partial class V { }
                    }
                    """);
        }

        [Fact]
        public void TestBaseTypeConstructorWithObsoleteAttribute()
        {
            RunTest(original: """
                    namespace Foo
                    {
                        public class B
                        {
                            public B(int p1, string p2) { }
                            [System.Obsolete("Constructor is deprecated.", true)]
                            public B(int p1) { }
                        }

                        public class C : B
                        {
                            public C() : base(1, "") { }
                        }
                    }
                    """,
                expected: """
                    namespace Foo
                    {
                        public partial class B
                        {
                            public B(int p1, string p2) { }
                            [System.Obsolete("Constructor is deprecated.", true)]
                            public B(int p1) { }
                        }

                        public partial class C : B
                        {
                            public C() : base(default, default!) { }
                        }
                    }
                    """);
        }

        [Fact]
        public void TestObsoleteBaseTypeConstructorWithoutErrorParameter()
        {
            RunTest(original: """
                    namespace Foo
                    {
                        public class B
                        {
                            public B(int p1, string p2) { }
                            [System.Obsolete("Constructor is deprecated.")]
                            public B(int p1) { }
                        }

                        public class C : B
                        {
                            public C() : base(1, "") { }
                        }
                    }
                    """,
                expected: """
                    namespace Foo
                    {
                        public partial class B
                        {
                            public B(int p1, string p2) { }
                            [System.Obsolete("Constructor is deprecated.")]
                            public B(int p1) { }
                        }

                        public partial class C : B
                        {
                            public C() : base(default) { }
                        }
                    }
                    """);
        }

        [Fact]
        public void TestObsoleteBaseTypeConstructorWithoutMessageParameter()
        {
            RunTest(original: """
                    namespace Foo
                    {
                        public class B
                        {
                            public B(int p1, string p2) { }
                            [System.Obsolete(null)]
                            public B(int p1) { }
                        }

                        public class C : B
                        {
                            public C() : base(1, "") { }
                        }
                    }
                    """,
                expected: """
                    namespace Foo
                    {
                        public partial class B
                        {
                            public B(int p1, string p2) { }
                            [System.Obsolete(null)]
                            public B(int p1) { }
                        }

                        public partial class C : B
                        {
                            public C() : base(default) { }
                        }
                    }
                    """);
        }

        [Fact]
        public void TestFilterOutInternalExplicitInterfaceImplementation()
        {
            RunTest(original: """
                    namespace A
                    {
                        internal interface Foo
                        {
                            void XYZ();
                            int ABC { get; set; }
                        }

                        public class Bar : Foo
                        {
                            int Foo.ABC { get => 1; set { } }
                            void Foo.XYZ() { }
                        }
                    }
                    """,
                expected: """
                    namespace A
                    {
                        public partial class Bar
                        {
                        }
                    }
                    """,
                includeInternalSymbols: false);
        }

        [Fact]
        public void TestMethodsWithReferenceParameterGeneration()
        {
            RunTest(original: """
                    namespace A
                    {
                        public class foo
                        {
                            public void Execute(out int i) { i = 1; }
                        }
                    }
                    """,
                expected: """
                    namespace A
                    {
                        public partial class foo
                        {
                            public void Execute(out int i) { throw null; }
                        }
                    }
                    """,
                includeInternalSymbols: false);
        }

        [Fact(Skip = "https://github.com/dotnet/roslyn/issues/67019")]
        public void TestInterfaceWithOperatorGeneration()
        {
            RunTest(original: """
                    namespace A
                    {
                        public interface IntType
                        {
                            public static IntType operator +(IntType left, IntType right) => left + right;
                        }
                    }
                    """,
                expected: """
                    namespace A
                    {
                        public partial interface IntType
                        {
                            public static IntType operator +(IntType left, IntType right) => left + right;
                        }
                    }
                    """,
                 includeInternalSymbols: false);
        }

        [Fact(Skip = "https://github.com/dotnet/roslyn/issues/67019")]
        public void TestInterfaceWithCheckedOperatorGeneration()
        {
            RunTest(original: """
                    namespace A
                    {
                        public interface IAdditionOperators<TSelf, TOther, TResult>
                            where TSelf : IAdditionOperators<TSelf, TOther, TResult>?
                        {
                            static abstract TResult operator +(TSelf left, TOther right);
                            static virtual TResult operator checked +(TSelf left, TOther right) => left + right;
                        }
                    }
                    """,
                expected: """
                    namespace A
                    {
                        public interface IAdditionOperators<TSelf, TOther, TResult>
                            where TSelf : IAdditionOperators<TSelf, TOther, TResult>?
                        {
                            static abstract TResult operator +(TSelf left, TOther right);
                            static virtual TResult operator checked +(TSelf left, TOther right) { throw null; }
                        }
                    }
                    """,
                 includeInternalSymbols: false);
        }

        [Fact]
        public void TestUnsafeFieldGeneration()
        {
            RunTest(original: """
                    namespace A
                    {
                        public struct Node
                        {
                            public unsafe Node* Left;
                            public unsafe Node* Right;
                            public int Value;
                        }
                    }
                    """,
                expected: """
                    namespace A
                    {
                        public partial struct Node
                        {
                            public unsafe Node* Left;
                            public unsafe Node* Right;
                            public int Value;
                        }
                    }
                    """,
                includeInternalSymbols: false,
                allowUnsafe: true);
        }

        [Fact]
        public void TestUnsafeMethodGeneration()
        {
            RunTest(original: """
                    namespace A
                    {
                        public unsafe class A
                        {
                            public virtual void F(char* p) {}
                        }

                        public class B: A
                        {
                            public unsafe override void F(char* p) {}
                        }
                    }
                    """,
                expected: """
                    namespace A
                    {
                        public partial class A
                        {
                            public virtual unsafe void F(char* p) { }
                        }

                        public partial class B : A
                        {
                            public override unsafe void F(char* p) { }
                        }
                    }
                    """,
                includeInternalSymbols: false,
                allowUnsafe: true);
        }

        [Fact]
        public void TestUnsafeConstructorGeneration()
        {
            RunTest(original: """
                    namespace A
                    {
                        public class Bar
                        {
                            public unsafe Bar(char* f) { }
                        }

                        public class Foo : Bar
                        {
                            public unsafe Foo(char* f) : base(f) { }
                        }
                    }
                    """,
                expected: """
                    namespace A
                    {
                        public partial class Bar
                        {
                            public unsafe Bar(char* f) { }
                        }

                        public partial class Foo : Bar
                        {
                            public unsafe Foo(char* f) : base(default) { }
                        }
                    }
                    """,
                includeInternalSymbols: false,
                allowUnsafe: true);
        }

        [Fact]
        public void TestUnsafeBaseConstructorGeneration()
        {
            RunTest(original: """
                    namespace A
                    {
                        public class Bar
                        {
                            public unsafe Bar(char* f) { }
                        }

                        public class Foo : Bar
                        {
                            public unsafe Foo() : base(default) { }
                        }
                    }
                    """,
                expected: """
                    namespace A
                    {
                        public partial class Bar
                        {
                            public unsafe Bar(char* f) { }
                        }

                        public partial class Foo : Bar
                        {
                            public unsafe Foo() : base(default) { }
                        }
                    }
                    """,
                includeInternalSymbols: false,
                allowUnsafe: true);
        }

        [Fact]
        public void TestInternalDefaultConstructorGeneration()
        {
            RunTest(original: """
                    namespace A
                    {
                        public class Bar
                        {
                            public Bar(int a) { }
                        }

                        public class Foo : Bar
                        {
                            internal Foo() : base(1) { }
                        }
                    }
                    namespace B
                    {
                        public class Bar
                        {
                            public Bar(int a) { }
                        }

                        public class Foo : Bar
                        {
                            private Foo() : base(1) { }
                        }
                    }
                    """,
                expected: """
                    namespace A
                    {
                        public partial class Bar
                        {
                            public Bar(int a) { }
                        }

                        public partial class Foo : Bar
                        {
                            internal Foo() : base(default) { }
                        }
                    }
                    namespace B
                    {
                        public partial class Bar
                        {
                            public Bar(int a) { }
                        }

                        public partial class Foo : Bar
                        {
                            internal Foo() : base(default) { }
                        }
                    }
                    """,
                includeInternalSymbols: false);
        }

        [Fact]
        public void TestPrivateDefaultConstructorGeneration()
        {
            RunTest(original: """
                    namespace A
                    {
                        public class Bar
                        {
                            public Bar(int a) { }
                        }

                        public class Foo : Bar
                        {
                            private Foo() : base(1) { }
                        }
                    }
                    """,
                expected: """
                    namespace A
                    {
                        public partial class Bar
                        {
                            public Bar(int a) { }
                        }

                        public partial class Foo : Bar
                        {
                            private Foo() : base(default) { }
                        }
                    }
                    """,
                includeInternalSymbols: true,
                includeEffectivelyPrivateSymbols: true);
        }

        [Fact]
        public void TestInternalDefaultConstructorGenerationForGenericType()
        {
            RunTest(original: """
                    namespace A
                    {
                        public class Bar
                        {
                            public Bar(int a) { }
                        }

                        public class Foo<T> : Bar
                        {
                            internal Foo() : base(1) { }
                        }
                    }
                    """,
                expected: """
                    namespace A
                    {
                        public partial class Bar
                        {
                            public Bar(int a) { }
                        }

                        public partial class Foo<T> : Bar
                        {
                            internal Foo() : base(default) { }
                        }
                    }
                    """,
                includeInternalSymbols: false);
        }

        [Fact]
        public void TestExplicitParameterlessConstructorNotRemoved()
        {
            RunTest(original: """
                    namespace A
                    {
                        public class Bar
                        {
                            public Bar() { }
                            public Bar(int a) { }
                        }
                    }
                    """,
                expected: """
                    namespace A
                    {
                        public partial class Bar
                        {
                            public Bar() { }
                            public Bar(int a) { }
                        }
                    }
                    """,
                includeInternalSymbols: false);
        }

        [Fact]
        public void TestBaseClassWithExplicitDefaultConstructor()
        {
            RunTest(original: """
                    namespace A
                    {
                        public class Bar
                        {
                            public Bar() { }
                        }

                        public class Foo : Bar
                        {
                        }
                    }
                    """,
                expected: """
                    namespace A
                    {
                        public partial class Bar
                        {
                        }

                        public partial class Foo : Bar
                        {
                        }
                    }
                    """,
                includeInternalSymbols: false);
        }

        [Fact]
        public void TestGenericBaseInterfaceWithInaccessibleTypeArguments()
        {
            RunTest(original: """
                    namespace A
                    {
                        internal class IOption2
                        {
                        }

                        public interface IOption
                        {
                        }

                        public interface AreEqual<T>
                        {
                            bool Compare(T other);
                        }

                        public class PerLanguageOption : AreEqual<IOption2>, IOption
                        {
                            bool AreEqual<IOption2>.Compare(IOption2 other) => false;
                        }
                    }
                    """,
                expected: """
                    namespace A
                    {
                        public partial interface AreEqual<T>
                        {
                            bool Compare(T other);
                        }

                        public partial interface IOption
                        {
                        }

                        public partial class PerLanguageOption : IOption
                        {
                        }
                    }
                    """,
                includeInternalSymbols: false);
        }

        [Fact]
        public void NewKeywordWhenBaseMethodIsHidden()
        {
            RunTest(original: """
                    namespace A
                    {
                        using System;
                        public partial class C : IFun, IExplicit, IExplicit2 {
                            public int Foo;
                            public const int Bar = 29;
                            public int Baz { get; }
                            public int ExplicitProperty { get; }
                            #pragma warning disable 8618
                            public event EventHandler MyEvent;
                            void IExplicit.Explicit() {}
                            #pragma warning disable 8625
                            public void Do() => MyEvent(default(object), default(EventArgs));
                            public void Do(float f) {}
                            public static void DoStatic() {}
                            public void Explicit2() {}
                            public void Fun() {}
                            public void Gen<T>() {}
                            public void Zoo() {}
                            public class MyNestedClass {}
                            public struct MyNestedStruct {}
                            public class MyNestedGenericClass<T> {}
                            public struct MyNestedGenericStruct<T> {}
                            public C this[int i]
                            {
                              get => default(C)!;
                              set {}
                            }
                        }
                        public class D : C, IExplicit, IExplicit2 {
                            public new int Foo;
                            public new const int Bar = 30;
                            int IExplicit2.ExplicitProperty { get; }
                            public new int Baz { get; set; }
                            public new event EventHandler MyEvent;
                            void IExplicit2.Explicit2() {}
                            public new void Do() => MyEvent(default(object), default(EventArgs));
                            public void Do(int i) {}
                            public new static void DoStatic() {}
                            public void Explicit() {}
                            public new void Fun() {}
                            public new void Gen<T>() where T : IComparable {}
                            public new class MyNestedClass {}
                            public new struct MyNestedStruct {}
                            public new class MyNestedGenericClass<T> {}
                            public new struct MyNestedGenericStruct<T> {}
                            public new D this[int i]
                            {
                              get => default(D)!;
                              set {}
                            }
                        }
                        public class E : C, IExplicit, IExplicit2 {
                            public new int Bar;
                            public new const int Do = 30;
                            int IExplicit2.ExplicitProperty { get; }
                            public new int Foo { get; set; }
                            public new event EventHandler MyNestedClass;
                            void IExplicit.Explicit() {}
                            void IExplicit2.Explicit2() {}
                            public new void Baz() => MyNestedClass(default(object), default(EventArgs));
                            public new void MyNestedStruct(double d) {}
                            public new void Zoo() {}
                        }
                        public interface IExplicit {
                            void Explicit();
                        }
                        public interface IExplicit2 {
                            int ExplicitProperty { get; }
                            void Explicit2();
                        }
                        public interface IFun {
                            void Fun();
                        }
                    }
                    """,
                    expected: """
                    namespace A
                    {
                        public partial class C : IFun, IExplicit, IExplicit2
                        {
                            public const int Bar = 29;
                            public int Foo;
                            public int Baz { get { throw null; } }
                            public int ExplicitProperty { get { throw null; } }
                            public C this[int i] { get { throw null; } set {} }
                            public event System.EventHandler MyEvent { add {} remove {} }
                            void IExplicit.Explicit() {}
                            public void Do() {}
                            public void Do(float f) {}
                            public static void DoStatic() {}
                            public void Explicit2() {}
                            public void Fun() {}
                            public void Gen<T>() {}
                            public void Zoo() {}
                            public partial class MyNestedClass {}
                            public partial class MyNestedGenericClass<T> {}
                            public partial struct MyNestedGenericStruct<T> {}
                            public partial struct MyNestedStruct {}
                        }
                        public partial class D : C, IExplicit, IExplicit2
                        {
                            public new const int Bar = 30;
                            public new int Foo;
                            int IExplicit2.ExplicitProperty { get { throw null; } }
                            public new int Baz { get { throw null; } set {} }
                            public new D this[int i] { get { throw null; } set {} }
                            public new event System.EventHandler MyEvent { add {} remove {} }
                            void IExplicit2.Explicit2() {}
                            public new void Do() {}
                            public void Do(int i) {}
                            public new static void DoStatic() {}
                            public void Explicit() {}
                            public new void Fun() {}
                            public new void Gen<T>() where T : System.IComparable {}
                            public new partial class MyNestedClass {}
                            public new partial class MyNestedGenericClass<T> {}
                            public new partial struct MyNestedGenericStruct<T> {}
                            public new partial struct MyNestedStruct {}
                        }
                        public partial class E : C, IExplicit, IExplicit2
                        {
                            public new int Bar;
                            public new const int Do = 30;
                            int IExplicit2.ExplicitProperty { get { throw null; } }
                            public new int Foo { get { throw null; } set {} }
                            public new event System.EventHandler MyNestedClass { add {} remove {} }
                            void IExplicit.Explicit() {}
                            void IExplicit2.Explicit2() {}
                            public new void Baz() {}
                            public new void MyNestedStruct(double d) {}
                            public new void Zoo() {}
                        }
                        public partial interface IExplicit {
                            void Explicit();
                        }
                        public partial interface IExplicit2 {
                            int ExplicitProperty { get; }
                            void Explicit2();
                        }
                        public partial interface IFun {
                            void Fun();
                        }
                    }
                    """);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void TestAttributeWithInternalTypeArgumentOmitted(bool includeInternalSymbols)
        {
            string expected = includeInternalSymbols ? """
                    namespace A
                    {
                        public partial class AnyTestAttribute : System.Attribute
                        {
                            public AnyTestAttribute(System.Type xType) { }

                            public System.Type XType { get { throw null; } set { } }
                        }

                        internal partial class InternalClass
                        {
                        }

                        [AnyTest(typeof(InternalClass))]
                        [System.Obsolete]
                        public partial class PublicClass
                        {
                        }
                    }
                    """ : """
                    namespace A
                    {
                        public partial class AnyTestAttribute : System.Attribute
                        {
                            public AnyTestAttribute(System.Type xType) { }

                            public System.Type XType { get { throw null; } set { } }
                        }

                        [System.Obsolete]
                        public partial class PublicClass
                        {
                        }
                    }
                    """;

            RunTest(original: """
                    namespace A
                    {
                        internal class InternalClass { }
                        public partial class AnyTestAttribute : System.Attribute
                        {
                            public AnyTestAttribute(System.Type xType)
                            {
                                XType = xType;
                            }

                            public System.Type XType { get; set; }
                        }

                        [AnyTest(typeof(InternalClass))]
                        [System.Obsolete]
                        public class PublicClass { }
                    }
                    """,
                expected: expected,
                includeInternalSymbols: includeInternalSymbols);
        }

        [Fact]
        public void TestGenericClassImplementsGenericInterface()
        {
            RunTest(original: """
                    using System;
                    namespace A
                    {
                        public class Foo<T> : System.Collections.ICollection, System.Collections.Generic.ICollection<T>
                        {
                            int System.Collections.Generic.ICollection<T>.Count => throw new NotImplementedException();
                            bool System.Collections.Generic.ICollection<T>.IsReadOnly => throw new NotImplementedException();
                            int System.Collections.ICollection.Count => throw new NotImplementedException();
                            bool System.Collections.ICollection.IsSynchronized => throw new NotImplementedException();
                            object System.Collections.ICollection.SyncRoot => throw new NotImplementedException();
                            void System.Collections.Generic.ICollection<T>.Add(T item) => throw new NotImplementedException();
                            void System.Collections.Generic.ICollection<T>.Clear() => throw new NotImplementedException();
                            bool System.Collections.Generic.ICollection<T>.Contains(T item) => throw new NotImplementedException();
                            void System.Collections.Generic.ICollection<T>.CopyTo(T[] array, int arrayIndex) => throw new NotImplementedException();
                            bool System.Collections.Generic.ICollection<T>.Remove(T item) => throw new NotImplementedException();
                            System.Collections.Generic.IEnumerator<T> System.Collections.Generic.IEnumerable<T>.GetEnumerator() => throw new NotImplementedException();
                            void System.Collections.ICollection.CopyTo(System.Array array, int index) => throw new NotImplementedException();
                            System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => throw new NotImplementedException();

                        }
                    }
                    
                    """,
                // https://github.com/dotnet/sdk/issues/32195 tracks interface expansion
                expected: """
                    namespace A
                    {
                        public partial class Foo<T> : System.Collections.ICollection, System.Collections.IEnumerable, System.Collections.Generic.ICollection<T>, System.Collections.Generic.IEnumerable<T>
                        {
                            int System.Collections.Generic.ICollection<T>.Count { get { throw null; } }
                            bool System.Collections.Generic.ICollection<T>.IsReadOnly { get { throw null; } }
                            int System.Collections.ICollection.Count { get { throw null; } }
                            bool System.Collections.ICollection.IsSynchronized { get { throw null; } }
                            object System.Collections.ICollection.SyncRoot { get { throw null; } }
                            void System.Collections.Generic.ICollection<T>.Add(T item) { }
                            void System.Collections.Generic.ICollection<T>.Clear() { }
                            bool System.Collections.Generic.ICollection<T>.Contains(T item) { throw null; }
                            void System.Collections.Generic.ICollection<T>.CopyTo(T[] array, int arrayIndex) { }
                            bool System.Collections.Generic.ICollection<T>.Remove(T item) { throw null; }
                            System.Collections.Generic.IEnumerator<T> System.Collections.Generic.IEnumerable<T>.GetEnumerator() { throw null; }
                            void System.Collections.ICollection.CopyTo(System.Array array, int index) { }
                            System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() { throw null; }
                        }
                    }
                    """,
                includeInternalSymbols: false);
        }

        [Fact]
        public void TestTypeForwardsToGenericTypesRegression31250()
        {
            RunTest(original: """
                    [assembly: System.Runtime.CompilerServices.TypeForwardedTo(typeof(System.Action))]
                    [assembly: System.Runtime.CompilerServices.TypeForwardedTo(typeof(System.Action<,>))]
                    [assembly: System.Runtime.CompilerServices.TypeForwardedTo(typeof(System.Action<,,>))]
                    [assembly: System.Runtime.CompilerServices.TypeForwardedTo(typeof(System.Action<,,,>))]
                    [assembly: System.Runtime.CompilerServices.TypeForwardedTo(typeof(System.Action<,,,,>))]
                    [assembly: System.Runtime.CompilerServices.TypeForwardedTo(typeof(System.Action<,,,,,>))]
                    [assembly: System.Runtime.CompilerServices.TypeForwardedTo(typeof(System.Action<,,,,,,>))]
                    [assembly: System.Runtime.CompilerServices.TypeForwardedTo(typeof(System.Action<,,,,,,,>))]
                    [assembly: System.Runtime.CompilerServices.TypeForwardedTo(typeof(System.Action<,,,,,,,,>))]
                    [assembly: System.Runtime.CompilerServices.TypeForwardedTo(typeof(System.ValueTuple))]
                    [assembly: System.Runtime.CompilerServices.TypeForwardedTo(typeof(System.ValueTuple<>))]
                    [assembly: System.Runtime.CompilerServices.TypeForwardedTo(typeof(System.ValueTuple<,>))]
                    [assembly: System.Runtime.CompilerServices.TypeForwardedTo(typeof(System.ValueTuple<,,>))]
                    [assembly: System.Runtime.CompilerServices.TypeForwardedTo(typeof(System.ValueTuple<,,,>))]
                    [assembly: System.Runtime.CompilerServices.TypeForwardedTo(typeof(System.ValueTuple<,,,,>))]
                    [assembly: System.Runtime.CompilerServices.TypeForwardedTo(typeof(System.ValueTuple<,,,,,>))]
                    [assembly: System.Runtime.CompilerServices.TypeForwardedTo(typeof(System.ValueTuple<,,,,,,>))]
                    [assembly: System.Runtime.CompilerServices.TypeForwardedTo(typeof(System.ValueTuple<,,,,,,,>))]
                    """,
                expected: """
                    [assembly: System.Runtime.CompilerServices.TypeForwardedTo(typeof(System.Action))]
                    [assembly: System.Runtime.CompilerServices.TypeForwardedTo(typeof(System.Action<,>))]
                    [assembly: System.Runtime.CompilerServices.TypeForwardedTo(typeof(System.Action<,,>))]
                    [assembly: System.Runtime.CompilerServices.TypeForwardedTo(typeof(System.Action<,,,>))]
                    [assembly: System.Runtime.CompilerServices.TypeForwardedTo(typeof(System.Action<,,,,>))]
                    [assembly: System.Runtime.CompilerServices.TypeForwardedTo(typeof(System.Action<,,,,,>))]
                    [assembly: System.Runtime.CompilerServices.TypeForwardedTo(typeof(System.Action<,,,,,,>))]
                    [assembly: System.Runtime.CompilerServices.TypeForwardedTo(typeof(System.Action<,,,,,,,>))]
                    [assembly: System.Runtime.CompilerServices.TypeForwardedTo(typeof(System.Action<,,,,,,,,>))]
                    [assembly: System.Runtime.CompilerServices.TypeForwardedTo(typeof(System.ValueTuple))]
                    [assembly: System.Runtime.CompilerServices.TypeForwardedTo(typeof(System.ValueTuple<>))]
                    [assembly: System.Runtime.CompilerServices.TypeForwardedTo(typeof(System.ValueTuple<,>))]
                    [assembly: System.Runtime.CompilerServices.TypeForwardedTo(typeof(System.ValueTuple<,,>))]
                    [assembly: System.Runtime.CompilerServices.TypeForwardedTo(typeof(System.ValueTuple<,,,>))]
                    [assembly: System.Runtime.CompilerServices.TypeForwardedTo(typeof(System.ValueTuple<,,,,>))]
                    [assembly: System.Runtime.CompilerServices.TypeForwardedTo(typeof(System.ValueTuple<,,,,,>))]
                    [assembly: System.Runtime.CompilerServices.TypeForwardedTo(typeof(System.ValueTuple<,,,,,,>))]
                    [assembly: System.Runtime.CompilerServices.TypeForwardedTo(typeof(System.ValueTuple<,,,,,,,>))]
                    """,
                includeInternalSymbols: false);
        }
    }
}
