### 0.21.4-beta001 - 2019-12-23
* MINOR: Cleans up mobile rendering for docs generation tool

### 0.21.3 - 2019-12-21
* DOCUMENTATION: Explain Appveyor badge nuances (https://github.com/TheAngryByrd/MiniScaffold/pull/160)
* MINOR: Use Microsoft.NETFramework.ReferenceAssemblies instead of netfx.props (https://github.com/TheAngryByrd/MiniScaffold/pull/161)


### 0.21.2 - 2019-12-21
* MINOR: Updates to depedencies (https://github.com/TheAngryByrd/MiniScaffold/pull/159)
    * FAKE 5.19.0
    * Paket 5.241.2
    * Altcover 6.6.747
    * Fantomas 3.1.0
    * Packaging.Targets 0.1.155

### 0.21.1 - 2019-12-16
* BUGFIX: IsTestProject in Directory.Build.props for Console template should be true (https://github.com/TheAngryByrd/MiniScaffold/pull/157)

### 0.21.0 - 2019-12-13
* FEATURE: Documentation generation (https://github.com/TheAngryByrd/MiniScaffold/pull/110)

### 0.20.2 - 2019-12-13
* MINOR: Bump to dotnet sdk 3.1 (https://github.com/TheAngryByrd/MiniScaffold/pull/155)
* MINOR: Add Package Name to nuget table in README (https://github.com/TheAngryByrd/MiniScaffold/pull/156)

### 0.20.1 - 2019-11-26
* MINOR: UPdate Argu to 6.0 (https://github.com/TheAngryByrd/MiniScaffold/pull/154)
* MINOR: Update Packaging.Targets to 0.1.121 (https://github.com/TheAngryByrd/MiniScaffold/pull/154)
* MINOR: Update Altcover to 5.3.688 (https://github.com/TheAngryByrd/MiniScaffold/pull/154)
* MINOR: Update expecto to 8.13.1 (https://github.com/TheAngryByrd/MiniScaffold/pull/154)
* MINOR: Update FAKE to 5.18.3 (https://github.com/TheAngryByrd/MiniScaffold/pull/154)
* MINOR: Update paket to 5.238.1 (https://github.com/TheAngryByrd/MiniScaffold/pull/154)


### 0.20.0 - 2019-10-22
* FEATURE: Use Paket as local tool (https://github.com/TheAngryByrd/MiniScaffold/pull/148)
* MINOR: Update FAKE to 5.18.1 (https://github.com/TheAngryByrd/MiniScaffold/pull/148)
* MINOR: Update Fantomas to 3.0.0 (https://github.com/TheAngryByrd/MiniScaffold/pull/152)

### 0.19.2 - 2019-10-22
* MINOR: More README cleanups (https://github.com/TheAngryByrd/MiniScaffold/pull/151)

### 0.19.1 - 2019-10-21
* MINOR: Better READMEs (https://github.com/TheAngryByrd/MiniScaffold/pull/150)

### 0.19.0 - 2019-09-24
* FEATURE: Dotnet core 3.0 support (https://github.com/TheAngryByrd/MiniScaffold/pull/141)

### 0.18.0 - 2019-09-20
* FEATURE: GitHub Actions (https://github.com/TheAngryByrd/MiniScaffold/pull/145) (https://github.com/TheAngryByrd/MiniScaffold/pull/146)

### 0.17.1 - 2019-06-04
* FEATURE: Adds environment variable `DISABLE_COVERAGE` to disable code coverage (https://github.com/TheAngryByrd/MiniScaffold/pull/139)

### 0.16.4 - 2019-05-20
* MINOR: Reorganzition of build.fsx for Library and Console (https://github.com/TheAngryByrd/MiniScaffold/pull/137)

### 0.16.3 - 2019-05-17
* MINOR: Update Fake dependencies to 5.13.7 (https://github.com/TheAngryByrd/MiniScaffold/pull/136)

### 0.16.2 - 2019-05-17
* MINOR: Speed up DotnetRestore target (https://github.com/TheAngryByrd/MiniScaffold/pull/135)

### 0.16.1 - 2019-05-08
* BUGFIX: Use latest mono in TravisCI Builds (https://github.com/TheAngryByrd/MiniScaffold/pull/133)
* MAINTENANCE: Use Source Link from Microsoft (https://github.com/TheAngryByrd/MiniScaffold/pull/131)

### 0.16.0 - 2019-05-08
* FEATURE: VSCode Devcontainer support (https://github.com/TheAngryByrd/MiniScaffold/pull/130)

### 0.15.1 - 2019-05-02
* MAINTENANCE: Bump YoloDev.Expecto.TestSdk to 0.8.0 (https://github.com/TheAngryByrd/MiniScaffold/pull/129)

### 0.15.0 - 2019-05-02
* FEATURE: Run tests via solution file to speed up build if multiple test projects (https://github.com/TheAngryByrd/MiniScaffold/pull/124)
* FEATURE: Run pack via solution via to speed up build if multiple library projects (https://github.com/TheAngryByrd/MiniScaffold/pull/123)
* FEATURE: Add recommended vscode plugins (https://github.com/TheAngryByrd/MiniScaffold/pull/128)

### 0.14.2 - 2019-05-02
* BUGFIX: Fixing packaging issues (https://github.com/TheAngryByrd/MiniScaffold/pull/127)

### 0.14.1 - 2019-04-09
* MAINTENANCE: Bump most dependencies (https://github.com/TheAngryByrd/MiniScaffold/pull/122)

### 0.14.0 - 2019-01-18
* MINOR: Use Directory.build.props for NuGet metadata (https://github.com/TheAngryByrd/MiniScaffold/pull/118)
* FEATURE: Update CI dotnet versions and TargetFrameworks to latest (https://github.com/TheAngryByrd/MiniScaffold/pull/119)

### 0.13.0 - 2019-01-09
* FEATURE: Improved GitHub issue templates (https://github.com/TheAngryByrd/MiniScaffold/pull/115)
* FEATURE: Paket as a dotnet tool (https://github.com/TheAngryByrd/MiniScaffold/pull/114)

### 0.12.1 - 2019-01-09
* BUGFIX: NETSDK1061 msbuild error (https://github.com/TheAngryByrd/MiniScaffold/pull/112)

### 0.12.0 - 2018-09-11
* FEATURE: Console App Scaffold Support (https://github.com/TheAngryByrd/MiniScaffold/pull/105)

### 0.11.0 - 2018-08-30
* FEATURE: FAKE 5 (https://github.com/TheAngryByrd/MiniScaffold/pull/104)
* FEATURE: Add Code Coverage threshold (https://github.com/TheAngryByrd/MiniScaffold/pull/103)
* MAINTENANCE: Added fsc and netfx props files (https://github.com/TheAngryByrd/MiniScaffold/pull/102)

### 0.10.0 - 2018-08-18
* FEATURE: Adding code formatter (https://github.com/TheAngryByrd/MiniScaffold/issues/97)
* BUGFIX: sh files might have wrong line endings (https://github.com/TheAngryByrd/MiniScaffold/pull/98)

### 0.9.7 - 2018-08-18
* MAINTENANCE: Update AltCover to 4.0.603 (https://github.com/TheAngryByrd/MiniScaffold/pull/95)

### 0.9.6 - 2018-08-10
* MAINTENANCE: latest dotnet sdk and targets remove netstandard 1.6 (https://github.com/TheAngryByrd/MiniScaffold/pull/93)

### 0.9.5 - 2018-07-09
* BUGFIX: fix for values left unset in the fsproj file (https://github.com/TheAngryByrd/MiniScaffold/pull/92)

### 0.9.4 - 2018-06-22
* MINOR: fixup report generator invocations on non-windows machines (https://github.com/TheAngryByrd/MiniScaffold/pull/90)

### 0.9.3 - 2018-05-31
* MAINTENANCE: Bump most dependencies (https://github.com/TheAngryByrd/MiniScaffold/pull/89)

### 0.9.2 - 2018-05-31
* BUGFIX: Fixes missed MyLib conversion from #86 (https://github.com/TheAngryByrd/MiniScaffold/pull/88)

### 0.9.1 - 2018-05-14
* BUGFIX: Fixes dotnet templating issues with dashes (https://github.com/TheAngryByrd/MiniScaffold/pull/86)

### 0.9.0 - 2018-05-14
* FEATURE: Convert to using dotnet test for running tests using (https://github.com/TheAngryByrd/MiniScaffold/pull/79)
* FEATURE: Adding Test Coverage with and Report generation (https://github.com/TheAngryByrd/MiniScaffold/pull/85)

### 0.8.1 - 2018-04-09
* BUGFIX: Handle TargetFramework as well as TargetFrameworks in proj files (https://github.com/TheAngryByrd/MiniScaffold/pull/83)
* DOCUMENTATION: Update Tree output (https://github.com/TheAngryByrd/MiniScaffold/pull/80)

### 0.8.0 - 2018-03-20
* FEATURE: Add release notes and NuGet packages to GitHub releases (https://github.com/TheAngryByrd/MiniScaffold/pull/75)

### 0.7.1 - 2018-03-19
* BUGFIX: Fixes names in PR templates (https://github.com/TheAngryByrd/MiniScaffold/pull/71)
* BUGFIX: Fix dotnet sdk 2.1.101 issue with dotnet-mono (https://github.com/TheAngryByrd/MiniScaffold/pull/74)

### 0.7.0 - 2018-03-15
* FEATURE: Source Link testing (https://github.com/TheAngryByrd/MiniScaffold/pull/69)
* FEATURE: Integration testing at the FAKE level for different perumutations (https://github.com/TheAngryByrd/MiniScaffold/pull/70)
* MINOR: Lots of small refactorings/additions (https://github.com/TheAngryByrd/MiniScaffold/pull/69)

### 0.6.1 - 2018-03-11
* BUGFIX: Only use sourcelink if git dependencies are met (https://github.com/TheAngryByrd/MiniScaffold/pull/67)
* MINOR: Only generate AssemblyInfo on Publish (https://github.com/TheAngryByrd/MiniScaffold/pull/66)

### 0.6.0 - 2018-03-07
* FEATURE: Use expecto's --log-name to specify targetframework when running tests.  Bumps Expecto to 6.0.0 and dotnet-mono to 0.5.1. (https://github.com/TheAngryByrd/MiniScaffold/pull/60)

### 0.5.2 - 2018-03-01
* BUGFUX: AssemblyVersion not showing (https://github.com/TheAngryByrd/MiniScaffold/pull/57)
* MINOR:  Added documentation to template's README/md (https://github.com/TheAngryByrd/MiniScaffold/pull/56)

### 0.5.1 - 2018-02-25
* BUGFIX: Fixed unwanted cosnole.clear (https://github.com/TheAngryByrd/MiniScaffold/pull/54)
* BUGFIX: paket bootstrapper TLS issues (https://github.com/TheAngryByrd/MiniScaffold/pull/55)

### 0.5.0 - 2018-02-15
* FEATURE: Use paket and sln files (https://github.com/TheAngryByrd/MiniScaffold/pull/35)
* FEATURE: Added GitHub ISSUE_TEMPLATE and PULL_REQUEST_TEMPLATE (https://github.com/TheAngryByrd/MiniScaffold/pull/46)
* MINOR: RELEASE_NOTES should be ISO 8601 format (https://github.com/TheAngryByrd/MiniScaffold/pull/45)
* MINOR: Put paket in magic mode (https://github.com/TheAngryByrd/MiniScaffold/pull/47)

### 0.4.1 - 2018-02-10
* BUGFIX: Make build.sh executable via post-action (https://github.com/TheAngryByrd/MiniScaffold/pull/37)
* BUGFIX: FAKE not restoring on initial build. Exclude paket-files directory from template (https://github.com/TheAngryByrd/MiniScaffold/pull/42)
* MINOR: Add Release notes to git release commits (https://github.com/TheAngryByrd/MiniScaffold/pull/39)

### 0.4.0 - 2017-12-01
* FEATURE: Added Source Link (https://github.com/TheAngryByrd/MiniScaffold/pull/34)
* MINOR: Fix CI Builds and convert to dotnet core 2 fsproj style (https://github.com/TheAngryByrd/MiniScaffold/pull/33)

### 0.3.5 - 2017-07-18
* MINOR: Added .editorconfig (https://github.com/TheAngryByrd/MiniScaffold/commit/f24293735dd04df6094d2e6f1bdd2b8771f15597)
* MINOR: Removed unneeded --restore from dotnet-mono call (https://github.com/TheAngryByrd/MiniScaffold/pull/23)

### 0.3.4 - 2017-07-03
* MINOR: Smplify framework matching(https://github.com/TheAngryByrd/MiniScaffold/pull/20)

### 0.3.3 - 2017-06-25
* MINOR: Correct and expand targets (https://github.com/TheAngryByrd/MiniScaffold/pull/18)

### 0.3.2 - 2017-05-01
* MINOR: Appveyor template and badges (https://github.com/TheAngryByrd/MiniScaffold/pull/17)
* MONOR: Using buildstats.info for badges (https://github.com/TheAngryByrd/MiniScaffold/pull/15)

### 0.3.1 - 2017-04-30
* MINOR: Add NuGet Config Defaults (https://github.com/TheAngryByrd/MiniScaffold/pull/13)

### 0.3.0 - 2017-04-30
* FEATURE: Added WatchTests Target (https://github.com/TheAngryByrd/MiniScaffold/pull/8)
* MINOR: Updated FAKE to run on Mono 5.0 (https://github.com/TheAngryByrd/MiniScaffold/pull/11)

#### 0.2.0 - 2017-04-17
* FEATURE: Use Expecto for tests (https://github.com/TheAngryByrd/MiniScaffold/pull/4)
* MINOR: Better travis ci defaults (https://github.com/TheAngryByrd/MiniScaffold/pull/2)

#### 0.1.0 - 2017-04-14
* Initial release
