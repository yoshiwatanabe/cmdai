#!/bin/bash

# CmdAI Version Bumping Script
set -e

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[0;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

PROJECT_FILE="$(dirname "$0")/../cmdai/src/CmdAi.Cli/CmdAi.Cli.csproj"

usage() {
    echo "Usage: $0 <major|minor|patch> [version]"
    echo ""
    echo "Examples:"
    echo "  $0 patch              # Bump patch version (1.0.0 -> 1.0.1)"
    echo "  $0 minor              # Bump minor version (1.0.0 -> 1.1.0)"
    echo "  $0 major              # Bump major version (1.0.0 -> 2.0.0)"
    echo "  $0 patch 1.2.3       # Set specific version"
    echo ""
    exit 1
}

get_current_version() {
    grep -o '<Version>[^<]*</Version>' "$PROJECT_FILE" | sed 's/<Version>\(.*\)<\/Version>/\1/'
}

update_version() {
    local new_version=$1
    
    # Update Version
    sed -i "s|<Version>[^<]*</Version>|<Version>$new_version</Version>|" "$PROJECT_FILE"
    
    # Update AssemblyVersion (major.minor.0.0)
    local major=$(echo $new_version | cut -d. -f1)
    local minor=$(echo $new_version | cut -d. -f2)
    sed -i "s|<AssemblyVersion>[^<]*</AssemblyVersion>|<AssemblyVersion>$major.$minor.0.0</AssemblyVersion>|" "$PROJECT_FILE"
    
    # Update FileVersion (full version + .0)
    sed -i "s|<FileVersion>[^<]*</FileVersion>|<FileVersion>$new_version.0</FileVersion>|" "$PROJECT_FILE"
    
    # Update AssemblyInformationalVersion
    sed -i "s|<AssemblyInformationalVersion>[^<]*</AssemblyInformationalVersion>|<AssemblyInformationalVersion>$new_version</AssemblyInformationalVersion>|" "$PROJECT_FILE"
}

increment_version() {
    local version=$1
    local bump_type=$2
    
    local major=$(echo $version | cut -d. -f1)
    local minor=$(echo $version | cut -d. -f2)
    local patch=$(echo $version | cut -d. -f3)
    
    case $bump_type in
        major)
            major=$((major + 1))
            minor=0
            patch=0
            ;;
        minor)
            minor=$((minor + 1))
            patch=0
            ;;
        patch)
            patch=$((patch + 1))
            ;;
        *)
            echo -e "${RED}Invalid bump type: $bump_type${NC}"
            usage
            ;;
    esac
    
    echo "$major.$minor.$patch"
}

# Check arguments
if [ $# -lt 1 ] || [ $# -gt 2 ]; then
    usage
fi

BUMP_TYPE=$1
SPECIFIC_VERSION=$2

# Check if project file exists
if [ ! -f "$PROJECT_FILE" ]; then
    echo -e "${RED}Project file not found: $PROJECT_FILE${NC}"
    exit 1
fi

# Get current version
CURRENT_VERSION=$(get_current_version)
if [ -z "$CURRENT_VERSION" ]; then
    echo -e "${RED}Could not find current version in project file${NC}"
    exit 1
fi

echo -e "${BLUE}Current version: $CURRENT_VERSION${NC}"

# Calculate new version
if [ -n "$SPECIFIC_VERSION" ]; then
    # Validate version format
    if [[ ! $SPECIFIC_VERSION =~ ^[0-9]+\.[0-9]+\.[0-9]+$ ]]; then
        echo -e "${RED}Invalid version format: $SPECIFIC_VERSION${NC}"
        echo "Version must be in format: major.minor.patch (e.g., 1.2.3)"
        exit 1
    fi
    NEW_VERSION=$SPECIFIC_VERSION
else
    NEW_VERSION=$(increment_version "$CURRENT_VERSION" "$BUMP_TYPE")
fi

echo -e "${YELLOW}New version: $NEW_VERSION${NC}"

# Confirm the change
read -p "Update version from $CURRENT_VERSION to $NEW_VERSION? (y/N): " confirm
if [[ ! $confirm =~ ^[Yy]$ ]]; then
    echo "Version update cancelled."
    exit 0
fi

# Update the version
update_version "$NEW_VERSION"

echo -e "${GREEN}✅ Version updated successfully!${NC}"
echo ""
echo "Changes made:"
echo "  Version: $CURRENT_VERSION → $NEW_VERSION"
echo "  AssemblyVersion: Updated to match major.minor"
echo "  FileVersion: Updated to match full version"
echo ""
echo "Next steps:"
echo "  1. Review changes: git diff $PROJECT_FILE"
echo "  2. Test build: ./scripts/build-dev.sh"
echo "  3. Commit changes: git add . && git commit -m \"chore: bump version to v$NEW_VERSION\""
echo "  4. Tag release: git tag v$NEW_VERSION"
echo "  5. Push changes: git push && git push --tags"