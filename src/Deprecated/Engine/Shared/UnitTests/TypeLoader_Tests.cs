// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections;
using NUnit.Framework;
using Microsoft.Build.BuildEngine.Shared;

namespace Microsoft.Build.UnitTests
{
 [TestFixture]
 public class TypeLoader_Tests
 {
    [Test]
    public void Basic()
    {

        Assertion.Assert(TypeLoader.IsPartialTypeNameMatch("Csc", "csc")); // ==> exact match
        Assertion.Assert(TypeLoader.IsPartialTypeNameMatch("Microsoft.Build.Tasks.Csc", "Microsoft.Build.Tasks.Csc")); // ==> exact match
        Assertion.Assert(TypeLoader.IsPartialTypeNameMatch("Microsoft.Build.Tasks.Csc", "Csc")); // ==> partial match
        Assertion.Assert(TypeLoader.IsPartialTypeNameMatch("Microsoft.Build.Tasks.Csc", "Tasks.Csc")); // ==> partial match
        Assertion.Assert(TypeLoader.IsPartialTypeNameMatch("MyTasks.ATask+NestedTask", "NestedTask")); // ==> partial match
        Assertion.Assert(TypeLoader.IsPartialTypeNameMatch("MyTasks.ATask\\\\+NestedTask", "NestedTask")); // ==> partial match
        Assertion.Assert(!TypeLoader.IsPartialTypeNameMatch("MyTasks.CscTask", "Csc")); // ==> no match
        Assertion.Assert(!TypeLoader.IsPartialTypeNameMatch("MyTasks.MyCsc", "Csc")); // ==> no match
        Assertion.Assert(!TypeLoader.IsPartialTypeNameMatch("MyTasks.ATask\\.Csc", "Csc")); // ==> no match
        Assertion.Assert(!TypeLoader.IsPartialTypeNameMatch("MyTasks.ATask\\\\\\.Csc", "Csc")); // ==> no match
    }

     [Test]
     public void Regress_Mutation_TrailingPartMustMatch()
     {
         Assertion.Assert(!TypeLoader.IsPartialTypeNameMatch("Microsoft.Build.Tasks.Csc", "Vbc"));
     }

     [Test]
     public void Regress_Mutation_ParameterOrderDoesntMatter()
     {
         Assertion.Assert(TypeLoader.IsPartialTypeNameMatch("Csc", "Microsoft.Build.Tasks.Csc"));
     }

 }
}
