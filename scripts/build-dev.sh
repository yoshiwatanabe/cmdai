#!/bin/bash

# CmdAI Development Build and Install Script
set -e

echo "🔨 Building CmdAI for development..."

# Navigate to project directory
cd "$(dirname "$0")/../src/CmdAi.Cli"

# Clean previous builds
echo "🧹 Cleaning previous builds..."
rm -rf ./nupkg
rm -rf ./bin
rm -rf ./obj

# Build and pack the tool
echo "📦 Building and packing CmdAI..."
dotnet pack --configuration Release --output ./nupkg

# Get the package file
PACKAGE_FILE=$(find ./nupkg -name "CmdAi.Cli.*.nupkg" | head -1)
if [ -z "$PACKAGE_FILE" ]; then
    echo "❌ Package file not found!"
    exit 1
fi

echo "📄 Package created: $PACKAGE_FILE"

# Uninstall existing version (ignore errors)
echo "🗑️ Uninstalling existing version..."
dotnet tool uninstall --global CmdAi.Cli 2>/dev/null || true

# Install the new version
echo "⚡ Installing new version..."
dotnet tool install --global --add-source ./nupkg CmdAi.Cli

echo "✅ CmdAI development build complete!"
echo ""
echo "Test the installation:"
echo "  cmdai --version"
echo "  cmdai ask git \"check status\""
echo ""
echo "To uninstall: dotnet tool uninstall --global CmdAi.Cli"