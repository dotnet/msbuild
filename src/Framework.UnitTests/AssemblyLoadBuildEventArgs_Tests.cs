// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using Shouldly;
using Xunit;

namespace Microsoft.Build.Framework.UnitTests
{
    public class AssemblyLoadBuildEventArgs_Tests
    {
        [Fact]
        public void SerializationDeserializationTest()
        {
            string assemblyName = Guid.NewGuid().ToString();
            string assemblyPath = Guid.NewGuid().ToString();
            Guid mvid = Guid.NewGuid();
            int appDomainId = new Random().Next();
            string appDomainName = Guid.NewGuid().ToString();
            AssemblyLoadingContext context =
                (AssemblyLoadingContext)(new Random().Next(Enum.GetNames(typeof(AssemblyLoadingContext)).Length));
            AssemblyLoadBuildEventArgs arg = new(context, assemblyName, assemblyPath, mvid, appDomainId, appDomainName);

            using MemoryStream stream = new MemoryStream();
            using BinaryWriter bw = new BinaryWriter(stream);
            arg.WriteToStream(bw);

            stream.Position = 0;
            using BinaryReader br = new BinaryReader(stream);
            AssemblyLoadBuildEventArgs argDeserialized = new();
            argDeserialized.CreateFromStream(br, 0);

            argDeserialized.AssemblyName.ShouldBe(assemblyName);
            argDeserialized.AssemblyPath.ShouldBe(assemblyPath);
            argDeserialized.MVID.ShouldBe(mvid);
            argDeserialized.AppDomainId.ShouldBe(appDomainId);
            argDeserialized.AppDomainFriendlyName.ShouldBe(appDomainName);
            argDeserialized.LoadingContext.ShouldBe(context);
        }
    }
}
