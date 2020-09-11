using MessagePack;
using MessagePack.Resolvers;
using Microsoft.Build.Framework;
using Microsoft.Build.Tasks.ResolveAssemblyReferences;
using Microsoft.Build.Tasks.ResolveAssemblyReferences.Client;
using Microsoft.Build.Tasks.ResolveAssemblyReferences.Contract;
using Microsoft.Build.Tasks.ResolveAssemblyReferences.Server;
using Microsoft.Build.Tasks.ResolveAssemblyReferences.Services;
using Microsoft.Build.UnitTests;
using Microsoft.Build.UnitTests.ResolveAssemblyReference_Tests;
using Microsoft.Build.Utilities;
using Nerdbank.Streams;
using Shouldly;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipelines;
using System.IO.Pipes;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;
using Task = System.Threading.Tasks.Task;

namespace Microsoft.Build.Tasks.UnitTests.AssemblyDependency
{
    public sealed class ResolveAssemblyReferenceAsAService_Tests : ResolveAssemblyReferenceTestFixture
    {
        public ResolveAssemblyReferenceAsAService_Tests(ITestOutputHelper output) : base(output)
        {
        }

        [Fact]
        public void EnsureInputPropertiesMatch()
        {
            string[] rarInputProperties = typeof(ResolveAssemblyReference).GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Where(p => !p.GetCustomAttributes(typeof(OutputAttribute), inherit: true).Any()).Select(p => $"{p.PropertyType.FullName}.{p.Name}").ToArray();
            string[] inputProperties = typeof(ResolveAssemblyReferenceTaskInput).GetProperties().Select(p => $"{p.PropertyType.FullName}.{p.Name}").ToArray();

            foreach (var item in rarInputProperties)
            {
                inputProperties.ShouldContain(item);
            }
        }

        [Fact]
        public void EnsureOutputPropertiesMatch()
        {
            string[] rarInputProperties = typeof(ResolveAssemblyReference).GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Where(p => p.GetCustomAttributes(typeof(OutputAttribute), true).Any()).Select(p => $"{p.PropertyType.FullName}.{p.Name}").ToArray();
            string[] inputProperties = typeof(ResolveAssemblyReferenceTaskOutput).GetProperties().Select(p => $"{p.PropertyType.FullName}.{p.Name}").ToArray();

            foreach (var item in rarInputProperties)
            {
                inputProperties.ShouldContain(item);
            }
        }
        [Fact]
        public void TransferredRequestEquals()
        {
            ITaskItem[] assemblyNames = new TaskItem[]
            {
                new TaskItem("DependsOnEverettSystem, Version=1.0.5000.0, Culture=neutral, PublicKeyToken=feedbeadbadcadbe")
            };

            ResolveAssemblyReference rar = new ResolveAssemblyReference
            {
                Assemblies = assemblyNames
            };

            MessagePackSerializerOptions options = MessagePackSerializerOptions.Standard.WithResolver(ResolveAssemblyReferneceResolver.Instance);

            ResolveAssemblyReferenceRequest request = new ResolveAssemblyReferenceRequest(rar.ResolveAssemblyReferenceInput);
            byte[] data = MessagePackSerializer.Serialize(request, options);

            ResolveAssemblyReferenceRequest requestDes = MessagePackSerializer.Deserialize<ResolveAssemblyReferenceRequest>(data, options);

            ResolveAssemblyReferenceComparer.CompareInput(request, requestDes).ShouldBeTrue();
        }


        [Fact]
        public void TransmitDataTest()
        {
            using CancellationTokenSource cts = new CancellationTokenSource();
            (Stream serverStream, Stream clientStream) = FullDuplexStream.CreatePair();
            MockEngine e = new MockEngine(_output)
            {
                ClientStream = clientStream
            };

            RarController controller = new RarController(string.Empty, null, null);
            Task serverTask = controller.HandleClientAsync(serverStream, cts.Token);
            RarClient client = new RarClient(e);
            ITaskItem[] assemblyNames = new TaskItem[]
            {
                new TaskItem("DependsOnEverettSystem, Version=1.0.5000.0, Culture=neutral, PublicKeyToken=feedbeadbadcadbe")
            };

            ResolveAssemblyReference rar = new ResolveAssemblyReference
            {
                Assemblies = assemblyNames
            };

            ResolveAssemblyReferenceRequest request = new ResolveAssemblyReferenceRequest(rar.ResolveAssemblyReferenceInput);
            ResolveAssemblyReferenceHandler handler = new ResolveAssemblyReferenceHandler();
            ResolveAssemblyReferenceResult expectedResult = handler.Execute(request);

            client.Connect();
            ResolveAssemblyReferenceResult result = client.Execute(rar.ResolveAssemblyReferenceInput);
            cts.Cancel();

            ResolveAssemblyReferenceComparer.CompareOutput(expectedResult.Response, result.Response).ShouldBeTrue();

            serverStream.Dispose();
            clientStream.Dispose();
        }
    }
}
