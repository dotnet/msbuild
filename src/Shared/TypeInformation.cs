// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Reflection;
#if !TASKHOST
using System.Reflection.Metadata;
#endif
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
        internal bool IsMarshalByRef { get; set; }
        internal bool ImplementsIGeneratedTask { get; set; }
        internal AssemblyName AssemblyName { get; set; }
        internal string Namespace { get; set; }
#if !TASKHOST
        internal TypeInformationPropertyInfo[] Properties { get; set; }
#endif

        internal TypeInformation()
        {
        }

        internal TypeInformation(LoadedType baseType)
        {
            LoadedType = baseType;
            HasSTAThreadAttribute = LoadedType.HasSTAThreadAttribute();
            HasLoadInSeparateAppDomainAttribute = LoadedType.HasLoadInSeparateAppDomainAttribute();
            IsMarshalByRef = LoadedType.Type.GetTypeInfo().IsMarshalByRef;
#if TASKHOST
            ImplementsIGeneratedTask = false;
#else
            ImplementsIGeneratedTask = LoadedType.Type is IGeneratedTask;
#endif
            AssemblyName = LoadedType.LoadedAssembly?.GetName();
            Namespace = LoadedType.Type.Namespace;
            LoadInfo = LoadedType.Assembly;
            TypeName = LoadedType.Type.FullName;
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

    internal struct TypeInformationPropertyInfo
    {
        public string Name { get; set; }
        public Type PropertyType { get; set; } = null;
        public bool OutputAttribute { get; set; }
        public bool RequiredAttribute { get; set; }
    }
}
