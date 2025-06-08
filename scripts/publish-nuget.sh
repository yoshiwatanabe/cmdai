#!/bin/bash

# CmdAI NuGet Publishing Script
set -e

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[0;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

PROJECT_DIR="$(dirname "$0")/../cmdai/src/CmdAi.Cli"
NUGET_SOURCE="https://api.nuget.org/v3/index.json"

usage() {
    echo "Usage: $0 [API_KEY]"
    echo ""
    echo "Publishes CmdAI to NuGet.org"
    echo ""
    echo "API_KEY can be provided as:"
    echo "  - Command line argument"
    echo "  - Environment variable NUGET_API_KEY"
    echo "  - Stored in ~/.nuget/NuGet/NuGet.Config"
    echo ""
    echo "Get your API key from: https://www.nuget.org/account/apikeys"
    echo ""
    exit 1
}

check_dependencies() {
    if ! command -v dotnet &> /dev/null; then
        echo -e "${RED}❌ .NET CLI not found. Please install .NET 8.0 SDK${NC}"
        exit 1
    fi
}

get_api_key() {
    local api_key=""
    
    # Check command line argument
    if [ -n "$1" ]; then
        api_key="$1"
    # Check environment variable
    elif [ -n "$NUGET_API_KEY" ]; then
        api_key="$NUGET_API_KEY"
    else
        echo -e "${YELLOW}⚠️  No API key provided${NC}"
        echo "You can:"
        echo "1. Pass it as argument: $0 <your-api-key>"
        echo "2. Set environment variable: export NUGET_API_KEY=<your-api-key>"
        echo "3. Configure it globally: dotnet nuget push will prompt for it"
        echo ""
        read -p "Continue without API key? (y/N): " confirm
        if [[ ! $confirm =~ ^[Yy]$ ]]; then
            exit 1
        fi
    fi
    
    echo "$api_key"
}

get_current_version() {
    grep -o '<Version>[^<]*</Version>' "$PROJECT_DIR/CmdAi.Cli.csproj" | sed 's/<Version>\(.*\)<\/Version>/\1/'
}

build_and_pack() {
    echo -e "${BLUE}🔨 Building and packing CmdAI...${NC}"
    
    cd "$PROJECT_DIR"
    
    # Clean previous builds
    rm -rf ./nupkg
    rm -rf ./bin/Release
    rm -rf ./obj
    
    # Build in release mode
    dotnet build --configuration Release
    
    # Create package
    dotnet pack --configuration Release --output ./nupkg --no-build
    
    # Find the package file
    local package_file=$(find ./nupkg -name "CmdAi.Cli.*.nupkg" | head -1)
    if [ -z "$package_file" ]; then
        echo -e "${RED}❌ Package file not found!${NC}"
        exit 1
    fi
    
    echo -e "${GREEN}📦 Package created: $package_file${NC}"
    echo "$package_file"
}

publish_package() {
    local package_file="$1"
    local api_key="$2"
    
    echo -e "${BLUE}🚀 Publishing to NuGet.org...${NC}"
    
    # Build the push command
    local push_cmd="dotnet nuget push \"$package_file\" --source \"$NUGET_SOURCE\" --skip-duplicate"
    
    if [ -n "$api_key" ]; then
        push_cmd="$push_cmd --api-key \"$api_key\""
    fi
    
    # Execute the command
    eval $push_cmd
    
    if [ $? -eq 0 ]; then
        echo -e "${GREEN}✅ Package published successfully!${NC}"
        return 0
    else
        echo -e "${RED}❌ Publishing failed${NC}"
        return 1
    fi
}

# Main script
echo -e "${GREEN}CmdAI NuGet Publishing Script${NC}"
echo "=================================="

# Check dependencies
check_dependencies

# Get API key
API_KEY=$(get_api_key "$1")

# Check if we're in a git repo and on main branch
if git rev-parse --git-dir > /dev/null 2>&1; then
    current_branch=$(git branch --show-current)
    if [ "$current_branch" != "main" ]; then
        echo -e "${YELLOW}⚠️  Warning: Not on main branch (current: $current_branch)${NC}"
        read -p "Continue anyway? (y/N): " confirm
        if [[ ! $confirm =~ ^[Yy]$ ]]; then
            exit 1
        fi
    fi
    
    # Check for uncommitted changes
    if ! git diff-index --quiet HEAD --; then
        echo -e "${YELLOW}⚠️  Warning: Uncommitted changes detected${NC}"
        read -p "Continue anyway? (y/N): " confirm
        if [[ ! $confirm =~ ^[Yy]$ ]]; then
            exit 1
        fi
    fi
fi

# Get current version
CURRENT_VERSION=$(get_current_version)
echo -e "${BLUE}Current version: $CURRENT_VERSION${NC}"

# Confirm publishing
echo ""
read -p "Publish CmdAI v$CURRENT_VERSION to NuGet.org? (y/N): " confirm
if [[ ! $confirm =~ ^[Yy]$ ]]; then
    echo "Publishing cancelled."
    exit 0
fi

# Build and pack
PACKAGE_FILE=$(build_and_pack)

# Publish
if publish_package "$PACKAGE_FILE" "$API_KEY"; then
    echo ""
    echo -e "${GREEN}🎉 Success! CmdAI v$CURRENT_VERSION is now available on NuGet.org${NC}"
    echo ""
    echo "Users can install/update with:"
    echo -e "${BLUE}  dotnet tool install --global CmdAi.Cli${NC}"
    echo -e "${BLUE}  dotnet tool update --global CmdAi.Cli${NC}"
    echo ""
    echo "Package URL: https://www.nuget.org/packages/CmdAi.Cli/$CURRENT_VERSION"
else
    echo ""
    echo -e "${RED}Publishing failed. Please check the error messages above.${NC}"
    exit 1
fi