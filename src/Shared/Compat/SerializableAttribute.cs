// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Runtime.InteropServices;

namespace System
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Enum | AttributeTargets.Delegate, Inherited = false)]
    [ComVisible(true)]
    internal sealed class SerializableAttribute : Attribute
    {
        public SerializableAttribute() { }
    }

    [AttributeUsage(AttributeTargets.Field, Inherited = false)]
    [ComVisible(true)]
    internal sealed class NonSerializedAttribute : Attribute
    {
        public NonSerializedAttribute() { }
    }
}