// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Execution;
using Microsoft.Build.Framework;
using Microsoft.Build.UnitTests;
using Xunit;

#nullable disable

namespace Microsoft.Build.Engine.UnitTests.TestComparers
{
    internal sealed class TaskRegistryComparers
    {
        internal sealed class TaskRegistryComparer : IEqualityComparer<TaskRegistry>
        {
            public bool Equals(TaskRegistry x, TaskRegistry y)
            {
                Assert.Equal(x.Toolset, y.Toolset, new ToolsetComparer());
                Assert.Equal(x.NextRegistrationOrderId, y.NextRegistrationOrderId);

                Helpers.AssertDictionariesEqual(
                    x.TaskRegistrations,
                    y.TaskRegistrations,
                    (xp, yp) =>
                    {
                        Assert.Equal(xp.Key, yp.Key, TaskRegistry.RegisteredTaskIdentity.RegisteredTaskIdentityComparer.Exact);
                        Assert.Equal(xp.Value, yp.Value, new RegisteredTaskRecordComparer());
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
                Assert.Equal(x.TaskIdentity, y.TaskIdentity, TaskRegistry.RegisteredTaskIdentity.RegisteredTaskIdentityComparer.Exact);
                Assert.Equal(x.RegisteredName, y.RegisteredName);
                Assert.Equal(x.TaskFactoryAssemblyLoadInfo, y.TaskFactoryAssemblyLoadInfo);
                Assert.Equal(x.TaskFactoryAttributeName, y.TaskFactoryAttributeName);
                Assert.Equal(x.ParameterGroupAndTaskBody, y.ParameterGroupAndTaskBody, new ParamterGroupAndTaskBodyComparer());

                Helpers.AssertDictionariesEqual(
                    x.TaskFactoryParameters,
                    y.TaskFactoryParameters,
                    (xp, yp) =>
                    {
                        Assert.Equal(xp.Key, yp.Key);
                        Assert.Equal(xp.Value, yp.Value);
                    });

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
                Assert.Equal(x.InlineTaskXmlBody, y.InlineTaskXmlBody);
                Assert.Equal(x.TaskBodyEvaluated, y.TaskBodyEvaluated);

                Helpers.AssertDictionariesEqual(
                    x.UsingTaskParameters,
                    y.UsingTaskParameters,
                    (xp, yp) =>
                    {
                        Assert.Equal(xp.Key, yp.Key);
                        Assert.Equal(xp.Value, yp.Value, new TaskPropertyInfoComparer());
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
                Assert.Equal(x.Name, y.Name);
                Assert.Equal(x.Output, y.Output);
                Assert.Equal(x.Required, y.Required);
                Assert.Equal(x.PropertyType.FullName, y.PropertyType.FullName);

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
                    Assert.True(x == null && y == null);
                    return true;
                }

                Assert.Equal(x.ToolsVersion, y.ToolsVersion);
                Assert.Equal(x.ToolsPath, y.ToolsPath);
                Assert.Equal(x.DefaultOverrideToolsVersion, y.DefaultOverrideToolsVersion);
                Assert.Equal(x.OverrideTasksPath, y.OverrideTasksPath);

                Helpers.AssertDictionariesEqual(
                    x.Properties,
                    y.Properties,
                    (xp, yp) =>
                    {
                        Assert.Equal(xp.Key, yp.Key);
                        Assert.Equal(xp.Value.Name, yp.Value.Name);
                        Assert.Equal(xp.Value.EvaluatedValue, yp.Value.EvaluatedValue);
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
