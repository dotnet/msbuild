// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text;

namespace Microsoft.Build.TaskAuthoring.Analyzer.Benchmarks;

internal static class AnalyzerScenarios
{
    public static readonly AnalyzerScenario[] Analyzers =
    [
        new(
            nameof(MultiThreadableTaskAnalyzer),
            new MultiThreadableTaskAnalyzer(),
            "MSBuildTask0001"),
        new(
            nameof(TransitiveCallChainAnalyzer),
            new TransitiveCallChainAnalyzer(),
            "MSBuildTask0005"),
        new(
            nameof(PreferTypedParameterAnalyzer),
            new PreferTypedParameterAnalyzer(),
            "MSBuildTask0006"),
        new(
            nameof(UnsupportedTaskItemTypeAnalyzer),
            new UnsupportedTaskItemTypeAnalyzer(),
            "MSBuildTask0009"),
        new(
            nameof(RequiredTaskPropertyInitializationSuppressor),
            new RequiredTaskPropertyInitializationSuppressor(),
            "CS8618",
            includeCompilerDiagnostics: true),
    ];

    public static readonly AnalyzerDiagnosticScenario[] DiagnosticScenarios =
    [
        new("MSBuildTask0001", new MultiThreadableTaskAnalyzer(), AnalyzerSourceFactory.CreateCriticalErrorCalls),
        new("MSBuildTask0002", new MultiThreadableTaskAnalyzer(), AnalyzerSourceFactory.CreateTaskEnvironmentCalls),
        new("MSBuildTask0003", new MultiThreadableTaskAnalyzer(), AnalyzerSourceFactory.CreateRelativePathCalls),
        new("MSBuildTask0004", new MultiThreadableTaskAnalyzer(), AnalyzerSourceFactory.CreatePotentialIssueCalls),
        new("MSBuildTask0005", new TransitiveCallChainAnalyzer(), AnalyzerSourceFactory.CreateTransitiveCalls),
        new("MSBuildTask0006", new PreferTypedParameterAnalyzer(), AnalyzerSourceFactory.CreateTypedPathCandidates),
        new("MSBuildTask0007", new PreferTypedParameterAnalyzer(), AnalyzerSourceFactory.CreateTypedItemCandidates),
        new("MSBuildTask0008", new PreferTypedParameterAnalyzer(), AnalyzerSourceFactory.CreateRelativeDefaultCandidates),
        new("MSBuildTask0009", new UnsupportedTaskItemTypeAnalyzer(), AnalyzerSourceFactory.CreateUnsupportedItemTypes),
    ];
}

internal static class AnalyzerSourceFactory
{
    public const string CompliantTask = """
        public static class BenchmarkHelper
        {
            public static bool Run(string value) => System.Math.Abs(value.Length) >= 0;
        }

        [Microsoft.Build.Framework.MSBuildMultiThreadableTask]
        public sealed class BenchmarkTask : Microsoft.Build.Utilities.Task
        {
            [Microsoft.Build.Framework.Required]
            public string Input { get; set; } = "";

            public Microsoft.Build.Framework.ITaskItem Item { get; set; } = null!;

            public override bool Execute() => BenchmarkHelper.Run(Input);
        }
        """;

    public const string FrameworkStubs = """
        namespace Microsoft.Build.Framework
        {
            public interface IBuildEngine { }

            public interface ITask
            {
                IBuildEngine BuildEngine { get; set; }
                bool Execute();
            }

            public interface IMultiThreadableTask : ITask
            {
                TaskEnvironment TaskEnvironment { get; set; }
            }

            public sealed class TaskEnvironment
            {
                public AbsolutePath ProjectDirectory => default;
                public string? GetEnvironmentVariable(string name) => null;
                public AbsolutePath GetAbsolutePath(string path) => default;
            }

            public readonly struct AbsolutePath
            {
                public AbsolutePath(string path) { }
            }

            public interface ITaskItem
            {
                string ItemSpec { get; set; }
            }

            public interface ITaskItem<T> : ITaskItem
            {
                T Value { get; }
            }

            [System.AttributeUsage(System.AttributeTargets.Class, Inherited = false)]
            public sealed class MSBuildMultiThreadableTaskAttribute : System.Attribute { }

            [System.AttributeUsage(System.AttributeTargets.Class)]
            public sealed class MSBuildMultiThreadableTaskAnalyzedAttribute : System.Attribute { }

            [System.AttributeUsage(System.AttributeTargets.Property)]
            public sealed class OutputAttribute : System.Attribute { }

            [System.AttributeUsage(System.AttributeTargets.Property)]
            public sealed class RequiredAttribute : System.Attribute { }
        }

        namespace Microsoft.Build.Utilities
        {
            public abstract class Task : Microsoft.Build.Framework.ITask
            {
                public Microsoft.Build.Framework.IBuildEngine BuildEngine { get; set; } = null!;
                public abstract bool Execute();
            }
        }
        """;

    public static string CreateCriticalErrorCalls(int count) =>
        CreateTaskClasses(
            count,
            "System.Console.WriteLine(\"benchmark\");",
            additionalMembers: null);

    public static string CreateTaskEnvironmentCalls(int count) =>
        CreateTaskClasses(
            count,
            "System.Environment.GetEnvironmentVariable(\"BENCHMARK\");",
            additionalMembers: null);

    public static string CreateRelativePathCalls(int count) =>
        CreateTaskClasses(
            count,
            "System.IO.File.Exists(\"relative.txt\");",
            additionalMembers: null);

    public static string CreatePotentialIssueCalls(int count) =>
        CreateTaskClasses(
            count,
            "System.Reflection.Assembly.LoadFrom(\"plugin.dll\");",
            additionalMembers: null);

    public static string CreateTypedPathCandidates(int count) =>
        CreateTaskClasses(
            count,
            "var absolutePath = new Microsoft.Build.Framework.AbsolutePath(InputPath);",
            "public string InputPath { get; set; } = \"\";",
            addMultiThreadableAttribute: true);

    public static string CreateTypedItemCandidates(int count) =>
        CreateTaskClasses(
            count,
            "var absolutePath = new Microsoft.Build.Framework.AbsolutePath(Input.ItemSpec);",
            "public Microsoft.Build.Framework.ITaskItem Input { get; set; } = null!;",
            addMultiThreadableAttribute: true);

    public static string CreateRelativeDefaultCandidates(int count) =>
        CreateTaskClasses(
            count,
            "var absolutePath = new Microsoft.Build.Framework.AbsolutePath(InputPath);",
            "public string InputPath { get; set; } = \"obj\";",
            addMultiThreadableAttribute: true);

    public static string CreateUnsupportedItemTypes(int count) =>
        CreateTaskClasses(
            count,
            operation: null,
            "public Microsoft.Build.Framework.ITaskItem<int> Input { get; set; } = null!;");

    public static string CreateTransitiveCalls(int count)
    {
        StringBuilder source = new();
        for (int i = 0; i < count; i++)
        {
            source.AppendLine($$"""
                public static class BenchmarkHelper{{i}}
                {
                    public static void Run() => System.Console.WriteLine("benchmark");
                }

                public sealed class BenchmarkTask{{i}} : Microsoft.Build.Utilities.Task
                {
                    public override bool Execute()
                    {
                        BenchmarkHelper{{i}}.Run();
                        return true;
                    }
                }

                """);
        }

        return source.ToString();
    }

    public static string CreateRequiredProperties(int count)
    {
        StringBuilder source = new(
            """
            public sealed class BenchmarkTask : Microsoft.Build.Utilities.Task
            {

            """);

        for (int i = 0; i < count; i++)
        {
            source.AppendLine($$"""
                    [Microsoft.Build.Framework.Required]
                    public string Input{{i}} { get; set; }

                """);
        }

        source.AppendLine(
            """
                public override bool Execute() => true;
            }
            """);

        return source.ToString();
    }

    private static string CreateTaskClasses(
        int count,
        string? operation,
        string? additionalMembers,
        bool addMultiThreadableAttribute = false)
    {
        StringBuilder source = new();
        for (int i = 0; i < count; i++)
        {
            if (addMultiThreadableAttribute)
            {
                source.AppendLine("[Microsoft.Build.Framework.MSBuildMultiThreadableTask]");
            }

            source.AppendLine($"public sealed class BenchmarkTask{i} : Microsoft.Build.Utilities.Task");
            source.AppendLine("{");
            if (additionalMembers is not null)
            {
                source.Append("    ");
                source.AppendLine(additionalMembers);
            }

            source.AppendLine("    public override bool Execute()");
            source.AppendLine("    {");
            if (operation is not null)
            {
                source.Append("        ");
                source.AppendLine(operation);
            }

            source.AppendLine("        return true;");
            source.AppendLine("    }");
            source.AppendLine("}");
        }

        return source.ToString();
    }
}
