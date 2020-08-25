using MessagePack;
using MessagePack.Resolvers;
using Microsoft.Build.Framework;
using Microsoft.Build.Tasks.ResolveAssemblyReferences.Client;
using Microsoft.Build.Tasks.ResolveAssemblyReferences.Contract;
using Microsoft.Build.Tasks.ResolveAssemblyReferences.Server;
using Microsoft.Build.Tasks.ResolveAssemblyReferences.Services;
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

using Task = System.Threading.Tasks.Task;

namespace Microsoft.Build.Tasks.UnitTests.AssemblyDependency
{
    public sealed class ResolveAssemblyReferenceAsAService_Tests
    {
        [Fact]
        public void EnsureInputPropertiesMatch()
        {
            string[] rarInputProperties = typeof(ResolveAssemblyReference).GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Where(p => !p.GetCustomAttributes(typeof(OutputAttribute), true).Any()).Select(p => $"{p.PropertyType.FullName}.{p.Name}").ToArray();
            string[] inputProperties = typeof(ResolveAssemblyReferenceTaskInput).GetProperties().Select(p => $"{p.PropertyType.FullName}.{p.Name}").ToArray();

            Assert.Equal(rarInputProperties.Length, inputProperties.Length);
            foreach (var item in rarInputProperties)
            {
                Assert.Contains(item, inputProperties);
            }
        }

        [Fact]
        public void EnsureOutputPropertiesMatch()
        {
            string[] rarInputProperties = typeof(ResolveAssemblyReference).GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Where(p => p.GetCustomAttributes(typeof(OutputAttribute), true).Any()).Select(p => $"{p.PropertyType.FullName}.{p.Name}").ToArray();
            string[] inputProperties = typeof(ResolveAssemblyReferenceTaskOutput).GetProperties().Select(p => $"{p.PropertyType.FullName}.{p.Name}").ToArray();

            Assert.Equal(rarInputProperties.Length, inputProperties.Length);
            foreach (var item in rarInputProperties)
            {
                Assert.Contains(item, inputProperties);
            }
        }

        [Fact]
        public void TransferedRequestEquals()
        {
            ITaskItem[] assemblyNames = new TaskItem[]
            {
                new TaskItem("DependsOnEverettSystem, Version=1.0.5000.0, Culture=neutral, PublicKeyToken=feedbeadbadcadbe")
            };

            ResolveAssemblyReference rar = new ResolveAssemblyReference
            {
                Assemblies = assemblyNames
            };
            ResolveAssemblyReferenceRequest request = new ResolveAssemblyReferenceRequest(rar.ResolveAssemblyReferenceInput);
            byte[] data = MessagePackSerializer.Serialize(request);
            ResolveAssemblyReferenceRequest requestDes = MessagePackSerializer.Deserialize<ResolveAssemblyReferenceRequest>(data);

            Assert.Equal(request, requestDes, RARRequestComparer.Instance);
        }


        [Fact]
        public void TransmitDataTest()
        {
            using CancellationTokenSource cts = new CancellationTokenSource();
            (Stream serverStream, Stream clientStream) = FullDuplexStream.CreatePair();

            RarController controller = new RarController(string.Empty, GetPipe);
            Task serverTask = controller.HandleClientAsync(serverStream, cts.Token);
            RarClient client = new RarClient(new RarTestEngine(clientStream));
            ITaskItem[] assemblyNames = new TaskItem[]
            {
                new TaskItem("DependsOnEverettSystem, Version=1.0.5000.0, Culture=neutral, PublicKeyToken=feedbeadbadcadbe")
            };

            ResolveAssemblyReference rar = new ResolveAssemblyReference();
            rar.Assemblies = assemblyNames;
            ResolveAssemblyReferenceRequest request = new ResolveAssemblyReferenceRequest(rar.ResolveAssemblyReferenceInput);
            ResolveAssemblyReferenceTaskHandler handler = new ResolveAssemblyReferenceTaskHandler();
            ResolveAssemblyReferenceResult expectedResult = handler.Execute(request);

            client.Connect();
            ResolveAssemblyReferenceResult result = client.Execute(rar.ResolveAssemblyReferenceInput);
            cts.Cancel();

            Assert.Equal(expectedResult, result, RARResultComparer.Instance);

            serverStream.Dispose();
            clientStream.Dispose();
        }

        private NamedPipeServerStream GetPipe(string pipeName, int? arg2, int? arg3, int arg4, bool arg5)
        {
            throw new NotSupportedException();
        }

        class RarTestEngine : IRarBuildEngine
        {
            public Stream ClientStream { get; }

            public RarTestEngine(Stream clientStream)
            {
                ClientStream = clientStream;
            }

            bool IRarBuildEngine.CreateRarNode()
            {
                throw new NotImplementedException();
            }

            Stream IRarBuildEngine.GetRarClientStream(string pipeName, int timeout)
            {
                return ClientStream;
            }

            string IRarBuildEngine.GetRarPipeName()
            {
                return string.Empty;
            }
        }
    }
}
