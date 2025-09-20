# MiniScaffold Development Instructions

MiniScaffold is a .NET 8.0 F# template project that generates scaffolding for F# libraries and console applications. It uses FAKE (F# Make) for build automation and includes comprehensive tooling for testing, documentation, and release management.

**Always reference these instructions first and fallback to search or bash commands only when you encounter unexpected information that does not match the info here.**

## Working Effectively

### Essential Setup Commands
Run these commands in order to bootstrap the development environment:

```bash
# Navigate to repository root
cd /home/runner/work/MiniScaffold/MiniScaffold

# Restore tools and build - takes ~20 seconds. NEVER CANCEL.
./build.sh
```

### Build System & Key Targets
The project uses FAKE build system with these critical targets:

- **Default build (DotnetPack)**: `./build.sh` - Takes ~20 seconds. NEVER CANCEL. Set timeout to 60+ seconds.
- **Integration Tests**: `./build.sh IntegrationTests` - Takes 30+ minutes. NEVER CANCEL. Set timeout to 60+ minutes. This is the most comprehensive test that validates template generation.
- **Format Code**: `./build.sh FormatCode` - Takes ~5 seconds. Always run before committing.
- **Check Formatting**: `./build.sh CheckFormatCode` - Takes ~3 seconds. Validates code style.
- **Build Documentation**: `./build.sh BuildDocs` - Takes ~10 seconds. NEVER CANCEL.
- **Clean**: `./build.sh Clean` - Cleans build artifacts.

### Template Development Workflow
To test your changes to the template system:

```bash
# 1. Build the template package (required after any template changes)
./build.sh DotnetPack

# 2. Install the locally built template
dotnet new uninstall MiniScaffold || echo "Not installed yet"
dotnet new install ./dist/MiniScaffold.0.39.0.nupkg

# 3. Test library template generation (takes ~2 seconds)
cd /tmp
dotnet new mini-scaffold -n TestLib --githubUsername TestUser --outputType library

# 4. Test the generated template builds and works (takes ~25 seconds. NEVER CANCEL)
cd TestLib
./build.sh

# 5. Test console template generation
cd /tmp  
dotnet new mini-scaffold -n TestConsole --githubUsername TestUser --outputType console
cd TestConsole
./build.sh
```

## Validation Requirements

### CRITICAL: Always Validate Changes
After making any changes to templates or build system:

1. **Build Validation**: Run `./build.sh` - must complete successfully in ~20 seconds
2. **Template Generation**: Create both library and console templates as shown above
3. **Generated Project Build**: Both generated templates must build successfully with `./build.sh`
4. **Integration Tests**: Run `./build.sh IntegrationTests` for comprehensive validation (30+ minutes)

### Manual Testing Scenarios
Always test these scenarios after making changes:

**Library Template Scenario:**
1. Generate library: `dotnet new mini-scaffold -n MyLib --githubUsername TestUser`
2. Build: `cd MyLib && ./build.sh` (must succeed in ~25 seconds)
3. Verify output: Check that `dist/MyLib.0.1.0.nupkg` exists
4. Test basic functionality: Run generated tests

**Console Template Scenario:**  
1. Generate console app: `dotnet new mini-scaffold -n MyApp --githubUsername TestUser --outputType console`
2. Format code first: `cd MyApp && ./build.sh FormatCode` (console template may need formatting)
3. Build: `./build.sh` (must succeed in ~30 seconds)
4. Run the app: `dotnet run --project src/MyApp`

## Critical Requirements & Timing

### Build Timing Expectations
- **NEVER CANCEL builds or tests** - builds may take 30+ minutes
- Default build: ~20 seconds (timeout: 60+ seconds)
- Integration tests: 30+ minutes (timeout: 60+ minutes) 
- Template generation: ~2 seconds
- Generated template build: ~25 seconds (timeout: 60+ seconds)
- Documentation build: ~10 seconds (timeout: 30+ seconds)
- Code formatting: ~5 seconds

### Pre-Commit Requirements
Always run these before committing changes:
```bash
./build.sh FormatCode      # Format all code
./build.sh CheckFormatCode # Verify formatting (must pass)
./build.sh                 # Verify builds successfully
```

## Common Tasks & File Locations

### Key Directories
- `build/` - FAKE build scripts (build.fs is main build script)
- `Content/` - Template content for generated projects
  - `Content/Library/` - F# library template
  - `Content/Console/` - F# console application template
- `tests/MiniScaffold.Tests/` - Integration tests
- `docsSrc/` - Documentation source files
- `dist/` - Built NuGet packages (created by build)

### Important Files
- `build.sh` / `build.cmd` - Cross-platform build entry points
- `global.json` - .NET SDK version specification (8.0.100)
- `Directory.Packages.props` - Central package management
- `.config/dotnet-tools.json` - Local tools (fantomas, fsdocs-tool)
- `MiniScaffold.csproj` - Main template project file

### Environment Variables
- `CONFIGURATION` - Sets build configuration (Debug/Release), defaults to Debug
- `CI` - Set to true in CI environments, affects build behavior

## Troubleshooting

### Common Issues
- **Permission denied on build.sh**: Run `chmod +x ./build.sh`
- **Template not found**: Run `dotnet new install ./dist/MiniScaffold.0.39.0.nupkg`
- **Build timeout**: Increase timeout, builds can take 30+ minutes
- **Integration tests fail**: Ensure you have network access for NuGet restore

### Build System Details
- Uses .NET 8.0 SDK (specified in global.json)
- FAKE build system (F# DSL for build automation)
- Fantomas for code formatting
- Expecto for testing
- FSharp.Formatting/FSDocs for documentation

### Dependencies
The project uses centralized package management. Key dependencies:
- FAKE 6.1.3 (build system)
- FSharp.Core 8.0.403
- Expecto 10.2.2 (testing)
- Local tools: fantomas 7.0.3, fsdocs-tool 18.1.0

## Quick Reference Commands

```bash
# Essential workflow
./build.sh                           # Build (20s)
./build.sh FormatCode               # Format code (5s)  
./build.sh IntegrationTests        # Full test suite (30+ min)

# Template testing
dotnet new install ./dist/MiniScaffold.0.39.0.nupkg
dotnet new mini-scaffold -n MyLib --githubUsername User
cd MyLib && ./build.sh

# Development
./build.sh BuildDocs               # Build documentation (10s)
./build.sh Clean                   # Clean build artifacts
```

**Remember: NEVER CANCEL long-running commands. Set appropriate timeouts and wait for completion.**