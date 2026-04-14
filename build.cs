#:sdk Cake.Sdk@6.1.1


const string privatePackageSuffix  = ".SelfCompiled";    
var target = Argument("target", "Pack");
var buildNumber = Argument("buildNumber", "1");
var configuration = Argument("configuration", "Release");
var solutionPath = Argument("solutionPath", "");
var testProjectPath = Argument("testProjectPath", "");
var dxVersionsArg = Argument("dxVersions", "");
var packageNamePattern = Argument("packageNamePattern", "");
var currentDirectory = Argument("currentDirectory", "");

if (!string.IsNullOrWhiteSpace(currentDirectory))
    System.IO.Directory.SetCurrentDirectory(currentDirectory);

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
        Information($"Processing package version: {version}");

        var packageVersionInfo = CalculatePackageVersion(version);
        var packageVersion = packageVersionInfo.Version;
        Information($"Calculated Package Version: {packageVersion}");

        var msBuildSettings = new DotNetMSBuildSettings()
            .WithProperty("DevExpressPackageVersion", version)
            .WithProperty("PackageVersion", packageVersion)
            .WithProperty("AssemblyVersion", packageVersion);

        if (ShouldUsePrivateFeed(version))
            msBuildSettings = msBuildSettings.WithProperty("PackageIdSuffix", privatePackageSuffix);

        CreateAuthenticatedNugetConfig();
        try
        {
            DotNetRestore(solutionPath, new DotNetRestoreSettings
            {
                MSBuildSettings = msBuildSettings,
                Sources = new[] { GetNugetSource(version) }
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

    Information("Current Directory: " + System.IO.Directory.GetCurrentDirectory());
    if (string.IsNullOrWhiteSpace(testProjectPath))
    {
        Information("No test project specified, skipping tests.");
        return;
    }

    DotNetTest(testProjectPath, new DotNetTestSettings
    {
        Configuration = configuration,
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

        var packageVersionInfo = CalculatePackageVersion(version);
        var globPattern = $"{artifactsDir}/{packageNamePattern}{packageVersionInfo.Suffix}.{packageVersionInfo.Version}.nupkg";
        var packages = GetFiles(globPattern);

        if (!packages.Any())
        {
            Warning($"No packages found matching: {globPattern}");
            continue;
        }

        var nugetSettings = GetNuGetSettings(version);

        if (string.IsNullOrEmpty(nugetSettings.ApiKey))
        {
            Warning($"API Key not found for version {version}. Skipping push.");
            continue;
        }

        CreateAuthenticatedNugetConfig();
        foreach (var packagePath in packages)
        {
            Information($"Pushing {packagePath} to {nugetSettings.Source}...");
            DotNetNuGetPush(packagePath.ToString(), new DotNetNuGetPushSettings
            {
                Source = nugetSettings.Source,
                ApiKey = nugetSettings.ApiKey,
                SkipDuplicate = true
            });
        }
    }
});

RunTarget(target);


string GetNugetSource(string version)
{
    return ShouldUsePrivateFeed(version) ? azureSource : nugetSource;
}

bool ShouldUsePrivateFeed(string version)
{
    var versionSegments = version.Split('.');
    return versionSegments.Length == 4;
}

PackageVersionInfo CalculatePackageVersion(string versionString)
{
    var version = Version.Parse(versionString);
    var suffix = ShouldUsePrivateFeed(versionString) ? privatePackageSuffix : string.Empty;
    return new PackageVersionInfo(
        new Version(version.Major, version.Minor, version.Build, int.Parse(buildNumber)).ToString(),
        suffix);
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

record PackageVersionInfo(string Version, string Suffix);