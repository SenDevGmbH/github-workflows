# GitHub Workflows

Shared GitHub Actions workflows and build scripts for SenDevGmbH projects.

## Overview

This repository provides reusable GitHub Actions workflows and a [Cake](https://cakebuild.net/) build script for building, testing, and publishing NuGet packages.

## Contents

- **`build.cake`** — Cake build script that handles cleaning, packing, testing, and pushing NuGet packages.
- **`.github/workflows/publish.yml`** — Reusable GitHub Actions workflow for publishing packages to NuGet.

## Usage

### Reusable Workflow

Reference the `publish.yml` workflow from your repository:

```yaml
jobs:
  publish:
    uses: SenDevGmbH/github-workflows/.github/workflows/publish.yml@main
    with:
      solution-path: 'src/MyProject.sln'
      package-name-pattern: 'MyOrg.MyPackage'
      test-project-path: 'src/MyProject.Tests/MyProject.Tests.csproj' # optional
    secrets: inherit
```

### Inputs

| Name | Required | Default | Description |
|------|----------|---------|-------------|
| `solution-path` | Yes | — | Path to the solution file |
| `package-name-pattern` | Yes | — | Base NuGet package name (e.g. `Acme.MyLibrary`) |
| `test-project-path` | No | `''` | Path to the test project (leave empty to skip tests) |
| `dotnet-version` | No | `9.0.x` | .NET SDK version to install |

### Required Secrets

| Name | Description |
|------|-------------|
| `NUGET_API_KEY` | API key for nuget.org |
| `AZURE_NUGET_KEY` | API key for the Azure Artifacts feed |
| `AZURE_NUGET_SOURCE` | URL of the Azure Artifacts NuGet feed |

## License

[MIT](LICENSE)
