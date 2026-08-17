# MSBuild Custom Build Checks Guide

## Overview
Custom build checks in MSBuild provide developers with an extensibility point to implement specific validation and reporting during the build process.

## Step-by-Step Custom Check Creation

### 1. Understand the Motivation
Before creating a custom check, identify your specific project needs, e.g.:
- Do you need to enforce version constraints?
- Are there security concerns with certain configurations?
- Do you want to maintain consistent project properties?
Depending on this, different MSBuild project stages can be considered (Evaluation, Build or Post Build events.)

### 2. Install Prerequisites
- Install .NET SDK 9 or higher

- Install MSBuild Custom Check Template
```powershell
dotnet new install Microsoft.Build.Templates
```

### 3. Instantiate Project Template
```powershell
dotnet new msbuildcheck -n MyCustomBuildChecks
```

### 4. Examine Template Structure
- Inherit from the MSBuild API base class (Microsoft.Build.Experimental.BuildCheck.Check) as already done by the template as otherwise your checks won't be registered during build runtime
- <CustomCheckName>.props file contains the intrinsic function "RegisterBuildCheck" that is picked by MSBuild and is an entry point to the check.
- <CustomCheckName>.csproj file has a custom target `AddNuGetDlls` included for copying 3rd party assemblies in the final package

### 5. Define Rule Identification
Key components for making your check discoverable:
- Unique Rule ID (critical for system recognition)
- Clear, descriptive title
- Comprehensive description
- Actionable recommendation message

### 6. Choose Build Stage for Monitoring
Custom checks can monitor different build stages:
- Project Evaluation Build Time (most common)
- Access project properties
- Track and validate configurations

### 7. Implement Check Logic
```csharp
public sealed class MaxVersionCheck : Check
{
    // Define allowed versions
    private static Dictionary<string, Version> propertiesToAllowedVersion = new Dictionary<string, Version>()
    {
        { "ProductVersion", new Version(6, 0, 0) }
    };

    // Unique Rule Identifier
    private const string RuleId = "BC123";

    // Define Rule with Detailed Information
    public static CheckRule SupportedRule = new CheckRule(
        RuleId,
        "NoForbiddenProjectProperties",
        "Prevent unauthorized version usage",
        "The version '{0}' for property '{1}' is forbidden. Use version '{2}' instead.",
        new CheckConfiguration(Severity = CheckResultSeverity.Warning));

    // Registration Method
    public override void RegisterActions(IBuildCheckRegistrationContext registrationContext)
    {
        registrationContext.RegisterEvaluatedPropertiesAction(EvaluatedPropertiesAction);
    }

    // Validation Logic
    private void EvaluatedPropertiesAction(BuildCheckDataContext<EvaluatedPropertiesCheckData> context)
    {
        foreach (var property in propertiesToAllowedVersion)
        {
            if (context.Data.EvaluatedProperties.TryGetValue(property.Key, out string value))
            {
                if (Version.TryParse(value, out Version version) && version > property.Value)
                {
                    context.ReportResult(BuildCheckResult.Create(
                        SupportedRule,
                        ElementLocation.EmptyLocation,
                        value,
                        property.Key,
                        property.Value.ToString()));
                }
            }
        }
    }
}
```

### 8. Configure via .editorconfig
```editorconfig
# Custom check configuration
build_check.BC123.severity = error
```

### 9. Package and Distribute
- Compile as a NuGet package
- Integrate into project build process
- Add as a PackageReference to the checked project

## Practical Considerations

### Security and Vulnerability Prevention
- Version constraints can prevent:
  - Using outdated or vulnerable package versions
  - Breaking dependencies in product files
  - Introducing security risks

### Performance Tips
- Keep checks lightweight
- Focus on specific, targeted validations
- Minimize build time overhead

## Real-World Use Cases
- Enforce version constraints
- Prevent security vulnerabilities
- Manage dependency consistency
- Validate project configurations

## Contribution and Feedback
The MSBuild team welcomes:
- Community testing
- Feature feedback
- Repository contributions
- Issue reporting

## Limitations
- Performance-conscious checks
- Limited to specific build stages

## Conclusion
Custom build checks provide a powerful mechanism to enforce project-specific rules, enhance build quality, and maintain consistent development practices.

## Getting Help
- [MSBuild documentation](https://github.com/dotnet/msbuild/tree/main/documentation/)
- [GitHub discussions](https://github.com/dotnet/msbuild/discussions)
- [GitHub repository issues](https://github.com/dotnet/msbuild/issues)