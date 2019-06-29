// NOTE: Generated code may require at least .NET Framework 4.5 or .NET Core/Standard 2.0.

using System;
using System.ComponentModel;
using System.Xml.Serialization;

/// <remarks />
[Serializable]
[DesignerCategory("code")]
[XmlType(AnonymousType = true)]
[XmlRoot(Namespace = "", IsNullable = false)]
public class ScenarioBenchmark
{
    private ScenarioBenchmarkTest[] testsField;

    private string nameField;

    private string namespaceField;

    /// <remarks />
    [XmlArrayItem("Test", IsNullable = false)]
    public ScenarioBenchmarkTest[] Tests
    {
        get => testsField;
        set => testsField = value;
    }

    /// <remarks />
    [XmlAttribute]
    public string Name
    {
        get => nameField;
        set => nameField = value;
    }

    /// <remarks />
    [XmlAttribute]
    public string Namespace
    {
        get => namespaceField;
        set => namespaceField = value;
    }
}

/// <remarks />
[Serializable]
[DesignerCategory("code")]
[XmlType(AnonymousType = true)]
public class ScenarioBenchmarkTest
{
    private string separatorField;

    private ScenarioBenchmarkTestPerformance performanceField;

    private string nameField;

    private string namespaceField;

    /// <remarks />
    public string Separator
    {
        get => separatorField;
        set => separatorField = value;
    }

    /// <remarks />
    public ScenarioBenchmarkTestPerformance Performance
    {
        get => performanceField;
        set => performanceField = value;
    }

    /// <remarks />
    [XmlAttribute]
    public string Name
    {
        get => nameField;
        set => nameField = value;
    }

    /// <remarks />
    [XmlAttribute]
    public string Namespace
    {
        get => namespaceField;
        set => namespaceField = value;
    }
}

/// <remarks />
[Serializable]
[DesignerCategory("code")]
[XmlType(AnonymousType = true)]
public class ScenarioBenchmarkTestPerformance
{
    private ScenarioBenchmarkTestPerformanceMetrics metricsField;

    private ScenarioBenchmarkTestPerformanceIteration[] iterationsField;

    /// <remarks />
    public ScenarioBenchmarkTestPerformanceMetrics metrics
    {
        get => metricsField;
        set => metricsField = value;
    }

    /// <remarks />
    [XmlArrayItem("iteration", IsNullable = false)]
    public ScenarioBenchmarkTestPerformanceIteration[] iterations
    {
        get => iterationsField;
        set => iterationsField = value;
    }
}

/// <remarks />
[Serializable]
[DesignerCategory("code")]
[XmlType(AnonymousType = true)]
public class ScenarioBenchmarkTestPerformanceMetrics
{
    private ScenarioBenchmarkTestPerformanceMetricsExecutionTime executionTimeField;

    /// <remarks />
    public ScenarioBenchmarkTestPerformanceMetricsExecutionTime ExecutionTime
    {
        get => executionTimeField;
        set => executionTimeField = value;
    }
}

/// <remarks />
[Serializable]
[DesignerCategory("code")]
[XmlType(AnonymousType = true)]
public class ScenarioBenchmarkTestPerformanceMetricsExecutionTime
{
    private string displayNameField;

    private string unitField;

    /// <remarks />
    [XmlAttribute]
    public string displayName
    {
        get => displayNameField;
        set => displayNameField = value;
    }

    /// <remarks />
    [XmlAttribute]
    public string unit
    {
        get => unitField;
        set => unitField = value;
    }
}

/// <remarks />
[Serializable]
[DesignerCategory("code")]
[XmlType(AnonymousType = true)]
public class ScenarioBenchmarkTestPerformanceIteration
{
    private byte indexField;

    private decimal executionTimeField;

    /// <remarks />
    [XmlAttribute]
    public byte index
    {
        get => indexField;
        set => indexField = value;
    }

    /// <remarks />
    [XmlAttribute]
    public decimal ExecutionTime
    {
        get => executionTimeField;
        set => executionTimeField = value;
    }
}
