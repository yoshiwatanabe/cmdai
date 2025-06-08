# 🔄 CmdAI Versioning and Release Guide

This document outlines the versioning strategy, development workflow, and release process for CmdAI.

## 📋 Versioning Strategy

CmdAI follows [Semantic Versioning (SemVer)](https://semver.org/) with the format `MAJOR.MINOR.PATCH`:

- **MAJOR**: Incompatible API changes or breaking changes
- **MINOR**: New functionality added in a backward compatible manner
- **PATCH**: Backward compatible bug fixes

### Current Version: `1.0.0`
- **v1.0.0**: AI integration milestone with Ollama support

### Upcoming Releases
- **v1.1.x**: Enhanced AI features, additional providers
- **v1.2.x**: Shell integration, auto-completion
- **v2.0.x**: Major architecture changes (when needed)

## 🛠️ Development Workflow

### 1. Version Management

Use the automated version bumping scripts for consistent versioning:

```bash
# Bump patch version (1.0.0 -> 1.0.1)
./scripts/version-bump.sh patch

# Bump minor version (1.0.0 -> 1.1.0)  
./scripts/version-bump.sh minor

# Bump major version (1.0.0 -> 2.0.0)
./scripts/version-bump.sh major

# Set specific version
./scripts/version-bump.sh patch 1.2.3
```

**Windows:**
```powershell
.\scripts\version-bump.ps1 patch
.\scripts\version-bump.ps1 minor -SpecificVersion 1.2.3
```

### 2. Development Build and Testing

Test your changes locally before releasing:

```bash
# Build and install locally for testing
./scripts/build-dev.sh

# Verify the installation
cmdai --version
cmdai ask git "check status"
```

**Windows:**
```powershell
.\scripts\build-dev.ps1
```

### 3. Release Process

#### Option A: Manual Release (Recommended for patches)

```bash
# 1. Bump version
./scripts/version-bump.sh patch

# 2. Test build locally
./scripts/build-dev.sh

# 3. Commit and tag
git add .
git commit -m "chore: bump version to v1.0.1"
git tag v1.0.1

# 4. Push to trigger CI/CD
git push
git push --tags

# 5. Publish to NuGet (requires API key)
./scripts/publish-nuget.sh
```

#### Option B: GitHub Actions (Recommended for releases)

1. **Push tag** to trigger automated release:
   ```bash
   git tag v1.1.0
   git push --tags
   ```

2. **Manual trigger** via GitHub Actions:
   - Go to Actions → Release workflow
   - Click "Run workflow"
   - Enter version (e.g., `1.1.0`)

## 📦 Distribution Channels

### 1. NuGet.org (Primary)
- **Package ID**: `CmdAi.Cli`
- **Install**: `dotnet tool install --global CmdAi.Cli`
- **Update**: `dotnet tool update --global CmdAi.Cli`
- **Uninstall**: `dotnet tool uninstall --global CmdAi.Cli`

### 2. GitHub Releases
- Automated release notes
- Downloadable `.nupkg` files
- Release artifacts

## 🔧 Configuration Files

### Project Configuration
The main configuration is in `cmdai/src/CmdAi.Cli/CmdAi.Cli.csproj`:

```xml
<PropertyGroup>
  <!-- Versioning -->
  <Version>1.0.0</Version>
  <AssemblyVersion>1.0.0.0</AssemblyVersion>
  <FileVersion>1.0.0.0</FileVersion>
  
  <!-- .NET Global Tool Configuration -->
  <PackAsTool>true</PackAsTool>
  <ToolCommandName>cmdai</ToolCommandName>
  
  <!-- Package Metadata -->
  <PackageId>CmdAi.Cli</PackageId>
  <Authors>Yoshi Watanabe</Authors>
  <Description>AI-powered CLI assistant...</Description>
</PropertyGroup>
```

### CI/CD Workflows
- **`.github/workflows/ci.yml`**: Continuous integration on PR/push
- **`.github/workflows/release.yml`**: Automated releases on tag push

## 🚀 User Upgrade Experience

### For End Users
```bash
# Check current version
cmdai --version          # Shows version number
cmdai version           # Shows detailed version info

# Update to latest version
dotnet tool update --global CmdAi.Cli

# Install specific version
dotnet tool install --global CmdAi.Cli --version 1.2.0
```

### PATH Setup
If `cmdai` command is not found after installation:

**Linux/macOS:**
```bash
echo 'export PATH="$PATH:$HOME/.dotnet/tools"' >> ~/.bashrc
source ~/.bashrc
```

**Windows (PowerShell):**
```powershell
# Usually handled automatically, but if needed:
$env:PATH += ";$env:USERPROFILE\.dotnet\tools"
```

### Version Compatibility
- **Backward Compatible**: Minor and patch versions
- **Configuration**: Existing `appsettings.json` files remain compatible
- **Learning Data**: `cmdai_learning.json` format is preserved across versions

## 🔍 Troubleshooting

### Common Issues

#### Version Update Not Working
```bash
# Force reinstall
dotnet tool uninstall --global CmdAi.Cli
dotnet tool install --global CmdAi.Cli
```

#### Multiple Versions Installed
```bash
# List all .NET tools
dotnet tool list --global

# Clean and reinstall
dotnet tool uninstall --global CmdAi.Cli
dotnet tool install --global CmdAi.Cli
```

#### Development Build Issues
```bash
# Clean everything and rebuild
cd cmdai/src/CmdAi.Cli
rm -rf bin obj nupkg
dotnet clean
./scripts/build-dev.sh
```

## 📊 Release Checklist

### Pre-Release
- [ ] All tests passing
- [ ] Version bumped appropriately
- [ ] CHANGELOG.md updated
- [ ] Documentation updated
- [ ] Local testing completed
- [ ] Breaking changes documented

### Release
- [ ] Tag created and pushed
- [ ] GitHub Actions completed successfully
- [ ] NuGet package published
- [ ] GitHub release created
- [ ] Release notes published

### Post-Release
- [ ] Verify installation: `dotnet tool install --global CmdAi.Cli`
- [ ] Test upgrade: `dotnet tool update --global CmdAi.Cli`
- [ ] Update documentation if needed
- [ ] Announce release (if major/minor)

## 🔗 Useful Links

- **NuGet Package**: https://www.nuget.org/packages/CmdAi.Cli
- **GitHub Repository**: https://github.com/yoshiwatanabe/cmdai
- **Semantic Versioning**: https://semver.org/
- **NuGet API Keys**: https://www.nuget.org/account/apikeys
- **.NET Global Tools**: https://docs.microsoft.com/en-us/dotnet/core/tools/global-tools

## 🤝 Contributing to Releases

When contributing features or fixes:

1. **Feature branches** for new functionality
2. **Patch branches** for bug fixes
3. **Version bumps** happen on `main` branch only
4. **Breaking changes** require major version bump and clear documentation

For more details, see [CONTRIBUTING.md](CONTRIBUTING.md).