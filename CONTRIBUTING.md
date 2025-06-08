# Contributing to CmdAI

Thank you for your interest in contributing to CmdAI! This document provides guidelines and information for contributors.

## 🤝 How to Contribute

### Reporting Issues
- **Search existing issues** before creating a new one
- **Use clear, descriptive titles** for your issues
- **Include steps to reproduce** any bugs
- **Provide environment details** (OS, .NET version, etc.)

### Suggesting Features
- **Check the roadmap** in the main README first
- **Open a discussion** for major feature proposals
- **Provide use cases** and examples of how the feature would be used

### Code Contributions

#### 1. Fork and Clone
```bash
# Fork the repository on GitHub, then:
git clone https://github.com/YOUR_USERNAME/cmdai.git
cd cmdai
```

#### 2. Set Up Development Environment
```bash
# Ensure you have .NET 8.0 SDK installed
dotnet --version

# Build the project
cd cmdai
dotnet build

# Run tests
dotnet test

# Build and install locally for testing
../scripts/build-dev.sh  # Linux/macOS
# or
..\scripts\build-dev.ps1  # Windows
```

#### 3. Create a Feature Branch
```bash
# Create and switch to a new branch
git checkout -b feature/your-feature-name

# Or for bug fixes:
git checkout -b fix/issue-description
```

#### 4. Make Your Changes
- **Follow existing code style** and patterns
- **Add tests** for new functionality
- **Update documentation** as needed
- **Keep commits focused** and atomic

#### 5. Test Your Changes
```bash
# Run all tests
dotnet test

# Test the CLI locally
cmdai --version
cmdai ask git "test command"

# Test with different scenarios
cmdai ask docker "list containers"
cmdai kubectl "get pods"
```

#### 6. Commit and Push
```bash
# Stage your changes
git add .

# Commit with descriptive message
git commit -m "feat: add support for npm commands"

# Push to your fork
git push origin feature/your-feature-name
```

#### 7. Open a Pull Request
- **Use the pull request template**
- **Reference related issues** using `#issue-number`
- **Provide clear description** of changes
- **Include screenshots** for UI changes (if applicable)

## 📝 Code Style Guidelines

### C# Conventions
- Follow [Microsoft C# Coding Conventions](https://docs.microsoft.com/en-us/dotnet/csharp/fundamentals/coding-style/coding-conventions)
- Use **PascalCase** for public members
- Use **camelCase** for private fields and local variables
- Use **meaningful variable names**
- **Avoid abbreviations** unless they're well-known

### Project Structure
```
cmdai/
├── src/
│   ├── CmdAi.Cli/          # CLI application
│   └── CmdAi.Core/         # Core business logic
│       ├── Interfaces/     # Service contracts
│       ├── Models/         # Data models
│       └── Services/       # Service implementations
└── tests/
    └── CmdAi.Tests/        # Unit and integration tests
```

### Adding New Command Resolvers
1. Create interface in `CmdAi.Core/Interfaces/`
2. Implement service in `CmdAi.Core/Services/`
3. Register in `Program.cs` dependency injection
4. Add tests in `CmdAi.Tests/`

### Adding New AI Providers
1. Implement `IAIProvider` interface
2. Add configuration to `AIConfiguration.cs`
3. Register in dependency injection
4. Update documentation

## 🧪 Testing Guidelines

### Test Categories
- **Unit Tests**: Test individual components in isolation
- **Integration Tests**: Test component interactions
- **CLI Tests**: Test end-to-end command execution

### Test Naming Convention
```csharp
[Test]
public void MethodName_StateUnderTest_ExpectedBehavior()
{
    // Arrange
    // Act
    // Assert
}
```

### Test Coverage
- **New features** must include tests
- **Bug fixes** should include regression tests
- Aim for **high test coverage** of core logic
- **Mock external dependencies** (AI providers, file system)

## 📚 Documentation

### Documentation Requirements
- **Update README.md** for new features
- **Add XML documentation** for public APIs
- **Update OLLAMA_SETUP.md** for AI-related changes
- **Include usage examples** in code comments

### Documentation Style
- Use **clear, concise language**
- Include **code examples** where helpful
- **Test all code samples** to ensure they work
- Use **consistent formatting** with existing docs

## 🔄 Release Process

### Version Bumping
```bash
# Bump version (automated)
./scripts/version-bump.sh patch  # For bug fixes
./scripts/version-bump.sh minor  # For new features
./scripts/version-bump.sh major  # For breaking changes
```

### Release Checklist
- [ ] All tests passing
- [ ] Documentation updated
- [ ] Version bumped appropriately
- [ ] CHANGELOG.md updated
- [ ] No breaking changes (unless major version)

## 🐛 Debugging Tips

### Local Development
```bash
# Build and install locally
./scripts/build-dev.sh

# Debug with verbose output
cmdai ask git "status" --verbose

# Check logs
cat ~/.cmdai/logs/cmdai.log
```

### Common Issues
- **Path issues**: Ensure `~/.dotnet/tools` is in PATH
- **AI not working**: Check Ollama is running on localhost:11434
- **Permission errors**: Run with appropriate permissions

## 📋 Pull Request Template

When creating a pull request, please include:

```markdown
## Description
Brief description of changes

## Type of Change
- [ ] Bug fix (non-breaking change which fixes an issue)
- [ ] New feature (non-breaking change which adds functionality)
- [ ] Breaking change (fix or feature that would cause existing functionality to not work as expected)
- [ ] Documentation update

## Testing
- [ ] Tests pass locally
- [ ] Added tests for new functionality
- [ ] Tested with multiple command types

## Checklist
- [ ] Code follows project style guidelines
- [ ] Self-review completed
- [ ] Documentation updated
- [ ] No breaking changes (or marked as such)
```

## 🎯 Areas for Contribution

### High Priority
- **Additional AI providers** (OpenAI, Anthropic, local models)
- **Shell integration** (bash, zsh, PowerShell completion)
- **Performance improvements** (caching, optimization)
- **Security enhancements** (command validation)

### Medium Priority
- **New command resolvers** (terraform, ansible, etc.)
- **Enhanced learning algorithms**
- **Configuration management improvements**
- **Error handling and user experience**

### Documentation
- **Tutorial videos** or blog posts
- **Translation** to other languages
- **API documentation** improvements
- **Setup guides** for different platforms

## 📞 Getting Help

### Community
- **GitHub Discussions**: For general questions and feature discussions
- **GitHub Issues**: For bug reports and specific problems

### Maintainer Contact
- **GitHub**: [@yoshiwatanabe](https://github.com/yoshiwatanabe)
- **Issues**: Use GitHub issues for project-related questions

## 📜 Code of Conduct

This project follows the [Contributor Covenant Code of Conduct](https://www.contributor-covenant.org/version/2/1/code_of_conduct/). By participating, you are expected to uphold this code.

### Our Standards
- **Be respectful** and inclusive
- **Welcome newcomers** and help them learn
- **Focus on constructive feedback**
- **Respect different viewpoints** and experiences

---

Thank you for contributing to CmdAI! 🎉