// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Reflection;
using Microsoft.Build.Framework;

namespace Microsoft.Build.Shared
{
    internal sealed class TypeInformation
    {
        internal AssemblyLoadInfo LoadInfo { get; set; }
        internal string TypeName { get; set; }

        internal LoadedType LoadedType { get; set; }

        internal bool HasSTAThreadAttribute { get; set; }
        internal bool HasLoadInSeparateAppDomainAttribute { get; set; }
        internal bool IsMarshallByRef { get; set; }
        internal bool ImplementsIGeneratedTask { get; set; }
        internal AssemblyName AssemblyName { get; set; }

        internal TypeInformation()
        {
        }

        internal TypeInformation(LoadedType baseType)
        {
            LoadedType = baseType;
            HasSTAThreadAttribute = LoadedType.HasSTAThreadAttribute();
            HasLoadInSeparateAppDomainAttribute = LoadedType.HasLoadInSeparateAppDomainAttribute();
            IsMarshallByRef = LoadedType.Type.GetTypeInfo().IsMarshalByRef;
#if TASKHOST
            ImplementsIGeneratedTask = false;
#else
            ImplementsIGeneratedTask = LoadedType.Type is IGeneratedTask;
#endif
            AssemblyName = LoadedType.LoadedAssembly.GetName();
        }

        public PropertyInfo[] GetProperties(BindingFlags flags)
        {
            if (LoadedType is null)
            {
                throw new NotImplementedException();
            }
            else
            {
                return LoadedType.Type.GetProperties(flags);
            }
        }

        public PropertyInfo GetProperty(string name, BindingFlags flags)
        {
            if (LoadedType is null)
            {
                throw new NotImplementedException();
            }
            else
            {
                return LoadedType.Type.GetProperty(name, flags);
            }
        }
    }
}