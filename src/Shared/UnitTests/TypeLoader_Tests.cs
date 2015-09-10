// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections;
using Microsoft.Build.Shared;
using System.Reflection;
using Xunit;

namespace Microsoft.Build.UnitTests
{
    public class TypeLoader_Tests
    {
        [Fact]
        public void Basic()
        {
            Assert.True(TypeLoader.IsPartialTypeNameMatch("Csc", "csc")); // ==> exact match
            Assert.True(TypeLoader.IsPartialTypeNameMatch("Microsoft.Build.Tasks.Csc", "Microsoft.Build.Tasks.Csc")); // ==> exact match
            Assert.True(TypeLoader.IsPartialTypeNameMatch("Microsoft.Build.Tasks.Csc", "Csc")); // ==> partial match
            Assert.True(TypeLoader.IsPartialTypeNameMatch("Microsoft.Build.Tasks.Csc", "Tasks.Csc")); // ==> partial match
            Assert.True(TypeLoader.IsPartialTypeNameMatch("MyTasks.ATask+NestedTask", "NestedTask")); // ==> partial match
            Assert.True(TypeLoader.IsPartialTypeNameMatch("MyTasks.ATask\\\\+NestedTask", "NestedTask")); // ==> partial match
            Assert.False(TypeLoader.IsPartialTypeNameMatch("MyTasks.CscTask", "Csc")); // ==> no match
            Assert.False(TypeLoader.IsPartialTypeNameMatch("MyTasks.MyCsc", "Csc")); // ==> no match
            Assert.False(TypeLoader.IsPartialTypeNameMatch("MyTasks.ATask\\.Csc", "Csc")); // ==> no match
            Assert.False(TypeLoader.IsPartialTypeNameMatch("MyTasks.ATask\\\\\\.Csc", "Csc")); // ==> no match
        }

        [Fact]
        public void Regress_Mutation_TrailingPartMustMatch()
        {
            Assert.False(TypeLoader.IsPartialTypeNameMatch("Microsoft.Build.Tasks.Csc", "Vbc"));
        }

        [Fact]
        public void Regress_Mutation_ParameterOrderDoesntMatter()
        {
            Assert.True(TypeLoader.IsPartialTypeNameMatch("Csc", "Microsoft.Build.Tasks.Csc"));
        }


        /// <summary>
        /// Make sure that when we load multiple types out of the same assembly with different typefilters that both the fullyqualified name matching and the 
        /// partial name matching still work.
        /// </summary>
        [Fact]
        public void Regress640476PartialName()
        {
            string forwardingLoggerLocation = typeof(Microsoft.Build.Logging.ConfigurableForwardingLogger).Assembly.Location;
            TypeLoader loader = new TypeLoader(new TypeFilter(IsForwardingLoggerClass));
            LoadedType loadedType = loader.Load("ConfigurableForwardingLogger", AssemblyLoadInfo.Create(null, forwardingLoggerLocation));
            Assert.NotNull(loadedType);
            Assert.True(loadedType.Assembly.AssemblyLocation.Equals(forwardingLoggerLocation, StringComparison.OrdinalIgnoreCase));

            string fileLoggerLocation = typeof(Microsoft.Build.Logging.FileLogger).Assembly.Location;
            loader = new TypeLoader(new TypeFilter(IsLoggerClass));
            loadedType = loader.Load("FileLogger", AssemblyLoadInfo.Create(null, fileLoggerLocation));
            Assert.NotNull(loadedType);
            Assert.True(loadedType.Assembly.AssemblyLocation.Equals(fileLoggerLocation, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Make sure that when we load multiple types out of the same assembly with different typefilters that both the fullyqualified name matching and the 
        /// partial name matching still work.
        /// </summary>
        [Fact]
        public void Regress640476FullyQualifiedName()
        {
            Type forwardingLoggerType = typeof(Microsoft.Build.Logging.ConfigurableForwardingLogger);
            string forwardingLoggerLocation = forwardingLoggerType.Assembly.Location;
            TypeLoader loader = new TypeLoader(new TypeFilter(IsForwardingLoggerClass));
            LoadedType loadedType = loader.Load(forwardingLoggerType.FullName, AssemblyLoadInfo.Create(null, forwardingLoggerLocation));
            Assert.NotNull(loadedType);
            Assert.True(loadedType.Assembly.AssemblyLocation.Equals(forwardingLoggerLocation, StringComparison.OrdinalIgnoreCase));

            Type fileLoggerType = typeof(Microsoft.Build.Logging.FileLogger);
            string fileLoggerLocation = fileLoggerType.Assembly.Location;
            loader = new TypeLoader(new TypeFilter(IsLoggerClass));
            loadedType = loader.Load(fileLoggerType.FullName, AssemblyLoadInfo.Create(null, fileLoggerLocation));
            Assert.NotNull(loadedType);
            Assert.True(loadedType.Assembly.AssemblyLocation.Equals(fileLoggerLocation, StringComparison.OrdinalIgnoreCase));
        }


        /// <summary>
        /// Make sure if no typeName is passed in then pick the first type which matches the desired typefilter.
        /// This has been in since whidbey but there has been no test for it and it was broken in the last refactoring of TypeLoader.
        /// This test is to prevent that from happening again.
        /// </summary>
        [Fact]
        public void NoTypeNamePicksFirstType()
        {
            Type forwardingLoggerType = typeof(Microsoft.Build.Logging.ConfigurableForwardingLogger);
            string forwardingLoggerAssemblyLocation = forwardingLoggerType.Assembly.Location;
            TypeFilter forwardingLoggerfilter = new TypeFilter(IsForwardingLoggerClass);
            Type firstPublicType = FirstPublicDesiredType(forwardingLoggerfilter, forwardingLoggerAssemblyLocation);

            TypeLoader loader = new TypeLoader(forwardingLoggerfilter);
            LoadedType loadedType = loader.Load(String.Empty, AssemblyLoadInfo.Create(null, forwardingLoggerAssemblyLocation));
            Assert.NotNull(loadedType);
            Assert.True(loadedType.Assembly.AssemblyLocation.Equals(forwardingLoggerAssemblyLocation, StringComparison.OrdinalIgnoreCase));
            Assert.True(loadedType.Type.Equals(firstPublicType));


            Type fileLoggerType = typeof(Microsoft.Build.Logging.FileLogger);
            string fileLoggerAssemblyLocation = forwardingLoggerType.Assembly.Location;
            TypeFilter fileLoggerfilter = new TypeFilter(IsLoggerClass);
            firstPublicType = FirstPublicDesiredType(fileLoggerfilter, fileLoggerAssemblyLocation);

            loader = new TypeLoader(fileLoggerfilter);
            loadedType = loader.Load(String.Empty, AssemblyLoadInfo.Create(null, fileLoggerAssemblyLocation));
            Assert.NotNull(loadedType);
            Assert.True(loadedType.Assembly.AssemblyLocation.Equals(fileLoggerAssemblyLocation, StringComparison.OrdinalIgnoreCase));
            Assert.True(loadedType.Type.Equals(firstPublicType));
        }


        private static Type FirstPublicDesiredType(TypeFilter filter, string assemblyLocation)
        {
            Assembly loadedAssembly = Assembly.UnsafeLoadFrom(assemblyLocation);

            // only look at public types
            Type[] allPublicTypesInAssembly = loadedAssembly.GetExportedTypes();
            foreach (Type publicType in allPublicTypesInAssembly)
            {
                if (filter(publicType, null))
                {
                    return publicType;
                }
            }

            return null;
        }


        private static bool IsLoggerClass(Type type, object unused)
        {
            return (type.IsClass &&
                !type.IsAbstract &&
                (type.GetInterface("ILogger") != null));
        }

        private static bool IsForwardingLoggerClass(Type type, object unused)
        {
            return (type.IsClass &&
                !type.IsAbstract &&
                (type.GetInterface("IForwardingLogger") != null));
        }
    }
}
