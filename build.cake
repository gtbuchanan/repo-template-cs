// Add-ins
#addin "nuget:?package=Cake.Codecov&version=0.4.0"
#addin "nuget:?package=Cake.Git&version=0.19.0"
#addin "nuget:?package=Cake.GitVersioning&version=2.2.13"
#addin "nuget:?package=Cake.Http&version=0.5.0"
#addin "nuget:?package=Cake.Json&version=3.0.1"
#addin "nuget:?package=Newtonsoft.Json&version=9.0.1"

// Tools
#tool "nuget:?package=Codecov&version=1.1.0"
#tool "nuget:?package=OpenCover&version=4.6.519"
#tool "nuget:?package=ReportGenerator&version=4.0.0-rc4"

// Arguments
var target = Argument("target", "Default");
var configuration = Argument("configuration", "Release");
var artifactDirectory = Directory(Argument("artifactDirectory",
    EnvironmentVariable("BUILD_ARTIFACTSTAGINGDIRECTORY") ?? "./artifacts"));
var codecovToken = Argument("codecovToken", EnvironmentVariable("CODECOV_TOKEN"));
var dropboxToken = Argument("dropboxToken", EnvironmentVariable("DROPBOX_TOKEN"));

// Build Info
var version = GitVersioningGetVersion().SemVer2;
var isWindows = IsRunningOnWindows();
var isLocal = BuildSystem.IsLocalBuild;
var isAzurePipelines = TFBuild.IsRunningOnVSTS || TFBuild.IsRunningOnTFS;
var isFork = !StringComparer.OrdinalIgnoreCase.Equals(
    "gtbuchanan/repo-template-cs",
    TFBuild.Environment.Repository.RepoName);
var branchName = isAzurePipelines
    ? TFBuild.Environment.Repository.Branch
    : GitBranchCurrent(".").FriendlyName;

// Paths
var solutionFile = File("./RepoTemplate.sln");
var testResultDirectory = artifactDirectory + Directory("TestResults");
var testResultFileName = "TestResults.trx";
var testResultFile = testResultDirectory + File(testResultFileName);
var testCoverageFile = testResultDirectory + File("TestCoverage.OpenCover.xml");
var testCoverageCoberturaFile = testResultDirectory + File("TestCoverage.Cobertura.xml");
var testCoverageReportDirectory = artifactDirectory + Directory("TestCoverageReport");
var testCoverageReportFile = testCoverageReportDirectory + File("index.htm");
var packageDirectory = artifactDirectory + Directory("Packages");

///////////////////////////////////////////

Setup(_ => Information($"Version {version} from branch {branchName}"));

Task("InstallDependencies")
    .Does(() => ChocolateyInstall("chocolatey.config"));

Task("Clean")
    .Does(() => {
        CleanDirectory(artifactDirectory);
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
        OpenCover(dotNetCoreVsTest, testCoverageFile,
            new OpenCoverSettings{
                ArgumentCustomization = args =>
                    args.Append("-register")
            }
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
    .IsDependentOn("Test")
    .Does(() => {
        ReportGenerator(testCoverageFile, testCoverageReportDirectory, new ReportGeneratorSettings {
            ArgumentCustomization = args => args
                .Append($"-reporttypes:HtmlInline;Cobertura;Badges")
        });
        MoveFile(testCoverageReportDirectory + File("Cobertura.xml"), testCoverageCoberturaFile);
    });

Task("LaunchTestCoverageReport")
    .WithCriteria(isLocal, "CI environment")
    .WithCriteria(isWindows, "Not Windows")
    .IsDependentOn("ReportTestCoverage")
    .Does(() => {
        StartProcess("cmd", new ProcessSettings {
            Arguments = $"/C start \"\" {testCoverageReportFile}"
        });
    });

Task("UploadTestCoverageDropbox")
    .WithCriteria(!isLocal, "Local environment")
    .WithCriteria(!isFork, "Fork")
    .WithCriteria(!string.IsNullOrEmpty(dropboxToken), "Missing Dropbox token")
    .Does(() => {
        var argsJson = SerializeJson(new {
            path = $"/{branchName}/badges/coverage-reportgenerator.svg",
            mode = "overwrite",
            mute = true
        });
        var coverageBadgeFile = testCoverageReportDirectory + File("badge_linecoverage.svg");
        var requestBytes = System.IO.File.ReadAllBytes(coverageBadgeFile);
        var responseBody = HttpPost("https://content.dropboxapi.com/2/files/upload",
            new HttpSettings { RequestBody = requestBytes }
                .EnsureSuccessStatusCode()
                .UseBearerAuthorization(dropboxToken)
                .AppendHeader("Dropbox-API-Arg", argsJson)
                .SetContentType("application/octet-stream"));
        Verbose(responseBody);
    });

Task("UploadTestCoverageCodecov")
    .WithCriteria(!isLocal, "Local environment")
    .WithCriteria(!string.IsNullOrEmpty(codecovToken), "Missing Codecov token")
    .IsDependentOn("ReportTestCoverage")
    .Does(() => {
        Codecov(testCoverageFile, codecovToken);
    });

Task("UploadTestCoverage")
    .WithCriteria(!isLocal, "Local environment")
    .IsDependentOn("UploadTestCoverageCodecov")
    .IsDependentOn("UploadTestCoverageDropbox");

Task("Package")
    .Does(() => {
        DotNetCorePack(solutionFile, new DotNetCorePackSettings {
            Configuration = configuration,
            NoBuild = true,
            NoRestore = true,
            OutputDirectory = packageDirectory
        });
    });

Task("PublishTestResults")
    .WithCriteria(isAzurePipelines, "Not Azure Pipelines")
    .IsDependentOn("Test")
    .Does(() => {
        TFBuild.Commands.PublishTestResults(new TFBuildPublishTestResultsData {
            Configuration = configuration,
            Platform = "x64",
            TestResultsFiles = new string[] { testResultFile },
            TestRunTitle = "Unit Tests",
            TestRunner = TFTestRunnerType.VSTest
        });
    });

Task("PublishTestCoverageResults")
    .WithCriteria(isAzurePipelines, "Not Azure Pipelines")
    .IsDependentOn("ReportTestCoverage")
    .Does(() => {
        TFBuild.Commands.PublishCodeCoverage(new TFBuildPublishCodeCoverageData {
            CodeCoverageTool = TFCodeCoverageToolType.Cobertura,
            ReportDirectory = testCoverageReportDirectory,
            SummaryFileLocation = testCoverageCoberturaFile
        });
    });

Task("PublishTestArtifacts")
    .WithCriteria(isAzurePipelines, "Not Azure Pipelines")
    .IsDependentOn("ReportTestCoverage")
    .Does(() => {
        TFBuild.Commands.LinkArtifact("Test Results", TFBuildArtifactType.Container, testResultDirectory);
    });

Task("PublishPackageArtifacts")
    .WithCriteria(isAzurePipelines, "Not Azure Pipelines")
    .WithCriteria(!isFork, "Fork")
    .IsDependentOn("Package")
    .Does(() => {
        TFBuild.Commands.LinkArtifact("Packages", TFBuildArtifactType.Container, packageDirectory);
    });

Task("Default")
    .IsDependentOn("LaunchTestCoverageReport")
    .IsDependentOn("UploadTestCoverage")
    .IsDependentOn("PublishTestResults")
    .IsDependentOn("PublishTestCoverageResults")
    .IsDependentOn("PublishTestArtifacts")
    .IsDependentOn("PublishPackageArtifacts");

///////////////////////////////////////////

RunTarget(target);
