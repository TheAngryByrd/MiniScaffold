### 0.11.0-beta002 - 2018-08-30
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
* FEATURE: Add release notes and nuget packages to GitHub releases (https://github.com/TheAngryByrd/MiniScaffold/pull/75)

### 0.7.1 - 2018-03-19
* BUGFIX: Fixes names in github PR templates (https://github.com/TheAngryByrd/MiniScaffold/pull/71)
* BUGFIX: Fix dotnet sdk 2.1.101 issue with dotnet-mono (https://github.com/TheAngryByrd/MiniScaffold/pull/74)

### 0.7.0 - 2018-03-15
* FEATURE: SourceLink testing (https://github.com/TheAngryByrd/MiniScaffold/pull/69)
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
* FEATURE: Added Github ISSUE_TEMPLATE and PULL_REQUEST_TEMPLATE (https://github.com/TheAngryByrd/MiniScaffold/pull/46)
* MINOR: RELEASE_NOTES should be ISO 8601 format (https://github.com/TheAngryByrd/MiniScaffold/pull/45)
* MINOR: Put paket in magic mode (https://github.com/TheAngryByrd/MiniScaffold/pull/47)

### 0.4.1 - 2018-02-10
* BUGFIX: Make build.sh executable via post-action (https://github.com/TheAngryByrd/MiniScaffold/pull/37)
* BUGFIX: FAKE not restoring on initial build. Exclude paket-files directory from template (https://github.com/TheAngryByrd/MiniScaffold/pull/42)
* MINOR: Add Release notes to git release commits (https://github.com/TheAngryByrd/MiniScaffold/pull/39)

### 0.4.0 - 2017-12-01
* FEATURE: Added SourceLink (https://github.com/TheAngryByrd/MiniScaffold/pull/34)
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
* MINOR: Add Nuget Config Defaults (https://github.com/TheAngryByrd/MiniScaffold/pull/13)

### 0.3.0 - 2017-04-30
* FEATURE: Added WatchTests Target (https://github.com/TheAngryByrd/MiniScaffold/pull/8)
* MINOR: Updated FAKE to run on Mono 5.0 (https://github.com/TheAngryByrd/MiniScaffold/pull/11)

#### 0.2.0 - 2017-04-17
* FEATURE: Use Expecto for tests (https://github.com/TheAngryByrd/MiniScaffold/pull/4)
* MINOR: Better travis ci defaults (https://github.com/TheAngryByrd/MiniScaffold/pull/2)

#### 0.1.0 - 2017-04-14
* Initial release
