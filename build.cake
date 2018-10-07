var target = Argument("target", "Default");
var configuration = Argument("configuration", "Release");

var solutionPath = "./RepoTemplate.sln";
var artifactsPath = "./artifacts/";

Task("InstallDependencies")
	.Does(() => ChocolateyInstall("chocolatey.config"));

Task("Clean")
    .Does(() => {
        if (DirectoryExists(artifactsPath)) {
            Information("Cleaning artifacts...");
            DeleteDirectory(artifactsPath, new DeleteDirectorySettings {
                Recursive = true
            });
            Information("Cleaning solution...");
        }
        DotNetCoreClean(solutionPath, new DotNetCoreCleanSettings {
            Configuration = configuration,
            Verbosity = DotNetCoreVerbosity.Quiet
        });
    });

Task("Build")
    .IsDependentOn("Clean")
    .Does(() => {
        DotNetCoreBuild(solutionPath, new DotNetCoreBuildSettings {
            Configuration = configuration,
            Verbosity = DotNetCoreVerbosity.Minimal
        });
    });

Task("Test")
    .IsDependentOn("Build")
    .Does(() => {
        var testDllPaths = GetFiles($"./test/**/bin/x64/{configuration}/net461/*.Test.dll");
        DotNetCoreVSTest(testDllPaths, new DotNetCoreVSTestSettings {
            ArgumentCustomization = args => args
                .Append($"--ResultsDirectory:{artifactsPath}"),
            Framework = ".NETFramework,Version=v4.6.1",
            Logger = "trx;LogFileName=TestResults.trx",
            Parallel = true,
            Platform = VSTestPlatform.x64
        });
    });

Task("Pack")
    .IsDependentOn("Test")
    .Does(() => {
        DotNetCorePack(solutionPath, new DotNetCorePackSettings {
            Configuration = configuration,
            NoBuild = true,
            NoRestore = true,
            OutputDirectory = artifactsPath
        });
    });

Task("Default")
    .IsDependentOn("Test");

RunTarget(target);