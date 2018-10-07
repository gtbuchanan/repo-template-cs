[![Build Status](https://dev.azure.com/gtbuchanan/repo-template-cs/_apis/build/status/gtbuchanan.repo-template-cs)](https://dev.azure.com/gtbuchanan/repo-template-cs/_build/latest?definitionId=1)
[![Coverage Status](https://coveralls.io/repos/github/gtbuchanan/repo-template-cs/badge.svg?branch=master)](https://coveralls.io/github/gtbuchanan/repo-template-cs?branch=master)

# C# Library Repository Template

A template for new repositories containing C# libraries.

## What you get

* One-step system dependency installation with [Chocolatey](https://chocolatey.org/)

* One-step local build/test with [Cake Build](https://cakebuild.net/)

* Continuous Integration with Azure Pipelines

* Automatic external JetBrains Annotations from attributes with [JetBrainsAnnotations.Fody](https://github.com/tom-englert/JetBrainsAnnotations.Fody)

* Automatic null guarding from JetBrains Annotations with [NullGuard.Fody](https://github.com/Fody/NullGuard)

* Advanced Unit Testing with [NUnit](https://nunit.org/), [NSubstitute](http://nsubstitute.github.io/), [Shouldly](https://github.com/shouldly/shouldly), and [AutoFixture](https://github.com/AutoFixture/AutoFixture)

* Public API change tracking with [PublicApiGenerator](https://github.com/JakeGinnivan/ApiApprover)

* Test coverage analysis with [OpenCover](https://github.com/OpenCover/opencover), [ReportGenerator](https://github.com/danielpalme/ReportGenerator), and [Coveralls](https://coveralls.io/)

* Consolidated multi-level configuration with Directory.Build.props

* Automatic versioning with [Nerdbank.GitVersioning](https://github.com/AArnott/Nerdbank.GitVersioning)

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