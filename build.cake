#addin "nuget:?package=Cake.Coveralls&version=0.9.0"

#tool "nuget:?package=coveralls.io&version=1.4.2"
#tool "nuget:?package=OpenCover&version=4.6.519"
#tool "nuget:?package=ReportGenerator&version=3.1.2"

var target = Argument("target", "Default");
var configuration = Argument("configuration", "Release");
var coverallsToken = EnvironmentVariable("COVERALLS_TOKEN");
var isLocal = BuildSystem.IsLocalBuild;

var solutionPath = "./RepoTemplate.sln";
var artifactsPath = "./artifacts/";
var testCoveragePath = $"{artifactsPath}TestCoverage.xml";
var testCoverageReportPath = $"{artifactsPath}TestCoverage/";

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
        Action<ICakeContext> dotNetCoreVsTest = context => {
            var testDllPaths = context.GetFiles($"./test/**/bin/x64/{configuration}/net461/*.Test.dll");
            context.DotNetCoreVSTest(testDllPaths, new DotNetCoreVSTestSettings {
                ArgumentCustomization = args => args
                    .Append($"--ResultsDirectory:{artifactsPath}"),
                Framework = ".NETFramework,Version=v4.6.1",
                Logger = "trx;LogFileName=TestResults.trx",
                Parallel = true,
                Platform = VSTestPlatform.x64
            });
        };

        EnsureDirectoryExists(artifactsPath);
        OpenCover(dotNetCoreVsTest,
            new FilePath(testCoveragePath),
            new OpenCoverSettings()
                .WithFilter("+[*]*")
                .WithFilter("-[*.Test]*.*Test")
                .ExcludeByAttribute("*.ExcludeFromCodeCoverage*")
                // Generated
                .WithFilter("-[*]ProcessedByFody")
                .WithFilter("-[*]ThisAssembly")
                .WithFilter("-[*]PublicApiGenerator.*"));
    });

Task("ReportTestCoverage")
    .WithCriteria(isLocal)
    .IsDependentOn("Test")
    .Does(() => {
        ReportGenerator(testCoveragePath, testCoverageReportPath);
        if (IsRunningOnWindows()) {
            StartProcess("cmd", new ProcessSettings {
                Arguments = $"/C start \"\" {testCoverageReportPath}index.htm"
            });
        }
    });

Task("UploadTestCoverage")
    .WithCriteria(!isLocal)
    .WithCriteria(!string.IsNullOrEmpty(coverallsToken))
    .IsDependentOn("Test")
    .Does(() => {
        CoverallsIo(testCoveragePath, new CoverallsIoSettings {
            RepoToken = coverallsToken
        });
    })
    .DeferOnError();

Task("Package")
    .IsDependentOn("ReportTestCoverage")
    .IsDependentOn("UploadTestCoverage")
    .Does(() => {
        DotNetCorePack(solutionPath, new DotNetCorePackSettings {
            Configuration = configuration,
            NoBuild = true,
            NoRestore = true,
            OutputDirectory = artifactsPath
        });
    });

Task("Default")
    .IsDependentOn("ReportTestCoverage")
    .IsDependentOn("UploadTestCoverage");

RunTarget(target);