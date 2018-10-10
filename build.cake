// Add-ins
#addin "nuget:?package=Cake.GitVersioning&version=2.2.13"

// Tools
#tool "nuget:?package=OpenCover&version=4.6.519"
#tool "nuget:?package=ReportGenerator&version=4.0.0-rc4"

// Arguments
var target = Argument("target", "Default");
var configuration = Argument("configuration", "Release");
var artifactDirectory = Directory(Argument("artifactDirectory", "./artifacts"));

// Build Info
var version = GitVersioningGetVersion().SemVer2;
var isLocal = BuildSystem.IsLocalBuild;

// Paths
var solutionFile = File("./RepoTemplate.sln");
var testResultDirectory = artifactDirectory + Directory("TestResults");
var testResultFileName = "TestResults.trx";
var testCoverageFile = testResultDirectory + File("TestCoverage.OpenCover.xml");
var testCoverageCoberturaFile = testResultDirectory + File("TestCoverage.Cobertura.xml");
var testCoverageReportDirectory = artifactDirectory + Directory("TestCoverageReport");
var testCoverageReportFile = testCoverageReportDirectory + File("index.htm");

///////////////////////////////////////////

Setup(_ => Information($"Version {version}"));

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
        var reportTypes = isLocal ? "Html" : "HtmlInline;Cobertura";
        ReportGenerator(testCoverageFile, testCoverageReportDirectory, new ReportGeneratorSettings {
            ArgumentCustomization = args => args
                .Append($"-reporttypes:{reportTypes}")
        });
        if (!isLocal) {
            MoveFile(testCoverageReportDirectory + File("Cobertura.xml"), testCoverageCoberturaFile);
        } else if (IsRunningOnWindows()) {
            Information("Launching Test Coverage Report...");
            StartProcess("cmd", new ProcessSettings {
                Arguments = $"/C start \"\" {testCoverageReportFile}"
            });
        }
    });

Task("Package")
    .IsDependentOn("ReportTestCoverage")
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