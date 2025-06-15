# CmdAI - AI-Powered CLI Assistant

Transform natural language into CLI commands using AI (Azure OpenAI + Ollama) with smart fallback.

## 🚀 Quick Start

```bash
# Download from Releases, then install:
dotnet tool install --global --add-source ./downloads CmdAi.Cli

# Use with any CLI tool:
cmdai git "delete untracked files"     → git clean -fd
cmdai az "list subscriptions"          → az account list --output table  
cmdai docker "show running containers" → docker ps
```

## ⚡ Features

- **🤖 AI-Powered**: Azure OpenAI → Ollama → Pattern fallback
- **🛡️ Safe**: Validates dangerous commands before execution
- **🔒 Private**: Local processing option available
- **🌐 Universal**: Works with any CLI tool

## 🛠️ Installation

### Prerequisites
- .NET 8.0+ 
- **Optional**: [Ollama](https://ollama.ai) for local AI (`ollama pull codellama:7b`)

### Install
1. Download `CmdAi.Cli.1.1.0.nupkg` from [Releases](https://github.com/yoshiwatanabe/cmdai/releases)
2. Install globally:
   ```bash
   dotnet tool install --global --add-source ./downloads CmdAi.Cli
   ```

### Azure OpenAI Setup (Optional)
1. Copy `.env.example` to `.env`
2. Add your API key:
   ```bash
   AI__AzureOpenAIApiKey=your_api_key_here
   AI__AzureOpenAIEndpoint=your_endpoint_url
   ```

## 📖 Usage

```bash
# Direct syntax
cmdai git "undo last commit"
cmdai az "show current subscription"  

# Ask syntax  
cmdai ask docker "stop all containers"
cmdai ask kubectl "get pods"
```

## 📄 License

MIT License