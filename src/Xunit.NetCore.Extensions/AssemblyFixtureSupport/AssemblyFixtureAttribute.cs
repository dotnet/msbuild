// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

#nullable disable

namespace Xunit
{
    [AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
    public class AssemblyFixtureAttribute : Attribute
    {
        public AssemblyFixtureAttribute(Type fixtureType)
        {
            FixtureType = fixtureType;
        }

        public Type FixtureType { get; private set; }

        public Scope LifetimeScope { get; set; }

        public enum Scope
        {
            Assembly,
            Class,
            Method
        }
    }
}
