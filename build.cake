var target = Argument("target", "Pack");
var buildNumber = Argument("buildNumber", "1");
var configuration = Argument("configuration", "Release");
var solutionPath = Argument("solutionPath", "");
var testProjectPath = Argument("testProjectPath", "");
var dxVersionsArg = Argument("dxVersions", "");
var packageNamePattern = Argument("packageNamePattern", "");

var nugetApiKey = EnvironmentVariable("NUGET_API_KEY");
var nugetSource = EnvironmentVariable("NUGET_SOURCE") ?? "https://api.nuget.org/v3/index.json";
var azureApiKey = EnvironmentVariable("AZURE_NUGET_KEY");
var azureSource = EnvironmentVariable("AZURE_NUGET_SOURCE");
const string tempSourceName = "TempPrivateFeed";

var devExpressVersions = dxVersionsArg
    .Split(new[] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries)
    .Select(v => v.Trim())
    .ToList();

var artifactsDir = "./artifacts";

Task("Clean")
    .Does(() =>
{
    CleanDirectory(artifactsDir);
    CleanDirectories("./src/**/bin");
    CleanDirectories("./src/**/obj");
});

Task("Pack")
    .IsDependentOn("Clean")
    .Does(() =>
{
    foreach (var version in devExpressVersions)
    {
        Information($"Processing DevExpress Version: {version}");

        var packageVersion = CalculatePackageVersion(version);
        Information($"Calculated Package Version: {packageVersion}");

        var msBuildSettings = new DotNetMSBuildSettings()
            .WithProperty("DevExpressPackageVersion", version)
            .WithProperty("PackageVersion", packageVersion);

        CreateAuthenticatedNugetConfig();
        try
        {
            DotNetRestore(solutionPath, new DotNetRestoreSettings
            {
                MSBuildSettings = msBuildSettings,
                Sources = new[] { azureSource }
            });
        }
        finally
        {
            try { DotNetNuGetRemoveSource(tempSourceName); } catch { }
        }

        DotNetBuild(solutionPath, new DotNetBuildSettings
        {
            Configuration = configuration,
            NoRestore = true,
            MSBuildSettings = msBuildSettings
        });

        DotNetPack(solutionPath, new DotNetPackSettings
        {
            Configuration = configuration,
            NoBuild = true,
            NoRestore = true,
            OutputDirectory = artifactsDir,
            MSBuildSettings = msBuildSettings
        });
    }
});

Task("Test")
    .Does(() =>
{
    if (string.IsNullOrWhiteSpace(testProjectPath))
    {
        Information("No test project specified, skipping tests.");
        return;
    }

    DotNetTest(testProjectPath, new DotNetTestSettings
    {
        Configuration = configuration,
        NoBuild = true,
        NoRestore = true,
    });
});

Task("Push")
    .IsDependentOn("Pack")
    .IsDependentOn("Test")
    .Does(() =>
{
    foreach (var version in devExpressVersions)
    {
        Information($"Processing Push for DevExpress Version: {version}");

        var packageVersion = CalculatePackageVersion(version);
        var packagePath = $"{artifactsDir}/{packageNamePattern}.{packageVersion}.nupkg";

        if (!FileExists(packagePath))
        {
            Warning($"Package not found: {packagePath}");
            continue;
        }

        var nugetSettings = GetNuGetSettings(version);

        if (string.IsNullOrEmpty(nugetSettings.ApiKey))
        {
            Warning($"API Key not found for version {version}. Skipping push.");
            continue;
        }

        Information($"Pushing {packagePath} to {nugetSettings.Source}...");
        CreateAuthenticatedNugetConfig();
        DotNetNuGetPush(packagePath, new DotNetNuGetPushSettings
        {
            Source = nugetSettings.Source,
            ApiKey = nugetSettings.ApiKey,
            SkipDuplicate = true
        });
    }
});

RunTarget(target);

string CalculatePackageVersion(string version)
{
    var versionSegments = version.Split('.');
    if (versionSegments.Length == 4)
    {
        if (int.TryParse(versionSegments[3], out int lastSegment))
        {
            int newLastSegment = (lastSegment * 1000) + int.Parse(buildNumber);
            return $"{versionSegments[0]}.{versionSegments[1]}.{versionSegments[2]}.{newLastSegment}";
        }
        throw new InvalidOperationException($"Invalid version format for self-compiled version: {version}");
    }
    return $"{version}.{buildNumber}";
}

(string Source, string ApiKey) GetNuGetSettings(string version)
{
    var versionSegments = version.Split('.');
    if (versionSegments.Length == 4)
        return (azureSource, azureApiKey);

    return (nugetSource, nugetApiKey);
}

void CreateAuthenticatedNugetConfig()
{
    try { DotNetNuGetRemoveSource(tempSourceName); } catch { }

    DotNetNuGetAddSource(tempSourceName, new DotNetNuGetAddSourceSettings
    {
        Source = azureSource,
        UserName = "devops",
        Password = azureApiKey,
        ArgumentCustomization = args => args
            .Append("--store-password-in-clear-text")
    });
}
