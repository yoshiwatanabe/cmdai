# Changelog

All notable changes to CmdAI will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [1.0.0] - 2025-06-08

### 🎉 Initial Release - AI Integration Milestone

This marks the first major release of CmdAI with comprehensive AI integration capabilities.

### Added
- **🤖 AI-Powered Command Resolution**
  - Local AI integration via Ollama and CodeLlama models
  - Universal CLI tool support through AI understanding
  - Smart context-aware command generation
  - Privacy-focused local processing (no cloud dependencies)

- **🛡️ Safety and Validation System**
  - Comprehensive command safety validation
  - Dangerous operation detection and warnings
  - Pattern-based fallback for critical commands
  - User confirmation for potentially harmful operations

- **📚 Continuous Learning**
  - Learning service with user feedback integration
  - Command success/failure tracking
  - Confidence scoring for generated commands
  - Persistent learning data storage

- **🔧 Development and Distribution**
  - .NET Global Tool packaging for easy installation
  - Automated version management and bumping scripts
  - Cross-platform build and development scripts
  - CI/CD automation with GitHub Actions

- **📖 Comprehensive Documentation**
  - Detailed Ollama setup guide with optimization tips
  - Complete versioning and release workflow documentation
  - Development and contribution guidelines
  - Architecture and design documentation

### Core Components
- **AICommandResolver**: Primary AI-driven command resolution
- **CommandValidator**: Safety checking and validation
- **FileLearningService**: Persistent learning and feedback system
- **OllamaAIProvider**: Local AI model integration
- **PatternCommandResolver**: Reliable fallback for Git/Azure CLI

### Supported Tools
- **Git**: Complete command pattern support with AI enhancement
- **Azure CLI (az)**: 25+ command patterns with AI fallback
- **Docker**: AI-powered command generation
- **Kubernetes (kubectl)**: AI-driven pod and cluster management
- **npm/yarn**: Package management via AI understanding
- **Universal**: Any CLI tool through AI comprehension

### Installation
```bash
# Install as global .NET tool
dotnet tool install --global CmdAi.Cli

# Or update existing installation
dotnet tool update --global CmdAi.Cli
```

### Configuration
- Local AI configuration via `appsettings.json`
- Environment variable overrides
- Flexible AI provider system (currently supports Ollama)
- Learning and fallback behavior customization

### Platform Support
- **Windows**: Full support with PowerShell scripts
- **macOS**: Native support with shell scripts
- **Linux**: Native support with shell scripts
- **.NET 8.0**: Minimum runtime requirement

### Breaking Changes
- None (initial release)

### Migration Notes
- This is the initial public release
- No migration required from previous versions

---

## Version History

### Development Timeline
- **v0.x.x**: Internal development and MVP iterations
- **v1.0.0**: First public release with AI integration

### Upcoming Releases
- **v1.1.x**: Enhanced AI features, additional providers
- **v1.2.x**: Shell integration, auto-completion
- **v2.0.x**: Major architecture improvements (when needed)

---

## Contributing

See [CONTRIBUTING.md](CONTRIBUTING.md) for guidelines on contributing to CmdAI.

## Support

- **Issues**: [GitHub Issues](https://github.com/yoshiwatanabe/cmdai/issues)
- **Discussions**: [GitHub Discussions](https://github.com/yoshiwatanabe/cmdai/discussions)
- **Documentation**: [README.md](README.md) and [OLLAMA_SETUP.md](cmdai/OLLAMA_SETUP.md)