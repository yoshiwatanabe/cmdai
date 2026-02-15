# 🚀 Quick Start: Releasing a New Version

This is a quick reference guide for releasing a new version of CmdAI. For detailed instructions, see [docs/CICD.md](docs/CICD.md).

## ⚡ One-Time Setup

1. **Get NuGet API Key**:
   - Go to [NuGet.org](https://www.nuget.org/) → Account Settings → API Keys
   - Create new key with `Push` permission for `CmdAi.Cli`
   - Copy the key

2. **Add to GitHub**:
   - Go to repository Settings → Secrets and variables → Actions
   - Add secret named `NUGET_API_KEY` with your NuGet key

## 📦 Release Steps

### 1. Bump Version

```bash
# Patch release (1.2.6 → 1.2.7)
./scripts/version-bump.sh patch

# Minor release (1.2.6 → 1.3.0)
./scripts/version-bump.sh minor

# Major release (1.2.6 → 2.0.0)
./scripts/version-bump.sh major
```

**Windows:**
```powershell
.\scripts\version-bump.ps1 patch
```

### 2. Update CHANGELOG.md

Add release notes:

```markdown
## [1.2.7] - 2026-02-15

### Added
- New feature description

### Fixed
- Bug fix description
```

### 3. Commit & Tag

```bash
# Commit changes
git add .
git commit -m "chore: bump version to v1.2.7"
git push

# Create and push tag
git tag v1.2.7
git push origin v1.2.7
```

### 4. Monitor Release

- Go to **Actions** tab on GitHub
- Watch the **Release** workflow
- Verify it completes successfully

### 5. Verify

**Check NuGet:**
```bash
dotnet tool search CmdAi.Cli
```

**Check GitHub:**
- Visit: https://github.com/yoshiwatanabe/cmdai/releases

## 🧪 Test Before Release

```bash
# Build and test locally
./scripts/build-dev.sh

# Verify
cmdai --version
cmdai "test command"
```

**Windows:**
```powershell
.\scripts\build-dev.ps1
```

## 📋 What the CI/CD Does Automatically

When you push a tag:

1. ✅ Extracts version from tag
2. ✅ Builds the solution
3. ✅ Runs all tests
4. ✅ Creates NuGet package
5. ✅ Publishes to NuGet.org
6. ✅ Creates GitHub Release

## 🔍 Troubleshooting

**Workflow fails with "No NuGet API Key"**
- Add `NUGET_API_KEY` secret in GitHub Settings

**Tests fail**
- Run `dotnet test` locally and fix issues first

**Version already exists**
- Bump to a new version number
- Check what's on NuGet.org: https://www.nuget.org/packages/CmdAi.Cli

## 📚 More Information

- Full CI/CD guide: [docs/CICD.md](docs/CICD.md)
- Versioning details: [docs/VERSIONING.md](docs/VERSIONING.md)
- Contributing guide: [docs/CONTRIBUTING.md](docs/CONTRIBUTING.md)

## 🎯 Current Version

Current version: **1.2.6**

To release **1.2.7**:
```bash
./scripts/version-bump.sh patch
# Update CHANGELOG.md
git add . && git commit -m "chore: bump version to v1.2.7"
git tag v1.2.7 && git push && git push origin v1.2.7
```

**That's it!** 🎉
