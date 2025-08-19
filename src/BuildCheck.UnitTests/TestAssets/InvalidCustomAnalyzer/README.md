# MSBuild Custom Analyzer Template

## Overview
MSBuild Custom Analyzer Template is a .NET template designed to streamline the creation of MSBuild analyzer libraries. This template facilitates the development of custom analyzers targeting .NET Standard, enabling developers to inspect and enforce conventions, standards, or patterns within their MSBuild builds.

## Features
- Simplified template for creating MSBuild analyzer libraries.
- Targeting .NET Standard for cross-platform compatibility.
- Provides a starting point for implementing custom analysis rules.

## Getting Started
To use the MSBuild Custom Analyzer Template, follow these steps:
1. Install the template using the following command:
   ```bash
   dotnet new install msbuildanalyzer
2. Instantiate a custom template:
   ```bash
   dotnet new msbuildanalyzer -n <ProjectName>

### Prerequisites
- .NET SDK installed on your machine.