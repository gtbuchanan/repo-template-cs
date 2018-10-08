#addin "nuget:?package=Cake.Codecov"

#tool "nuget:?package=Codecov"
#tool "nuget:?package=OpenCover&version=4.6.519"
#tool "nuget:?package=ReportGenerator&version=3.1.2"

var target = Argument("target", "Default");
var configuration = Argument("configuration", "Release");
var artifactDirectory = Directory(Argument("artifactDirectory", "./artifacts"));
var codecovToken = Argument<string>("codecovToken", null);
var isLocal = BuildSystem.IsLocalBuild;
var solutionFile = File("./RepoTemplate.sln");
var testResultDirectory = artifactDirectory + Directory("TestResults");
var testCoverageFile = testResultDirectory + File("TestCoverage.xml");
var testCoverageReportDirectory = testResultDirectory + Directory("TestCoverageReport");
var testCoverageReportFile = testCoverageReportDirectory + File("index.htm");

Task("InstallDependencies")
	.Does(() => ChocolateyInstall("chocolatey.config"));

Task("Clean")
    .Does(() => {
        if (DirectoryExists(artifactDirectory)) {
            Information("Cleaning artifacts...");
            DeleteDirectory(artifactDirectory, new DeleteDirectorySettings {
                Recursive = true
            });
            Information("Cleaning solution...");
        }
        DotNetCoreClean(solutionFile, new DotNetCoreCleanSettings {
            Configuration = configuration,
            Verbosity = DotNetCoreVerbosity.Quiet
        });
    });

Task("Build")
    .IsDependentOn("Clean")
    .Does(() => {
        DotNetCoreBuild(solutionFile, new DotNetCoreBuildSettings {
            Configuration = configuration,
            Verbosity = DotNetCoreVerbosity.Minimal
        });
    });

Task("Test")
    .IsDependentOn("Build")
    .Does(() => {
        Action<ICakeContext> dotNetCoreVsTest = context => {
            var testDllFiles = context.GetFiles($"./test/**/bin/x64/{configuration}/net461/*.Test.dll");
            context.DotNetCoreVSTest(testDllFiles, new DotNetCoreVSTestSettings {
                ArgumentCustomization = args => args
                    .Append($"--ResultsDirectory:{testResultDirectory}"),
                Framework = ".NETFramework,Version=v4.6.1",
                Logger = "trx;LogFileName=TestResults.trx",
                Parallel = true,
                Platform = VSTestPlatform.x64
            });
        };

        EnsureDirectoryExists(testResultDirectory);
        OpenCover(dotNetCoreVsTest, testCoverageFile, new OpenCoverSettings()
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
        ReportGenerator(testCoverageFile, testCoverageReportDirectory);
        if (IsRunningOnWindows()) {
            Information("Launching Test Coverage Report...");
            StartProcess("cmd", new ProcessSettings {
                Arguments = $"/C start \"\" {testCoverageReportFile}"
            });
        }
    });

Task("UploadTestCoverage")
    .WithCriteria(!isLocal)
    .WithCriteria(!string.IsNullOrEmpty(codecovToken))
    .IsDependentOn("Test")
    .Does(() => {
        Information($"Coverage File: {testCoverageFile}");
        Codecov(testCoverageFile, codecovToken);
    })
    .DeferOnError();

Task("Package")
    .IsDependentOn("ReportTestCoverage")
    .IsDependentOn("UploadTestCoverage")
    .Does(() => {
        DotNetCorePack(solutionFile, new DotNetCorePackSettings {
            Configuration = configuration,
            NoBuild = true,
            NoRestore = true,
            OutputDirectory = artifactDirectory
        });
    });

Task("Default")
    .IsDependentOn("ReportTestCoverage")
    .IsDependentOn("UploadTestCoverage");

RunTarget(target);