# GitHub Workflows

Shared GitHub Actions workflows and build scripts for SenDevGmbH projects.

## Overview

This repository provides reusable GitHub Actions workflows and a [Cake](https://cakebuild.net/) build script for building, testing, and publishing NuGet packages.

## Contents

- **`build.cs`** — Cake.Sdk build script (file-based C# app) that handles cleaning, packing, testing, and pushing NuGet packages.
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

### Source debugging

Packages are built with [Source Link](https://learn.microsoft.com/dotnet/standard/library-guidance/sourcelink) enabled:

- `ContinuousIntegrationBuild=true` — deterministic builds with normalized source paths
- `PublishRepositoryUrl=true` — the repository URL is recorded in the package and PDB
- `EmbedUntrackedSources=true` — generated/untracked source files are embedded in the PDB
- `DebugType=embedded` — the PDB is embedded in the assembly, so symbols ship inside the package itself and work with any feed (nuget.org and Azure Artifacts alike, no `.snupkg` or symbol server required)

Consumers can step into package source code in Visual Studio or Rider — the debugger fetches the sources from GitHub at the exact commit the package was built from (disable *Just My Code* and keep *Source Link support* enabled).

### Required Secrets

| Name | Description |
|------|-------------|
| `NUGET_API_KEY` | API key for nuget.org |
| `AZURE_NUGET_KEY` | API key for the Azure Artifacts feed |
| `AZURE_NUGET_SOURCE` | URL of the Azure Artifacts NuGet feed |

## License

[MIT](LICENSE)
