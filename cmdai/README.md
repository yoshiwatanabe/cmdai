# CmdAI - AI-Powered CLI Assistant

An extensible CLI assistant that translates natural language to CLI commands using pattern matching, with a clean architecture ready for future AI integration.

## 🚀 What is CmdAI?

CmdAI helps you run CLI commands using natural language. Instead of remembering exact command syntax, just describe what you want to do:

```bash
# Git commands
cmdai ask git "check the status"          → git status
cmdai git "undo last commit"              → git reset --soft HEAD~1  
cmdai ask git "add all files"             → git add .
cmdai ask git "show me the history"       → git log --oneline

# Azure CLI commands
cmdai ask az "list subscriptions"         → az account list --output table
cmdai az "show current subscription"      → az account show
cmdai ask az "list storage accounts"      → az storage account list --output table
cmdai ask az "create resource group MyRG" → az group create --name "MyRG" --location eastus
```

## 🏗️ Architecture

**Command Pipeline**: User Input → Intent Parser → Command Resolver → Executor → Output

### Core Interfaces
- **`ICommandResolver`**: Resolves natural language to commands
- **`ICommandExecutor`**: Executes commands with user confirmation  
- **`IContextProvider`**: Provides execution context (git repo detection, etc.)
- **`ICommandRepository`**: Repository pattern for future web-sourced commands

### Current Implementation
- **Phase 1**: Pattern-based resolver using regex matching
- **Phase 2** (planned): AI-powered command resolution
- **Extensible**: Easy to add new tools (Azure CLI, Docker, etc.)

## 📖 CLI Syntax

```bash
# Ask syntax - explicit tool specification
cmdai ask git "how do I check status"
cmdai ask az "list subscriptions"
cmdai ask az "list resources in myResourceGroup"

# Direct syntax - tool name as command  
cmdai git "undo last commit"
cmdai az "show current subscription"
cmdai azure "list storage accounts"
```

## ✨ Features

- **Safe Execution**: User confirmation before running commands
- **Context Aware**: Detects git repositories and working directory
- **Pattern Matching**: 30+ command patterns for Git and Azure CLI supported
- **Extensible**: Clean architecture for adding new tools
- **Cross-platform**: Works on Windows, macOS, and Linux

## 🛠️ Installation & Usage

### Prerequisites
- .NET 8.0 SDK or later

### Build & Run
```bash
# Clone the repository
git clone https://github.com/yourusername/cmdai.git
cd cmdai

# Build the project
dotnet build

# Run examples
dotnet run --project src/CmdAi.Cli -- ask git "status command"
dotnet run --project src/CmdAi.Cli -- git "undo last commit"
dotnet run --project src/CmdAi.Cli -- ask az "list subscriptions"
dotnet run --project src/CmdAi.Cli -- az "show current subscription"
```

### Install as Global Tool (Future)
```bash
dotnet tool install -g cmdai
cmdai ask git "status command"
```

## 📋 Supported Commands

### Git Commands
| Natural Language | Generated Command | Description |
|------------------|-------------------|-------------|
| "status command", "check status" | `git status` | Show working tree status |
| "undo last commit" | `git reset --soft HEAD~1` | Undo last commit, keep changes staged |
| "add all files" | `git add .` | Stage all changes |
| "commit changes" | `git commit` | Create a new commit |
| "show history" | `git log --oneline` | Show commit history |
| "what changed" | `git diff` | Show file differences |
| "push changes" | `git push` | Push to remote repository |
| "pull changes" | `git pull` | Pull from remote repository |

### Azure CLI Commands
| Natural Language | Generated Command | Description |
|------------------|-------------------|-------------|
| "list subscriptions" | `az account list --output table` | List all available subscriptions |
| "show current subscription" | `az account show` | Show current subscription details |
| "switch subscription" | `az account set --subscription` | Switch to a specific subscription |
| "list resource groups" | `az group list --output table` | List all resource groups |
| "create resource group MyRG" | `az group create --name "MyRG" --location eastus` | Create a new resource group |
| "list resources in MyRG" | `az resource list --resource-group "MyRG" --output table` | List resources in resource group |
| "list storage accounts" | `az storage account list --output table` | List all storage accounts |
| "list virtual machines" | `az vm list --output table` | List all virtual machines |
| "list web apps" | `az webapp list --output table` | List all web apps |
| "list key vaults" | `az keyvault list --output table` | List all key vaults |
| "list locations" | `az account list-locations --output table` | List all available Azure regions |
| "login" | `az login` | Login to Azure |

## 🔮 Roadmap

### Phase 1: Pattern-Based MVP ✅
- [x] Core architecture and interfaces
- [x] System.CommandLine integration  
- [x] Git command pattern matching (15+ patterns)
- [x] Azure CLI command pattern matching (25+ patterns)
- [x] User confirmation workflow
- [x] Context awareness (git repo detection)
- [x] Composite resolver supporting multiple tools

### Phase 2: AI Integration (Planned)
- [ ] OpenAI/Claude integration for command resolution
- [ ] Learning from user corrections
- [ ] Support for complex, multi-step operations
- [ ] Natural language explanations of commands

### Phase 3: Extended Tool Support (Planned)
- [x] Azure CLI (`az`) commands ✅
- [ ] Docker commands
- [ ] kubectl (Kubernetes) commands  
- [ ] npm/yarn package manager commands
- [ ] Web-sourced command repository

## 🤝 Contributing

Contributions are welcome! The codebase is designed to be easily extensible:

1. **Adding new tools**: Implement `ICommandResolver` for your tool
2. **Adding patterns**: Extend the pattern list in `GitCommandResolver`
3. **AI integration**: Replace pattern matching with AI in `ICommandResolver`

## 📄 License

MIT License - see LICENSE file for details

## 🏗️ Project Structure

```
src/
├── CmdAi.Core/           # Core interfaces and models
│   ├── Interfaces/       # ICommandResolver, ICommandExecutor, etc.
│   ├── Models/          # CommandRequest, CommandResult, etc.
│   └── Services/        # Implementation classes
├── CmdAi.Cli/           # Console application
└── tests/
    └── CmdAi.Tests/     # Unit tests
```