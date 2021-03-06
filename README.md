[![Build Status](https://dev.azure.com/gtbuchanan/repo-template-cs/_apis/build/status/gtbuchanan.repo-template-cs?branchName=master)](https://dev.azure.com/gtbuchanan/repo-template-cs/_build/latest?definitionId=1)
[![Test Coverage](https://dl.dropbox.com/s/fzt3bzww6hn6nkq/coverage-reportgenerator.svg)](https://dev.azure.com/gtbuchanan/repo-template-cs/_build/latest?definitionId=1)
[![codecov](https://codecov.io/gh/gtbuchanan/repo-template-cs/branch/master/graph/badge.svg)](https://codecov.io/gh/gtbuchanan/repo-template-cs)
[![Gitter chat](https://badges.gitter.im/gitterHQ/gitter.png)](https://gitter.im/gtbuchanan/repo-template-cs)

# C# Library Repository Template

A template for new repositories containing C# libraries.

## What you get

* One-step system dependency installation with [Chocolatey](https://chocolatey.org/)

* One-step local build/test with [Cake Build](https://cakebuild.net/)

* Code issue and duplication detection with [ReSharper Command Line Tools](https://www.jetbrains.com/resharper/features/command-line.html).

* Continuous Integration with [Azure Pipelines](https://azure.microsoft.com/en-us/services/devops/pipelines)

* Automatic external [JetBrains Annotations](https://www.jetbrains.com/help/resharper/Code_Analysis__Code_Annotations.html) from attributes with [JetBrainsAnnotations.Fody](https://github.com/tom-englert/JetBrainsAnnotations.Fody)

* Automatic null guarding from JetBrains Annotations with [NullGuard.Fody](https://github.com/Fody/NullGuard)

* Advanced Unit Testing with [NUnit](https://nunit.org/), [NSubstitute](http://nsubstitute.github.io/), [Shouldly](https://github.com/shouldly/shouldly), and [AutoFixture](https://github.com/AutoFixture/AutoFixture)

* Public API change tracking with [PublicApiGenerator](https://github.com/JakeGinnivan/ApiApprover)

* Test coverage analysis with [OpenCover](https://github.com/OpenCover/opencover), [ReportGenerator](https://github.com/danielpalme/ReportGenerator), and [Codecov](https://codecov.io)

* Consolidated multi-level configuration with Directory.Build.props

* Automatic versioning with [Nerdbank.GitVersioning](https://github.com/AArnott/Nerdbank.GitVersioning)

* GitHub [SourceLink](https://github.com/dotnet/sourcelink) metadata for enhanced debugging

## Development

### Initial Setup

Skip to step 4 if you already have Chocolatey.

1. Open PowerShell as Administrator
2. Install [Chocolatey](https://chocolatey.org/install#install-with-powershellexe) with PowerShell.
3. Close PowerShell
4. Open PowerShell as Administrator
5. Run `./build -Target InstallDependencies` from the root repository path
6. Close PowerShell

### Build

1. Open PowerShell
2. Run `./build` from the root repository path
