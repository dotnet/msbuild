// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Execution;
using Microsoft.Build.Framework;
using Microsoft.Build.UnitTests;
using System.Linq;

#nullable disable

namespace Microsoft.Build.Engine.UnitTests.TestComparers
{
    internal sealed class TaskRegistryComparers
    {
        internal sealed class TaskRegistryComparer : IEqualityComparer<TaskRegistry>
        {
            public bool Equals(TaskRegistry x, TaskRegistry y)
            {
                Assert.AreEqual(x.Toolset, y.Toolset, new ToolsetComparer());
                Assert.AreEqual(x.NextRegistrationOrderId, y.NextRegistrationOrderId);

                Helpers.AssertDictionariesEqual(
                    x.TaskRegistrations,
                    y.TaskRegistrations,
                    (xp, yp) =>
                    {
                        Assert.AreEqual(xp.Key, yp.Key, TaskRegistry.RegisteredTaskIdentity.RegisteredTaskIdentityComparer.Exact);
                        Assert.IsTrue(xp.Value.SequenceEqual(yp.Value, new RegisteredTaskRecordComparer()));
                    });

                return true;
            }

            public int GetHashCode(TaskRegistry obj)
            {
                throw new NotImplementedException();
            }
        }

        internal sealed class RegisteredTaskRecordComparer : IEqualityComparer<TaskRegistry.RegisteredTaskRecord>
        {
            public bool Equals(TaskRegistry.RegisteredTaskRecord x, TaskRegistry.RegisteredTaskRecord y)
            {
                Assert.AreEqual(x.TaskIdentity, y.TaskIdentity, TaskRegistry.RegisteredTaskIdentity.RegisteredTaskIdentityComparer.Exact);
                Assert.AreEqual(x.RegisteredName, y.RegisteredName);
                Assert.AreEqual(x.TaskFactoryAssemblyLoadInfo, y.TaskFactoryAssemblyLoadInfo);
                Assert.AreEqual(x.TaskFactoryAttributeName, y.TaskFactoryAttributeName);
                Assert.AreEqual(x.ParameterGroupAndTaskBody, y.ParameterGroupAndTaskBody, new ParamterGroupAndTaskBodyComparer());

                // Assert TaskFactoryParameters equality
                var xParams = x.TaskFactoryParameters;
                var yParams = y.TaskFactoryParameters;
                Assert.AreEqual(xParams.Runtime, yParams.Runtime);
                Assert.AreEqual(xParams.Architecture, yParams.Architecture);
                Assert.AreEqual(xParams.DotnetHostPath, yParams.DotnetHostPath);
                Assert.AreEqual(xParams.MSBuildAssemblyPath, yParams.MSBuildAssemblyPath);
                Assert.AreEqual(xParams.TaskHostFactoryExplicitlyRequested, yParams.TaskHostFactoryExplicitlyRequested);

                return true;
            }

            public int GetHashCode(TaskRegistry.RegisteredTaskRecord obj)
            {
                throw new NotImplementedException();
            }
        }

        internal sealed class ParamterGroupAndTaskBodyComparer : IEqualityComparer<TaskRegistry.RegisteredTaskRecord.ParameterGroupAndTaskElementRecord>
        {
            public bool Equals(
                TaskRegistry.RegisteredTaskRecord.ParameterGroupAndTaskElementRecord x,
                TaskRegistry.RegisteredTaskRecord.ParameterGroupAndTaskElementRecord y)
            {
                Assert.AreEqual(x.InlineTaskXmlBody, y.InlineTaskXmlBody);
                Assert.AreEqual(x.TaskBodyEvaluated, y.TaskBodyEvaluated);

                Helpers.AssertDictionariesEqual(
                    x.UsingTaskParameters,
                    y.UsingTaskParameters,
                    (xp, yp) =>
                    {
                        Assert.AreEqual(xp.Key, yp.Key);
                        Assert.AreEqual(xp.Value, yp.Value, new TaskPropertyInfoComparer());
                    });

                return true;
            }

            public int GetHashCode(TaskRegistry.RegisteredTaskRecord.ParameterGroupAndTaskElementRecord obj)
            {
                throw new NotImplementedException();
            }
        }

        internal sealed class TaskPropertyInfoComparer : IEqualityComparer<TaskPropertyInfo>
        {
            public bool Equals(TaskPropertyInfo x, TaskPropertyInfo y)
            {
                Assert.AreEqual(x.Name, y.Name);
                Assert.AreEqual(x.Output, y.Output);
                Assert.AreEqual(x.Required, y.Required);
                Assert.AreEqual(x.PropertyType.FullName, y.PropertyType.FullName);

                return true;
            }

            public int GetHashCode(TaskPropertyInfo obj)
            {
                throw new NotImplementedException();
            }
        }

        public sealed class ToolsetComparer : IEqualityComparer<Toolset>
        {
            public bool Equals(Toolset x, Toolset y)
            {
                if (x == null || y == null)
                {
                    Assert.IsTrue(x == null && y == null);
                    return true;
                }

                Assert.AreEqual(x.ToolsVersion, y.ToolsVersion);
                Assert.AreEqual(x.ToolsPath, y.ToolsPath);
                Assert.AreEqual(x.DefaultOverrideToolsVersion, y.DefaultOverrideToolsVersion);
                Assert.AreEqual(x.OverrideTasksPath, y.OverrideTasksPath);

                Helpers.AssertDictionariesEqual(
                    x.Properties,
                    y.Properties,
                    (xp, yp) =>
                    {
                        Assert.AreEqual(xp.Key, yp.Key);
                        Assert.AreEqual(xp.Value.Name, yp.Value.Name);
                        Assert.AreEqual(xp.Value.EvaluatedValue, yp.Value.EvaluatedValue);
                    });

                return true;
            }

            public int GetHashCode(Toolset obj)
            {
                throw new NotImplementedException();
            }
        }
    }
}
