// Add-ins
#addin "nuget:?package=Cake.Codecov&version=0.5.0"
#addin "nuget:?package=Cake.Git&version=0.19.0"
#addin "nuget:?package=Cake.GitVersioning&version=2.2.13"
#addin "nuget:?package=Cake.Http&version=0.5.0"
#addin "nuget:?package=Cake.Issues&version=0.6.2"
#addin "nuget:?package=Cake.Issues.InspectCode&version=0.6.1"
#addin "nuget:?package=Cake.Issues.Reporting&version=0.6.1"
#addin "nuget:?package=Cake.Issues.Reporting.Generic&version=0.6.0"
#addin "nuget:?package=Cake.Json&version=3.0.1"
#addin "nuget:?package=Cake.ReSharperReports&version=0.10.0"
#addin "nuget:?package=Newtonsoft.Json&version=9.0.1"

// Tools
#tool "nuget:?package=Codecov&version=1.1.0"
#tool "nuget:?package=JetBrains.ReSharper.CommandLineTools&version=2018.3.1"
#tool "nuget:?package=OpenCover&version=4.6.519"
#tool "nuget:?package=ReportGenerator&version=4.0.5"
#tool "nuget:?package=ReSharperReports&version=0.4.0"

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

var codeIssues = new List<IIssue>();

///////////////////////////////////////////

void DeleteDirectoryIfExists(DirectoryPath directory) {
    if (!DirectoryExists(directory)) return;
    DeleteDirectory(directory,
        new DeleteDirectorySettings {
            Recursive = true,
            Force = true
        });
}

void DeleteFileIfExists(FilePath file) {
    if (!FileExists(file)) return;
    DeleteFile(file);
}

void LaunchDefaultProgram(string filePath) {
    var exitCode = StartProcess("cmd", new ProcessSettings {
        Arguments = new ProcessArgumentBuilder()
            .Append("/C")
            .Append("start")
            .Append("\"\"")
            .Append(filePath)
    });
    if (exitCode != 0) throw new Exception("Failed to start program");
}

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
    .Does(() => {
        DotNetCoreBuild(solutionFile, new DotNetCoreBuildSettings {
            Configuration = configuration,
            Verbosity = DotNetCoreVerbosity.Minimal
        });
    });

Task("DetectCodeDuplication")
    .WithCriteria(isWindows, "Not Windows")
    .Does(() => {
        var duplicateFile = artifactDirectory + File("DupFinder.xml");
        var duplicateReportDirectory = artifactDirectory + Directory("CodeDuplicationReport");
        var duplicateReportFile = duplicateReportDirectory + File("index.html");

        DeleteFileIfExists(duplicateFile);
        DeleteDirectoryIfExists(duplicateReportDirectory);

        try {
            DupFinder(solutionFile, new DupFinderSettings {
                ShowStats = true,
                ShowText = true,
                OutputFile = duplicateFile,
                ExcludeCodeRegionsByNameSubstring = new string[] { "DupFinder Exclusion" },
                ExcludePattern = new [] {
                    "./**/*.AssemblyInfo.cs",
                    "./test/**/*.cs"
                },
                ThrowExceptionOnFindingDuplicates = true
            });
        } catch (CakeException) {
            Warning("Duplicate code detected. Generating report...");
            EnsureDirectoryExists(duplicateReportDirectory);
            ReSharperReports(duplicateFile, duplicateReportFile);
            if (isLocal) {
                LaunchDefaultProgram(duplicateReportFile);
            }
            throw;
        }
    });

Task("DetectCodeIssues")
    .WithCriteria(isWindows, "Not Windows")
    .Does(() => {
        var inspectionFile = artifactDirectory + File("InspectCode.xml");

        DeleteFileIfExists(inspectionFile);

        // BUG: Only runs single threaded (https://youtrack.jetbrains.com/issue/RSRP-427896)
        InspectCode(solutionFile, new InspectCodeSettings {
            SolutionWideAnalysis = true,
            OutputFile = inspectionFile
        });

        var issues = ReadIssues(InspectCodeIssuesFromFilePath(inspectionFile), ".")
            // BUG: ReSharper doesn't recognize some generated files from the obj directory (https://youtrack.jetbrains.com/issue/RSRP-470475)
            .Where(i => !i.ProjectName.EndsWith(".Test")
                && i.Message != "Cannot access internal class 'ThisAssembly' here")
            .ToArray();
        if (issues.Any()) {
            Warning("{0} code issues found.", issues.Length);
            codeIssues.AddRange(issues);
        } else {
            Information("No code issues found.");
        }
    });

Task("BuildCodeIssuesReport")
    .WithCriteria(() => codeIssues.Any(), "No Code Issues")
    .Does(() => {
        var codeIssuesReportDirectory = artifactDirectory + Directory("CodeIssuesReport");
        var codeIssuesReportFile = codeIssuesReportDirectory + File("index.html");

        CleanDirectory(codeIssuesReportDirectory);

        var template = GenericIssueReportFormatFromEmbeddedTemplate(GenericIssueReportTemplate.HtmlDxDataGrid);
        CreateIssueReport(codeIssues, template, "./", codeIssuesReportFile);
    });

Task("LaunchCodeIssuesReport")
    .WithCriteria(isLocal, "Not Local Environment")
    .WithCriteria(isWindows, "Not Windows")
    .WithCriteria(() => codeIssues.Any(), "No Code Issues")
    .IsDependentOn("BuildCodeIssuesReport")
    .Does(() => LaunchDefaultProgram(artifactDirectory + File("CodeIssuesReport/index.html")));

Task("AnalyzeCodeQuality")
    .IsDependentOn("DetectCodeDuplication")
    .IsDependentOn("DetectCodeIssues")
    .IsDependentOn("LaunchCodeIssuesReport");

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

Task("BuildTestCoverageReport")
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
    .IsDependentOn("BuildTestCoverageReport")
    .Does(() => LaunchDefaultProgram(testCoverageReportFile.ToString()));

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
    .IsDependentOn("BuildTestCoverageReport")
    .Does(() => {
        Codecov(new CodecovSettings {
            Branch = branchName,
            Build = version,
            Files = new [] { testCoverageFile.ToString() },
            Token = codecovToken
        });
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
    .IsDependentOn("BuildTestCoverageReport")
    .Does(() => {
        TFBuild.Commands.PublishCodeCoverage(new TFBuildPublishCodeCoverageData {
            CodeCoverageTool = TFCodeCoverageToolType.Cobertura,
            ReportDirectory = testCoverageReportDirectory,
            SummaryFileLocation = testCoverageCoberturaFile
        });
    });

Task("PublishTestArtifacts")
    .WithCriteria(isAzurePipelines, "Not Azure Pipelines")
    .IsDependentOn("BuildTestCoverageReport")
    .Does(() => {
        var artifactName = "TestResults";
        Information($"##vso[artifact.upload containerfolder={artifactName};artifactname={artifactName}]{testResultDirectory}");
    });

Task("PublishPackageArtifacts")
    .WithCriteria(isAzurePipelines, "Not Azure Pipelines")
    .WithCriteria(!isFork, "Fork")
    .IsDependentOn("Package")
    .Does(() => {
        var artifactName = "Packages";
        Information($"##vso[artifact.upload containerfolder={artifactName};artifactname={artifactName}]{packageDirectory}");
    });

Task("Default")
    .IsDependentOn("Clean")
    .IsDependentOn("Build")
    .IsDependentOn("AnalyzeCodeQuality")
    .IsDependentOn("LaunchTestCoverageReport")
    .IsDependentOn("UploadTestCoverage")
    .IsDependentOn("PublishTestResults")
    .IsDependentOn("PublishTestCoverageResults")
    .IsDependentOn("PublishTestArtifacts")
    .IsDependentOn("PublishPackageArtifacts");

///////////////////////////////////////////

RunTarget(target);
