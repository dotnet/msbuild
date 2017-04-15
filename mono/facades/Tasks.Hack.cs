// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

/*
 * Csc/Vbc/ManagedCompiler were in Microsoft.Build.Tasks namespace in Microsoft.Build.Tasks.v* assembly
 * But moved to namespace Microsoft.CodeAnalysis.BuildTasks in Microsoft.Build.Tasks.CodeAnalysis.dll
 * Because of the namespace change, we cannot use typeforwarding
 */
namespace Microsoft.Build.Tasks {
    public abstract class ManagedCompiler : Microsoft.CodeAnalysis.BuildTasks.ManagedCompiler {}
    public class Csc : Microsoft.CodeAnalysis.BuildTasks.Csc {}
    public class Vbc : Microsoft.CodeAnalysis.BuildTasks.Vbc {}
}
