// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using BenchmarkDotNet.Attributes;
using Microsoft.Build.Evaluation.Context;

namespace MSBuild.Benchmarks;

/// <summary>
/// Measures filesystem validation of unchanged and stale evaluation inputs.
/// Evaluation and input capture happen only during setup.
/// </summary>
[MemoryDiagnoser]
public class EvaluationInputValidationBenchmark
{
    private EvaluationInputBenchmarkFixture _fixture = null!;
    private string? _importPath;
    private string? _globDirectory;
    private string? _globMemberPath;
    private DateTime _projectWriteTime;
    private DateTime _importWriteTime;
    private bool _globMemberCreated;

    [ParamsSource(nameof(ProjectPaths))]
    public string ProjectPath { get; set; } = EvaluationInputBenchmarkFixture.SyntheticProject;

    public static IEnumerable<string> ProjectPaths => EvaluationInputBenchmarkFixture.ProjectPaths;

    [GlobalSetup]
    public void GlobalSetup()
    {
        _fixture = new EvaluationInputBenchmarkFixture(ProjectPath);
        _fixture.RecordInputs();
        _importPath = FindNearestImport();
        _globDirectory = FindShallowestDirectoryUnderProject();
        if (_globDirectory is not null)
        {
            _globMemberPath = Path.Combine(_globDirectory, $"evaluation-inputs-benchmark-{Guid.NewGuid():N}.tmp");
        }
        Console.WriteLine($"// stale import {_importPath}, glob directory {_globDirectory}");
    }

    [GlobalCleanup]
    public void GlobalCleanup()
    {
        try
        {
            RemoveGlobMember();
        }
        finally
        {
            EvaluationInputBenchmarkFixture.SetRecording(enabled: false);
            _fixture?.Dispose();
        }
    }

    [IterationSetup(Target = nameof(ValidateStaleProjectFile))]
    public void TouchProjectFile() => _projectWriteTime = Touch(_fixture.ProjectPath);

    [IterationCleanup(Target = nameof(ValidateStaleProjectFile))]
    public void RestoreProjectFile() => File.SetLastWriteTimeUtc(_fixture.ProjectPath, _projectWriteTime);

    [IterationSetup(Target = nameof(ValidateStaleImportedFile))]
    public void TouchImportedFile() => _importWriteTime = Touch(ImportPath);

    [IterationCleanup(Target = nameof(ValidateStaleImportedFile))]
    public void RestoreImportedFile() => File.SetLastWriteTimeUtc(ImportPath, _importWriteTime);

    [IterationSetup(Target = nameof(ValidateStaleGlobMembership))]
    public void AddGlobMember()
    {
        using FileStream stream = new(GlobMemberPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        _globMemberCreated = true;
    }

    [IterationCleanup(Target = nameof(ValidateStaleGlobMembership))]
    public void RemoveGlobMember()
    {
        if (_globMemberCreated)
        {
            File.Delete(GlobMemberPath);
            _globMemberCreated = false;
        }
    }

    /// <summary>Unchanged file/directory validation only; environment reads, SDK results, registry reads, and request identity are not checked.</summary>
    [Benchmark]
    public bool ValidateUnchanged() => Validate();

    /// <summary>Validation after the project file's timestamp moved; the result is false.</summary>
    [Benchmark]
    public bool ValidateStaleProjectFile() => Validate();

    /// <summary>Validation after the timestamp of the recorded import nearest the project moved; the result is false.</summary>
    [Benchmark]
    public bool ValidateStaleImportedFile() => Validate();

    /// <summary>Validation after a file appeared in a directory a glob traversed, which moves the directory's timestamp; the result is false.</summary>
    [Benchmark]
    public bool ValidateStaleGlobMembership() => Validate();

    private bool Validate() => EvaluationInputValidator.IsFileSystemCurrent(_fixture.Inputs, out _);

    private string ImportPath =>
        _importPath ?? throw new NotSupportedException($"{_fixture.ProjectPath} records no imported .props or .targets file.");

    private string GlobMemberPath =>
        _globMemberPath ?? throw new NotSupportedException($"{_fixture.ProjectPath} records no directory under the project.");

    /// <summary>
    /// Moves a file's timestamp forward by two seconds and returns the original, so file systems with coarse timestamps see a change.
    /// </summary>
    private static DateTime Touch(string path)
    {
        DateTime original = File.GetLastWriteTimeUtc(path);
        File.SetLastWriteTimeUtc(path, original.AddSeconds(2));
        return original;
    }

    /// <summary>
    /// The recorded .props or .targets file sharing the longest path prefix with the project: the import a developer edits,
    /// not one inside the SDK.
    /// </summary>
    private string? FindNearestImport()
    {
        string? nearest = null;
        int longestShared = -1;
        foreach (KeyValuePair<string, FileDependency> file in _fixture.Inputs.Files)
        {
            string extension = Path.GetExtension(file.Key);
            if (file.Value.Kind != PathKind.File
                || string.Equals(file.Key, _fixture.ProjectPath, StringComparison.OrdinalIgnoreCase)
                || !(extension.Equals(".props", StringComparison.OrdinalIgnoreCase) || extension.Equals(".targets", StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            int shared = 0;
            while (shared < file.Key.Length && shared < _fixture.ProjectPath.Length && char.ToUpperInvariant(file.Key[shared]) == char.ToUpperInvariant(_fixture.ProjectPath[shared]))
            {
                shared++;
            }

            if (shared > longestShared)
            {
                longestShared = shared;
                nearest = file.Key;
            }
        }

        return nearest;
    }

    /// <summary>
    /// The shallowest recorded directory at or below the project directory, which a glob traversed.
    /// </summary>
    private string? FindShallowestDirectoryUnderProject()
    {
        string projectDirectory = Path.GetDirectoryName(_fixture.ProjectPath)!;
        string? shallowest = null;
        foreach (KeyValuePair<string, FileDependency> file in _fixture.Inputs.Files)
        {
            bool underProject = string.Equals(file.Key, projectDirectory, StringComparison.OrdinalIgnoreCase)
                || file.Key.StartsWith(projectDirectory + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
            if (file.Value.Kind == PathKind.Directory && underProject && (shallowest is null || file.Key.Length < shallowest.Length))
            {
                shallowest = file.Key;
            }
        }

        return shallowest;
    }
}
