// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using Microsoft.Build.BackEnd.Logging;
using Microsoft.Build.Collections;
using Microsoft.Build.Execution;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;

namespace Microsoft.Build.BackEnd;

/// <summary>
/// One bound task invocation, independent of target incremental analysis and output destinations
/// in the project. Only raw requested task outputs and explicitly declared file artifacts are cached.
/// </summary>
internal sealed class TaskInvocationCache
{
    private readonly TaskCacheStore _store;
    private readonly byte[] _invocation;
    private readonly List<string> _inputs;
    private readonly List<string> _outputs;
    private readonly Dictionary<string, TaskPropertyInfo> _requestedOutputs;
    private readonly Dictionary<string, byte[]> _values = new(MSBuildNameIgnoreCaseComparer.Default);
    private string _key = String.Empty;
    private TaskCacheWarning[] _warnings = [];
    private readonly CancellationToken _cancellationToken;

    private TaskInvocationCache(string directory, byte[] invocation, List<string> inputs, List<string> outputs,
        Dictionary<string, TaskPropertyInfo> requestedOutputs, TaskCacheBackend backend, CancellationToken cancellationToken)
    {
        _store = new TaskCacheStore(directory, backend);
        _cancellationToken = cancellationToken;
        _invocation = invocation;
        _inputs = inputs;
        _outputs = outputs;
        _requestedOutputs = requestedOutputs;
    }

    internal bool IsHit { get; private set; }

    internal static bool HasDeclaredIO(Type taskType)
    {
        foreach (CustomAttributeData attribute in taskType.GetCustomAttributesData())
        {
            if (attribute.AttributeType.FullName == "Microsoft.Build.Framework.MSBuildDeclaredIOTaskAttribute")
            {
                return true;
            }
        }

        return false;
    }

    internal static TaskInvocationCache? TryCreate(
        ProjectTaskInstance task,
        ITask instance,
        TaskFactoryWrapper factory,
        Dictionary<string, byte[]> boundInputs,
        TaskEnvironment environment,
        ProjectInstance project,
        string directory,
        out string? rejection,
        TaskCacheBackend backend,
        CancellationToken cancellationToken)
    {
        rejection = null;
        HashSet<string> inputNames = new(MSBuildNameIgnoreCaseComparer.Default);
        HashSet<string> outputNames = new(MSBuildNameIgnoreCaseComparer.Default);
        bool marked = false;
        // Full-name matching accepts task-local attribute polyfills without constructing attributes.
        foreach (CustomAttributeData attribute in factory.TaskFactoryLoadedType.Type.GetCustomAttributesData())
        {
            string? name = attribute.AttributeType.FullName;
            if (name == "Microsoft.Build.Framework.MSBuildDeclaredIOTaskAttribute")
            {
                if (attribute.ConstructorArguments.Count != 0 || attribute.NamedArguments.Count != 0)
                {
                    return Reject("TaskCache.DeclaredIOInvalid", out rejection);
                }

                marked = true;
            }
            else if (name is "Microsoft.Build.Framework.MSBuildDeclaredIOInputAttribute" or "Microsoft.Build.Framework.MSBuildDeclaredIOOutputAttribute")
            {
                if (attribute.ConstructorArguments.Count != 1
                    || attribute.ConstructorArguments[0].Value is not string parameterName
                    || String.IsNullOrWhiteSpace(parameterName)
                    || attribute.NamedArguments.Count != 0)
                {
                    return Reject("TaskCache.DeclaredIOInvalid", out rejection);
                }

                (name == "Microsoft.Build.Framework.MSBuildDeclaredIOInputAttribute" ? inputNames : outputNames).Add(parameterName);
            }
        }

        if (!marked)
        {
            return Reject("TaskCache.DeclaredIOMarkerRequired", out rejection);
        }

        // Authors may expose this optional switch when only a restricted execution mode
        // satisfies their declarations. Tasks without the switch rely on the marker promise.
        if (factory.GetProperty(MSBuildConstants.MSBuildTaskCacheEnabled)?.PropertyType == typeof(bool)
            && (!boundInputs.TryGetValue(MSBuildConstants.MSBuildTaskCacheEnabled, out byte[]? mode)
                || DeserializeParameter(mode) is not true))
        {
            return Reject("TaskCache.DeclaredIOOptOut", out rejection);
        }

        if (inputNames.Overlaps(outputNames))
        {
            return Reject("TaskCache.ReadWriteOverlap", out rejection);
        }

        Dictionary<string, TaskPropertyInfo> requestedOutputs = new(MSBuildNameIgnoreCaseComparer.Default);
        foreach (ProjectTaskInstanceChild output in task.Outputs)
        {
            string name = output switch
            {
                ProjectTaskOutputItemInstance item => item.TaskParameter,
                ProjectTaskOutputPropertyInstance outputProperty => outputProperty.TaskParameter,
                _ => String.Empty
            };
            // Conditions and destinations are evaluated only by normal output gathering. Requiring
            // complete name coverage lets a future invocation use different destinations/conditions.
            if (String.IsNullOrWhiteSpace(name) || name.IndexOfAny(['$', '@', '%', '(', ')', ';']) >= 0)
            {
                return Reject("TaskCache.DeclaredIOStructure", out rejection);
            }

            TaskPropertyInfo? property = factory.GetProperty(name);
            if (property is null || !factory.GetNamesOfPropertiesWithOutputAttribute.ContainsKey(name)
                || !IsSupportedParameterType(property.PropertyType))
            {
                return Reject("TaskCache.DeclaredIOStructure", out rejection);
            }

            requestedOutputs[property.Name] = property;
        }

        List<string> inputs = [];
        List<string> outputs = [];
        Dictionary<string, byte[]> effectiveFiles = new(MSBuildNameIgnoreCaseComparer.Default);
        HashSet<string>[] declarations = [inputNames, outputNames];
        foreach (HashSet<string> names in declarations)
        {
            foreach (string name in names)
            {
                TaskPropertyInfo? property = factory.GetProperty(name);
                if (property is null || !task.Parameters.ContainsKey(name))
                {
                    rejection = ResourceUtilities.FormatResourceStringIgnoreCodeAndKeyword("TaskCache.DeclaredIOUnbound", name);
                    return null;
                }

                Type type = property.PropertyType.IsArray ? property.PropertyType.GetElementType()! : property.PropertyType;
                if (type != typeof(string) && type != typeof(ITaskItem))
                {
                    return Reject("TaskCache.DeclaredIOInvalid", out rejection);
                }

                // These are the file contract, not speculative task outputs. Inspect the effective
                // paths after all setters have run, including defaults left by explicit empty XML
                // values (which do not call setters in MSBuild).
                object? value = factory.GetPropertyValue(instance, property);
                effectiveFiles[property.Name] = SerializeInputParameter(value);
                try
                {
                    AddPaths(value, names == inputNames ? inputs : outputs, environment);
                }
                catch (Exception e) when (e is InvalidDataException or ArgumentException or NotSupportedException)
                {
                    return Reject("TaskCache.DeclaredIOInvalid", out rejection);
                }
            }
        }

        foreach (string output in outputs)
        {
            if (ContainsPath(inputs, output))
            {
                return Reject("TaskCache.ReadWriteOverlap", out rejection);
            }
        }

        string assembly = factory.TaskFactoryLoadedType.Path;
        if (String.IsNullOrEmpty(assembly) || !File.Exists(assembly))
        {
            return Reject("TaskCache.DeclaredIOInvalid", out rejection);
        }

        AddPath(assembly, inputs, environment);
        foreach (string output in outputs)
        {
            if (ContainsPath(inputs, output))
            {
                return Reject("TaskCache.ReadWriteOverlap", out rejection);
            }
        }

        inputs.Sort(StringComparer.Ordinal);
        outputs.Sort(StringComparer.Ordinal);
        using MemoryStream stream = new();
        using (BinaryWriter writer = new(stream, Encoding.UTF8, leaveOpen: true))
        {
            writer.Write(typeof(TaskInvocationCache).Module.ModuleVersionId.ToString());
            writer.Write(factory.TaskFactoryLoadedType.Type.Module.ModuleVersionId.ToString());
            writer.Write(factory.TaskFactoryLoadedType.Type.FullName!);
            writer.Write(project.FullPath);
            writer.Write(project.ToolsVersion);
            writer.Write(project.Toolset.ToolsPath);
            writer.Write(environment.ProjectDirectory.Value);
            writer.Write(Environment.Version.ToString());
            writer.Write(Environment.OSVersion.ToString());
            writer.Write(Environment.Is64BitProcess);
            writer.Write(CultureInfo.CurrentCulture.Name);
            writer.Write(CultureInfo.CurrentUICulture.Name);
            WriteParameters(writer, boundInputs);
            WriteParameters(writer, effectiveFiles);
            List<string> names = new(requestedOutputs.Keys);
            names.Sort(StringComparer.Ordinal);
            writer.Write(names.Count);
            foreach (string name in names)
            {
                writer.Write(name);
            }

            IReadOnlyDictionary<string, string> variables = environment.GetEnvironmentVariables();
            names = new(variables.Keys);
            names.Sort(StringComparer.Ordinal);
            writer.Write(names.Count);
            foreach (string name in names)
            {
                writer.Write(name);
                writer.Write(variables[name]);
            }

            writer.Write(outputs.Count);
            foreach (string output in outputs)
            {
                writer.Write(output);
            }
        }

        return new TaskInvocationCache(directory, stream.ToArray(), inputs, outputs, requestedOutputs, backend, cancellationToken);
    }

    internal bool TryRestore()
    {
        // Contract getters can log diagnostics. The host checks those before
        // entering lookup, so defer input file access until that check succeeds.
        _key = ComputeKey();
        IsHit = _store.TryRestore(_key, _outputs, out _, ValidateState, _cancellationToken);
        return IsHit;
    }

    internal object? GetOutput(string name) => DeserializeParameter(_values[name]);
    internal bool HasOutput(string name) => _values.ContainsKey(name);
    internal IEnumerable<TaskPropertyInfo> RequestedOutputs => _requestedOutputs.Values;
    internal IReadOnlyList<TaskCacheWarning> Warnings => _warnings;

    internal void CaptureOutput(string name, object? value)
    {
        byte[] serialized = SerializeParameter(value);
        if (_values.TryGetValue(name, out byte[]? previous)
            && !previous.AsSpan().SequenceEqual(serialized))
        {
            throw new InvalidDataException("The task output getter returned different values for repeated bindings.");
        }

        _values[name] = serialized;
    }

    internal bool Publish(Func<bool> canPublish, IReadOnlyList<TaskCacheWarning> warnings)
    {
        if (_values.Count != _requestedOutputs.Count)
        {
            throw new InvalidDataException("Task cache output capture is incomplete.");
        }

        if (!String.Equals(_key, ComputeKey(), StringComparison.Ordinal))
        {
            return false;
        }

        using MemoryStream stream = new();
        using (BinaryWriter writer = new(stream, Encoding.UTF8, leaveOpen: true))
        {
            WriteParameters(writer, _values);
            TaskCacheWarning.Write(writer, warnings);
        }

        return _store.Store(_key, _outputs, stream.ToArray(),
            () => canPublish() && String.Equals(_key, ComputeKey(), StringComparison.Ordinal), _cancellationToken);
    }

    private void ValidateState(byte[] state)
    {
        using MemoryStream stream = new(state, writable: false);
        using BinaryReader reader = new(stream, Encoding.UTF8);
        if (reader.ReadInt32() != _requestedOutputs.Count)
        {
            throw new InvalidDataException("Task cache output coverage does not match the invocation.");
        }

        _values.Clear();
        for (int i = 0; i < _requestedOutputs.Count; i++)
        {
            string name = reader.ReadString();
            int length = reader.ReadInt32();
            if (!_requestedOutputs.TryGetValue(name, out TaskPropertyInfo? property)
                || _values.ContainsKey(name) || length < 0 || length > stream.Length - stream.Position)
            {
                throw new InvalidDataException("Invalid task cache output.");
            }

            byte[] value = reader.ReadBytes(length);
            object? decoded = DeserializeParameter(value);
            if (!IsCompatibleOutput(property.PropertyType, decoded))
            {
                throw new InvalidDataException("Task cache output type does not match the invocation.");
            }

            _values.Add(name, value);
        }

        _warnings = TaskCacheWarning.Read(reader);
        if (stream.Position != stream.Length)
        {
            throw new InvalidDataException("Unexpected data in task cache outputs.");
        }
    }

    private string ComputeKey()
    {
        using MemoryStream stream = new();
        using (BinaryWriter writer = new(stream, Encoding.UTF8, leaveOpen: true))
        {
            writer.Write(_invocation.Length);
            writer.Write(_invocation);
            writer.Write(_inputs.Count);
            foreach (string input in _inputs)
            {
                _cancellationToken.ThrowIfCancellationRequested();
                TaskCacheStore.CheckPath(input, allowDirectory: false, allowReadOnlyFile: true);
                writer.Write(input);
                bool exists = File.Exists(input);
                writer.Write(exists);
                if (exists)
                {
                    using FileStream file = new(input, FileMode.Open, FileAccess.Read, FileShare.Read);
                    using SHA256 hash = SHA256.Create();
                    writer.Write(hash.ComputeHash(file));
                }
            }
        }

        using SHA256 keyHash = SHA256.Create();
        return BitConverter.ToString(keyHash.ComputeHash(stream.ToArray())).Replace("-", String.Empty);
    }

    internal static byte[] SerializeInputParameter(object? value)
    {
        value = NormalizeTaskItemArray(value);
        byte[] serialized = SerializeParameter(value);
        if (value is not ITaskItem && value is not ITaskItem[])
        {
            return serialized;
        }

        using MemoryStream stream = new();
        using (BinaryWriter writer = new(stream, Encoding.UTF8, leaveOpen: true))
        {
            writer.Write(serialized.Length);
            writer.Write(serialized);
            if (value is ITaskItem item)
            {
                WriteTaskVisibleMetadata(writer, item);
            }
            else
            {
                foreach (ITaskItem? element in (ITaskItem[])value)
                {
                    WriteTaskVisibleMetadata(writer, element);
                }
            }
        }
        return stream.ToArray();
    }

    private static void WriteTaskVisibleMetadata(BinaryWriter writer, ITaskItem? item)
    {
        writer.Write(item is not null);
        if (item is null)
        {
            return;
        }

        // Transport metadata alone does not preserve whether item-definition expressions
        // expand on access. Keep both representations so neither distinction is lost.
        IDictionary metadata = item.CloneCustomMetadata();
        List<string> names = new(metadata.Count);
        foreach (object key in metadata.Keys)
        {
            if (key is not string name)
            {
                throw new InvalidDataException("Invalid task item metadata name.");
            }
            names.Add(name);
        }
        names.Sort(StringComparer.Ordinal);
        writer.Write(names.Count);
        foreach (string name in names)
        {
            writer.Write(name);
            writer.Write(item.GetMetadata(name));
        }
    }

    internal static byte[] SerializeParameter(object? value)
    {
        if (value is not null && !IsSupportedParameterType(value.GetType()))
        {
            throw new InvalidDataException("The task parameter cannot be losslessly cached.");
        }

        value = NormalizeTaskItemArray(value);
        using MemoryStream stream = new();
        using (ITranslator translator = BinaryTranslator.GetWriteTranslator(stream))
        {
            TaskParameter parameter = new(value);
            parameter.CanonicalizeMetadata();
            parameter.Translate(translator);
        }

        return stream.ToArray();
    }

    private static object? NormalizeTaskItemArray(object? value)
    {
        if (value is ITaskItem[] || value is not Array array || array.Rank != 1
            || !typeof(ITaskItem).IsAssignableFrom(array.GetType().GetElementType()))
        {
            return value;
        }

        // Struct-backed item arrays are not covariant to ITaskItem[]. Box their
        // elements so the existing translator preserves item data rather than strings.
        ITaskItem[] items = new ITaskItem[array.Length];
        int lowerBound = array.GetLowerBound(0);
        for (int i = 0; i < items.Length; i++)
        {
            items[i] = (ITaskItem)array.GetValue(lowerBound + i)!;
        }
        return items;
    }

    private static object? DeserializeParameter(byte[] bytes)
    {
        using MemoryStream stream = new(bytes, 0, bytes.Length, writable: false, publiclyVisible: true);
        using (BinaryReader reader = new(stream, Encoding.UTF8, leaveOpen: true))
        {
            TaskParameterType kind = (TaskParameterType)reader.ReadInt32();
            if (kind is not (TaskParameterType.Null or TaskParameterType.PrimitiveType or TaskParameterType.PrimitiveTypeArray
                or TaskParameterType.ITaskItem or TaskParameterType.ITaskItemArray))
            {
                throw new InvalidDataException("Invalid task cache parameter type.");
            }

            if (kind is TaskParameterType.PrimitiveType or TaskParameterType.PrimitiveTypeArray)
            {
                TypeCode typeCode = (TypeCode)reader.ReadInt32();
                if (typeCode is < TypeCode.Boolean or > TypeCode.String || (int)typeCode == 17)
                {
                    throw new InvalidDataException("Invalid task cache primitive type.");
                }
            }
        }

        stream.Position = 0;
        using ITranslator translator = BinaryTranslator.GetReadTranslator(stream, InterningBinaryReader.PoolingBuffer);
        TaskParameter parameter = TaskParameter.FactoryForDeserialization(translator);
        if (parameter.ParameterType is TaskParameterType.Invalid or TaskParameterType.ValueType or TaskParameterType.ValueTypeArray
            || stream.Position != stream.Length)
        {
            throw new InvalidDataException("Invalid task cache parameter.");
        }

        return parameter.WrappedParameter;
    }

    private static bool IsSupportedParameterType(Type type)
    {
        if (type.IsArray)
        {
            if (type.GetArrayRank() != 1)
            {
                return false;
            }

            type = type.GetElementType()!;
            // The native task translator represents these arrays with general-format strings,
            // which do not preserve every bit/tick on all supported runtimes.
            if (type == typeof(double) || type == typeof(DateTime))
            {
                return false;
            }
        }

        return typeof(ITaskItem).IsAssignableFrom(type)
            || (!type.IsEnum && Type.GetTypeCode(type) is not (TypeCode.Object or TypeCode.DBNull or TypeCode.Empty or TypeCode.Single));
    }

    private static bool IsCompatibleOutput(Type type, object? value)
    {
        if (value is null)
        {
            return true;
        }

        return type.IsInstanceOfType(value)
            || (type.IsArray && typeof(ITaskItem).IsAssignableFrom(type.GetElementType()) && value is ITaskItem[])
            || (typeof(ITaskItem).IsAssignableFrom(type) && value is ITaskItem);
    }

    private static void AddPaths(object? value, List<string> paths, TaskEnvironment environment)
    {
        if (value is Array array)
        {
            foreach (object? element in array)
            {
                AddPaths(element, paths, environment);
            }
        }
        else if (value is string path)
        {
            AddPath(path, paths, environment);
        }
        else if (value is ITaskItem item)
        {
            AddPath(item.ItemSpec, paths, environment);
        }
        else if (value is not null)
        {
            throw new InvalidDataException("Invalid declared file parameter.");
        }
    }

    private static void AddPath(string path, List<string> paths, TaskEnvironment environment)
    {
        if (path.Length == 0)
        {
            return;
        }

        if (path.IndexOfAny(['*', '?']) >= 0)
        {
            throw new InvalidDataException("Declared task files must not contain wildcards.");
        }

        string normalized = NormalizePath(path, environment);
        if (!ContainsPath(paths, normalized))
        {
            paths.Add(normalized);
        }
    }

    internal static string NormalizePath(string path, TaskEnvironment environment) =>
        Path.GetFullPath(environment.GetAbsolutePath(path).Value);

    private static void WriteParameters(BinaryWriter writer, Dictionary<string, byte[]> values)
    {
        List<string> names = new(values.Keys);
        names.Sort(StringComparer.Ordinal);
        writer.Write(names.Count);
        foreach (string name in names)
        {
            writer.Write(name);
            writer.Write(values[name].Length);
            writer.Write(values[name]);
        }
    }

    private static bool ContainsPath(List<string> paths, string path)
    {
        foreach (string candidate in paths)
        {
            if (FileUtilities.PathComparer.Equals(candidate, path))
            {
                return true;
            }
        }

        return false;
    }

    private static TaskInvocationCache? Reject(string resource, out string? rejection)
    {
        rejection = ResourceUtilities.GetResourceString(resource);
        return null;
    }

    internal static bool IsCacheException(Exception exception) =>
        exception is InvalidDataException or ArgumentException or NotSupportedException or FormatException or OverflowException
            or TargetInvocationException or InvalidOperationException
        || ExceptionHandling.IsIoRelatedException(exception);
}
