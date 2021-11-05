// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
namespace Microsoft.NET.StringTools
{
    public partial class SpanBasedStringBuilder : System.IDisposable
    {
        public SpanBasedStringBuilder(int capacity = 4) { }
        public SpanBasedStringBuilder(string str) { }
        public int Length { get { throw null; } }
        public void Clear() { }
        public void Dispose() { }
        public Microsoft.NET.StringTools.SpanBasedStringBuilder.Enumerator GetEnumerator() { throw null; }
        public override string ToString() { throw null; }
        [System.Runtime.InteropServices.StructLayoutAttribute(System.Runtime.InteropServices.LayoutKind.Sequential)]
        public partial struct Enumerator
        {
            private object _dummy;
            private int _dummyPrimitive;
            public Enumerator(System.Text.StringBuilder builder) { throw null; }
            public char Current { get { throw null; } }
            public bool MoveNext() { throw null; }
        }
    }
    public static partial class Strings
    {
        public static string CreateDiagnosticReport() { throw null; }
        public static void EnableDiagnostics() { }
        public static Microsoft.NET.StringTools.SpanBasedStringBuilder GetSpanBasedStringBuilder() { throw null; }
        public static string WeakIntern(string str) { throw null; }
    }
}
namespace System
{
    public static partial class MemoryExtensions
    {
        public static string AsSpan<T>(this T[] array, int start, int length) { throw null; }
    }
}
