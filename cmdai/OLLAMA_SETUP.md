# 🤖 Ollama Setup Guide for CmdAI

This guide will help you set up Ollama with CodeLlama to enable AI-powered command generation in CmdAI.

## 📋 Prerequisites

- **Operating System**: Linux, macOS, or Windows (WSL2)
- **Memory**: At least 8GB RAM (16GB+ recommended for better performance)
- **Storage**: ~4GB free space for CodeLlama 7B model
- **Network**: Internet connection for initial setup

## 🚀 Quick Setup

### 1. Install Ollama

**Linux & macOS:**
```bash
curl -fsSL https://ollama.ai/install.sh | sh
```

**Windows:**
```cmd
# Download and install Ollama for Windows
# From https://ollama.ai/download
# Then:
ollama pull codellama:7b
```
- Download the installer from [ollama.ai/download](https://ollama.ai/download)
- Or use WSL2 with the Linux installation method

### 2. Download CodeLlama Model

```bash
# Download the 7B parameter model (recommended)
ollama pull codellama:7b

# Alternative: 13B model (better quality, requires more memory)
ollama pull codellama:13b

# Alternative: 34B model (best quality, requires 32GB+ RAM)
ollama pull codellama:34b
```

### 3. Start Ollama Service

```bash
# Start Ollama (runs on localhost:11434 by default)
ollama serve
```

### 4. Verify Installation

```bash
# Test that Ollama is running
curl http://localhost:11434/api/tags

# Test CodeLlama model
ollama run codellama:7b "Write a git command to check status"
```

## ⚙️ Configuration

### CmdAI Configuration

Update your `appsettings.json`:

```json
{
  "AI": {
    "EnableAI": true,
    "Provider": "ollama",
    "ModelName": "codellama:7b",
    "OllamaEndpoint": "http://localhost:11434",
    "TimeoutSeconds": 30,
    "FallbackToPatterns": true,
    "EnableLearning": true,
    "ConfidenceThreshold": 0.7
  }
}
```

### Environment Variables (Optional)

```bash
# Override default configuration
export CMDAI_AI_MODEL="codellama:13b"
export CMDAI_OLLAMA_ENDPOINT="http://localhost:11434"
export CMDAI_AI_TIMEOUT="45"
```

## 🔧 Advanced Setup

### Custom Ollama Port

If you need to run Ollama on a different port:

```bash
# Set custom port
export OLLAMA_HOST=0.0.0.0:11435

# Start Ollama
ollama serve
```

Update CmdAI configuration:
```json
{
  "AI": {
    "OllamaEndpoint": "http://localhost:11435"
  }
}
```

### Multiple Models

You can switch between different models:

```bash
# Download additional models
ollama pull codellama:13b
ollama pull mistral:7b
ollama pull llama2:7b

# List available models
ollama list
```

Update CmdAI to use different model:
```json
{
  "AI": {
    "ModelName": "mistral:7b"
  }
}
```

## 🔥 Performance Optimization

### Memory Settings

For better performance, you can adjust Ollama's memory usage:

```bash
# Set memory limit (e.g., 8GB)
export OLLAMA_MAX_LOADED_MODELS=1
export OLLAMA_MAX_VRAM=8192

# Start Ollama
ollama serve
```

### Model Selection Guide

| Model | RAM Required | Speed | Quality | Use Case |
|-------|-------------|-------|---------|----------|
| `codellama:7b` | 8GB | Fast | Good | **Recommended for most users** |
| `codellama:13b` | 16GB | Medium | Better | Power users with more RAM |
| `codellama:34b` | 32GB+ | Slow | Best | High-end workstations |

## 🐛 Troubleshooting

### Common Issues

**1. "Connection refused" error**
```bash
# Check if Ollama is running
ps aux | grep ollama

# Restart Ollama
ollama serve
```

**2. "Model not found" error**
```bash
# Verify model is downloaded
ollama list

# Re-download if needed
ollama pull codellama:7b
```

**3. Slow response times**
```bash
# Check available memory
free -h

# Consider using smaller model
ollama pull codellama:7b
```

**4. Port conflicts**
```bash
# Check what's using port 11434
lsof -i :11434

# Use different port
export OLLAMA_HOST=0.0.0.0:11435
ollama serve
```

### Enable Debug Logging

```bash
# Enable verbose logging
export OLLAMA_DEBUG=1
ollama serve
```

## 🔒 Security Considerations

### Local Network Access

By default, Ollama only accepts local connections. To allow network access:

```bash
# Allow network connections (use with caution)
export OLLAMA_HOST=0.0.0.0:11434
ollama serve
```

### Firewall Configuration

If running on a server, configure firewall:

```bash
# Ubuntu/Debian
sudo ufw allow 11434

# CentOS/RHEL
sudo firewall-cmd --add-port=11434/tcp --permanent
sudo firewall-cmd --reload
```

## 📊 Monitoring

### Check Ollama Status

```bash
# API health check
curl http://localhost:11434/api/tags

# Model information
curl http://localhost:11434/api/show -d '{
  "name": "codellama:7b"
}'
```

### Resource Usage

```bash
# Monitor CPU/memory usage
htop

# Monitor GPU usage (if applicable)
nvidia-smi
```

## 🆘 Getting Help

- **Ollama Documentation**: [https://github.com/jmorganca/ollama](https://github.com/jmorganca/ollama)
- **Model Library**: [https://ollama.ai/library](https://ollama.ai/library)
- **CmdAI Issues**: [GitHub Issues](https://github.com/yoshiwatanabe/cmdai/issues)

## 🎯 Testing Your Setup

Once everything is configured, test CmdAI with AI features:

```bash
# Test with a tool that doesn't have patterns
cmdai ask docker "show running containers"

# Test learning (accept the command to train the AI)
cmdai ask kubectl "get all pods"

# Test fallback (stop Ollama temporarily)
cmdai ask git "status"  # Should fall back to patterns
```

**Success indicators:**
- ✅ Commands suggest without "(Pattern-based)" in context
- ✅ New tools work that weren't supported before
- ✅ Learning data accumulates in `cmdai_learning.json`
- ✅ Fallback works when Ollama is unavailable

Happy AI-powered command line assistance! 🚀