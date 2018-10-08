// Add-ins
#addin "nuget:?package=Cake.Codecov&version=0.4.0"
#addin "nuget:?package=Cake.GitVersioning&version=2.2.13"

// Tools
#tool "nuget:?package=Codecov&version=1.1.0"
#tool "nuget:?package=OpenCover&version=4.6.519"
#tool "nuget:?package=ReportGenerator&version=3.1.2"

// Arguments
var target = Argument("target", "Default");
var configuration = Argument("configuration", "Release");
var artifactDirectory = Directory(Argument("artifactDirectory", "./artifacts"));
var codecovToken = Argument<string>("codecovToken", null);

// Build Info
var version = GitVersioningGetVersion().SemVer2;
var isLocal = BuildSystem.IsLocalBuild;

// Paths
var solutionFile = File("./RepoTemplate.sln");
var testResultDirectory = artifactDirectory + Directory("TestResults");
var testResultFileName = "TestResults.trx";
var testCoverageFile = testResultDirectory + File("TestCoverage.xml");
var testCoverageReportDirectory = artifactDirectory + Directory("TestCoverageReport");
var testCoverageReportFile = testCoverageReportDirectory + File("index.htm");

///////////////////////////////////////////

Setup(_ => Information($"Version {version}"));

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
                Logger = $"trx;LogFileName={testResultFileName}",
                Parallel = true,
                Platform = VSTestPlatform.x64
            });
        };

        EnsureDirectoryExists(testResultDirectory);
        OpenCover(dotNetCoreVsTest, testCoverageFile, new OpenCoverSettings()
            .WithFilter("+[*]*")
            .WithFilter("-[*.Test]*.*Test")
            .ExcludeByAttribute("*.Test*")
            .ExcludeByAttribute("*.Theory*")
            .ExcludeByAttribute("*.ExcludeFromCodeCoverage*")
            // Generated
            .WithFilter("-[*]ProcessedByFody")
            .WithFilter("-[*]ThisAssembly")
            .WithFilter("-[*]PublicApiGenerator.*"));
    });

Task("ReportTestCoverage")
    .WithCriteria(isLocal, "Not a local build")
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
    .WithCriteria(!isLocal, "Not a CI build")
    .WithCriteria(!string.IsNullOrEmpty(codecovToken), "Missing Codecov token")
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
            OutputDirectory = artifactDirectory + Directory("Packages")
        });
    });

Task("Default")
    .IsDependentOn("Package");

///////////////////////////////////////////

RunTarget(target);