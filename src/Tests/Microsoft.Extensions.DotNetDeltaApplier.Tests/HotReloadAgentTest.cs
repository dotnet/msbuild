// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Reflection;
using Microsoft.Extensions.HotReload;
using Moq;

namespace Microsoft.Extensions.DotNetDeltaApplier
{
    public class HotReloadAgentTest
    {
        [Fact]
        public void TopologicalSort_Works()
        {
            // Arrange
            var assembly1 = GetAssembly("System.Private.CoreLib", Array.Empty<AssemblyName>());
            var assembly2 = GetAssembly("System.Text.Json", new[] { new AssemblyName("System.Private.CoreLib"), });
            var assembly3 = GetAssembly("Microsoft.AspNetCore.Components", new[] { new AssemblyName("System.Private.CoreLib"), });
            var assembly4 = GetAssembly("Microsoft.AspNetCore.Components.Web", new[] { new AssemblyName("Microsoft.AspNetCore.Components"), new AssemblyName("System.Text.Json"), });

            var sortedList = HotReloadAgent.TopologicalSort(new[] { assembly2, assembly4, assembly1, assembly3 });

            // Assert
            Assert.Equal(new[] { assembly1, assembly2, assembly3, assembly4 }, sortedList);
        }

        [Fact]
        public void TopologicalSort_IgnoresUnknownReferencedAssemblies()
        {
            // Arrange
            var assembly1 = GetAssembly("System.Private.CoreLib", Array.Empty<AssemblyName>());
            var assembly2 = GetAssembly("System.Text.Json", new[] { new AssemblyName("netstandard"), new AssemblyName("System.Private.CoreLib"), });
            var assembly3 = GetAssembly("Microsoft.AspNetCore.Components", new[] { new AssemblyName("System.Private.CoreLib"), new AssemblyName("Microsoft.Extensions.DependencyInjection"), });
            var assembly4 = GetAssembly("Microsoft.AspNetCore.Components.Web", new[] { new AssemblyName("Microsoft.AspNetCore.Components"), new AssemblyName("System.Text.Json"), });

            var sortedList = HotReloadAgent.TopologicalSort(new[] { assembly2, assembly4, assembly1, assembly3 });

            // Assert
            Assert.Equal(new[] { assembly1, assembly2, assembly3, assembly4 }, sortedList);
        }

        [Fact]
        public void TopologicalSort_WithCycles()
        {
            // Arrange
            var assembly1 = GetAssembly("System.Private.CoreLib", Array.Empty<AssemblyName>());
            var assembly2 = GetAssembly("System.Text.Json", new[] { new AssemblyName("System.Collections.Immutable"), new AssemblyName("System.Private.CoreLib"), });
            var assembly3 = GetAssembly("System.Collections.Immutable", new[] { new AssemblyName("System.Text.Json"), new AssemblyName("System.Private.CoreLib"), });
            var assembly4 = GetAssembly("Microsoft.AspNetCore.Components", new[] { new AssemblyName("System.Private.CoreLib"), new AssemblyName("Microsoft.Extensions.DependencyInjection"), });
            var assembly5 = GetAssembly("Microsoft.AspNetCore.Components.Web", new[] { new AssemblyName("Microsoft.AspNetCore.Components"), new AssemblyName("System.Text.Json"), });

            var sortedList = HotReloadAgent.TopologicalSort(new[] { assembly2, assembly4, assembly1, assembly3, assembly5 });

            // Assert
            Assert.Equal(new[] { assembly1, assembly3, assembly2, assembly4, assembly5 }, sortedList);
        }

        [Fact]
        public void GetHandlerActions_DiscoversActionsOnTypeWithClearCache()
        {
            // Arrange
            var actions = new HotReloadAgent.UpdateHandlerActions();
            var log = new List<string>();
            var agent = new HotReloadAgent(message => log.Add(message));

            agent.GetHandlerActions(actions, typeof(HandlerWithClearCache));

            Assert.Empty(log);
            Assert.Single(actions.ClearCache);
            Assert.Empty(actions.UpdateApplication);
        }

        [Fact]
        public void GetHandlerActions_DiscoversActionsOnTypeWithUpdateApplication()
        {
            // Arrange
            var actions = new HotReloadAgent.UpdateHandlerActions();
            var log = new List<string>();
            var agent = new HotReloadAgent(message => log.Add(message));

            agent.GetHandlerActions(actions, typeof(HandlerWithUpdateApplication));

            Assert.Empty(log);
            Assert.Empty(actions.ClearCache);
            Assert.Single(actions.UpdateApplication);
        }

        [Fact]
        public void GetHandlerActions_DiscoversActionsOnTypeWithBothActions()
        {
            // Arrange
            var actions = new HotReloadAgent.UpdateHandlerActions();
            var log = new List<string>();
            var agent = new HotReloadAgent(message => log.Add(message));

            agent.GetHandlerActions(actions, typeof(HandlerWithBothActions));

            Assert.Empty(log);
            Assert.Single(actions.ClearCache);
            Assert.Single(actions.UpdateApplication);
        }

        [Fact]
        public void GetHandlerActions_LogsMessageIfMethodHasIncorrectSignature()
        {
            // Arrange
            var actions = new HotReloadAgent.UpdateHandlerActions();
            var log = new List<string>();
            var agent = new HotReloadAgent(message => log.Add(message));
            var handlerType = typeof(HandlerWithIncorrectSignature);

            agent.GetHandlerActions(actions, handlerType);

            var message = Assert.Single(log);
            Assert.Equal($"Type '{handlerType}' has method 'Void ClearCache()' that does not match the required signature.", message);
            Assert.Empty(actions.ClearCache);
            Assert.Single(actions.UpdateApplication);
        }

        [Fact]
        public void GetHandlerActions_LogsMessageIfNoActionsAreDiscovered()
        {
            // Arrange
            var actions = new HotReloadAgent.UpdateHandlerActions();
            var log = new List<string>();
            var agent = new HotReloadAgent(message => log.Add(message));
            var handlerType = typeof(HandlerWithNoActions);

            agent.GetHandlerActions(actions, handlerType);

            var message = Assert.Single(log);
            Assert.Equal($"No invokable methods found on metadata handler type '{handlerType}'. " +
                    $"Allowed methods are ClearCache, UpdateApplication", message);
            Assert.Empty(actions.ClearCache);
            Assert.Empty(actions.UpdateApplication);
        }

        private static Assembly GetAssembly(string fullName, AssemblyName[] dependencies)
        {
            var assembly = new Mock<Assembly>();
            assembly.Setup(a => a.GetName()).Returns(new AssemblyName(fullName));
            assembly.SetupGet(a => a.FullName).Returns(fullName);
            assembly.Setup(a => a.GetReferencedAssemblies()).Returns(dependencies);
            assembly.Setup(a => a.ToString()).Returns(fullName);
            return assembly.Object;
        }

        private class HandlerWithClearCache
        {
            internal static void ClearCache(Type[]? _) { }
        }

        private class HandlerWithUpdateApplication
        {
            internal static void UpdateApplication(Type[]? _) { }
        }

        private class HandlerWithBothActions
        {
            internal static void ClearCache(Type[]? _) { }
            internal static void UpdateApplication(Type[]? _) { }
        }

        private class HandlerWithIncorrectSignature
        {
            internal static void ClearCache() { }
            internal static void UpdateApplication(Type[]? _) { }
        }

        private class HandlerWithNoActions
        {
            internal static void SomeMethod() { }
        }
    }
}
