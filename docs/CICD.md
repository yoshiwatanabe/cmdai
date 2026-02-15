# 🚀 CI/CD Setup Guide

This document provides a comprehensive guide for setting up and using the CI/CD pipeline for CmdAI.

## 📋 Overview

CmdAI uses **GitHub Actions** for continuous integration and continuous deployment. The CI/CD pipeline consists of two main workflows:

1. **CI Workflow** (`.github/workflows/ci.yml`) - Runs on every push and pull request
2. **Release Workflow** (`.github/workflows/release.yml`) - Handles releases when tags are pushed

## ✅ Current Setup

The CI/CD infrastructure is **already configured** in this repository. You have:

- ✅ Automated testing across multiple platforms (Ubuntu, Windows, macOS)
- ✅ Automated building and packaging
- ✅ Release workflow triggered by Git tags
- ✅ NuGet package publishing
- ✅ GitHub Release creation

## 🔧 Prerequisites

Before you can release packages, you need to configure the following:

### 1. NuGet API Key (Required for Publishing)

To publish packages to NuGet.org, you need to set up a NuGet API key:

#### Step 1: Get Your NuGet API Key

1. Go to [NuGet.org](https://www.nuget.org/) and sign in
2. Navigate to **Account Settings** → **API Keys**
3. Click **Create** to generate a new API key
4. Configure the API key:
   - **Key Name**: `CmdAI GitHub Actions`
   - **Package owner**: Select your account
   - **Glob Pattern**: `CmdAi.Cli` (or `*` for all packages)
   - **Scopes**: Select `Push` and `Push new packages and package versions`
   - **Expiration**: Choose an appropriate duration (e.g., 365 days)
5. Click **Create**
6. **Important**: Copy the API key immediately - you won't be able to see it again!

#### Step 2: Add API Key to GitHub Secrets

1. Go to your GitHub repository: `https://github.com/yoshiwatanabe/cmdai`
2. Navigate to **Settings** → **Secrets and variables** → **Actions**
3. Click **New repository secret**
4. Configure the secret:
   - **Name**: `NUGET_API_KEY`
   - **Value**: Paste your NuGet API key
5. Click **Add secret**

## 🔄 How CI/CD Works

### Continuous Integration (CI)

The CI workflow runs automatically on:
- Every push to `main` or `develop` branches
- Every pull request to `main` branch

**What it does:**
1. ✅ Checks out the code
2. ✅ Sets up .NET 8.0
3. ✅ Restores dependencies
4. ✅ Builds the solution in Release configuration
5. ✅ Runs all tests with code coverage
6. ✅ Tests packaging (creates .nupkg)
7. ✅ Uploads test results as artifacts

**Platforms tested:**
- Ubuntu (Linux)
- Windows
- macOS

### Release Workflow

The Release workflow can be triggered in two ways:

#### Option A: Automatic (Tag Push) - Recommended

When you push a Git tag starting with `v`, the workflow:
1. ✅ Extracts version from the tag
2. ✅ Updates version in project file
3. ✅ Builds and tests the solution
4. ✅ Creates NuGet package
5. ✅ Publishes to NuGet.org (if NUGET_API_KEY is configured)
6. ✅ Creates GitHub Release with release notes

#### Option B: Manual Trigger

You can also manually trigger a release from GitHub Actions UI:
1. Go to **Actions** → **Release** workflow
2. Click **Run workflow**
3. Enter the version number (e.g., `1.2.7`)
4. The workflow creates a package but doesn't publish (unless it's a tag push)

## 🎯 Release Process

### Quick Release Guide

Here's how to release a new version:

#### 1. Prepare the Release

```bash
# Make sure you're on the main branch and it's up to date
git checkout main
git pull

# Bump the version (choose one)
./scripts/version-bump.sh patch    # 1.2.6 → 1.2.7
./scripts/version-bump.sh minor    # 1.2.6 → 1.3.0
./scripts/version-bump.sh major    # 1.2.6 → 2.0.0
```

**Windows:**
```powershell
.\scripts\version-bump.ps1 patch
```

#### 2. Update CHANGELOG.md

Edit `CHANGELOG.md` to document the changes in this release:

```markdown
## [1.2.7] - 2026-02-15

### Added
- New feature X

### Fixed
- Bug Y
- Issue Z
```

#### 3. Commit Changes

```bash
# Commit the version bump and changelog
git add .
git commit -m "chore: bump version to v1.2.7"
git push
```

#### 4. Create and Push Tag

```bash
# Create a Git tag matching the version
git tag v1.2.7

# Push the tag to trigger the release workflow
git push origin v1.2.7
```

#### 5. Monitor the Release

1. Go to **Actions** tab in GitHub
2. Watch the **Release** workflow run
3. Verify it completes successfully

The workflow will:
- Build and test the project
- Publish to NuGet.org (if API key is configured)
- Create a GitHub Release with auto-generated notes

#### 6. Verify the Release

After the workflow completes:

**Check NuGet.org:**
```bash
# Search for your package
dotnet tool search CmdAi.Cli

# Or install it
dotnet tool install --global CmdAi.Cli --version 1.2.7
```

**Check GitHub Releases:**
- Go to: `https://github.com/yoshiwatanabe/cmdai/releases`
- Verify the new release appears with correct version and notes

## 🛠️ Testing Locally Before Release

Before creating a release, always test locally:

```bash
# Build and install locally
./scripts/build-dev.sh

# Test the tool
cmdai --version
cmdai "test command"

# If on Windows
.\scripts\build-dev.ps1
```

## 📦 Package Distribution

### NuGet.org (Primary Distribution)

Once published, users can install via:

```bash
# Install
dotnet tool install --global CmdAi.Cli

# Update
dotnet tool update --global CmdAi.Cli

# Install specific version
dotnet tool install --global CmdAi.Cli --version 1.2.7
```

### GitHub Releases (Backup Distribution)

Users can also download `.nupkg` files directly from GitHub Releases and install:

```bash
# Download the .nupkg from GitHub Release
# Then install locally
dotnet tool install --global --add-source /path/to/nupkg CmdAi.Cli
```

## 🔍 Troubleshooting

### Release Workflow Fails

**Problem**: Release workflow fails with "No NuGet API Key"
**Solution**: Make sure `NUGET_API_KEY` secret is configured in GitHub (see Prerequisites)

**Problem**: Tests fail during release
**Solution**: 
- Run tests locally first: `dotnet test`
- Fix failing tests before creating release
- Don't release with failing tests

**Problem**: Version conflict on NuGet
**Solution**: 
- Check if version already exists on NuGet.org
- Bump to a new version
- The workflow uses `--skip-duplicate` flag to avoid errors

### CI Workflow Fails

**Problem**: CI fails on specific platform (e.g., macOS)
**Solution**:
- Check the specific error in Actions logs
- May need platform-specific fixes
- Can temporarily disable problematic platform in matrix

### Manual Release Testing

To test the release process without publishing:

```bash
# Build and pack locally
cd src/CmdAi.Cli
dotnet clean
dotnet build --configuration Release
dotnet pack --configuration Release --output ./nupkg

# Test the package locally
dotnet tool uninstall --global CmdAi.Cli || true
dotnet tool install --global --add-source ./nupkg CmdAi.Cli
cmdai --version
```

## 📊 Workflow Configuration

### CI Workflow Configuration

Location: `.github/workflows/ci.yml`

Key settings:
- **Triggers**: Push to `main`/`develop`, PRs to `main`
- **Platforms**: Ubuntu, Windows, macOS
- **.NET Version**: 8.0.x
- **Configuration**: Release

### Release Workflow Configuration

Location: `.github/workflows/release.yml`

Key settings:
- **Triggers**: Tags matching `v*` pattern, manual dispatch
- **Platform**: Ubuntu only (for consistency)
- **.NET Version**: 8.0.x
- **Configuration**: Release
- **Secrets Required**: `NUGET_API_KEY`

## 🔐 Security Best Practices

1. **Never commit API keys** to the repository
2. **Use GitHub Secrets** for sensitive data
3. **Rotate API keys** periodically (e.g., yearly)
4. **Use scoped API keys** (only push permission for CmdAi.Cli)
5. **Monitor release notifications** from NuGet and GitHub

## 📚 Additional Resources

- [GitHub Actions Documentation](https://docs.github.com/en/actions)
- [NuGet Package Publishing](https://docs.microsoft.com/en-us/nuget/nuget-org/publish-a-package)
- [Semantic Versioning](https://semver.org/)
- [Keep a Changelog](https://keepachangelog.com/)

## 🤝 Contributing

For more information about contributing and the development workflow, see:
- [CONTRIBUTING.md](CONTRIBUTING.md)
- [VERSIONING.md](VERSIONING.md)
- [INSTALL.md](INSTALL.md)

## 📝 Summary Checklist

To release the latest version, you need to:

- [ ] Configure `NUGET_API_KEY` secret in GitHub (one-time setup)
- [ ] Bump version using `./scripts/version-bump.sh`
- [ ] Update `CHANGELOG.md` with release notes
- [ ] Commit changes: `git commit -m "chore: bump version to vX.Y.Z"`
- [ ] Create tag: `git tag vX.Y.Z`
- [ ] Push tag: `git push origin vX.Y.Z`
- [ ] Monitor GitHub Actions workflow
- [ ] Verify release on NuGet.org and GitHub Releases

**That's it!** The CI/CD pipeline handles the rest automatically. 🎉
