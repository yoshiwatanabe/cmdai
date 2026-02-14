# CmdAI Installation Guide (Windows + WSL)

This guide starts from cloning the repo and ends with `cmdai` available globally on:
- Windows (PowerShell)
- WSL (Linux shell)

CmdAI is packaged and installed as a .NET global tool. Windows and WSL installs are independent (separate tool directories and separate home directories).

## 0) Prerequisites

### Windows
- .NET SDK 8+ (this repo targets `net8.0`)

Optional (recommended):
- PowerShell 7 (`pwsh`) for best Windows execution behavior

### WSL
- A working WSL distro (Ubuntu/etc.)
- .NET SDK 8+ installed inside WSL

Notes:
- Installing .NET on Windows does not install .NET inside WSL.
- CmdAI executes commands in the environment it runs in.

## 1) Clone the repo (Windows)

Open PowerShell:

```powershell
git clone <your-repo-url> cmdai
cd cmdai
```

## 2) Build the tool package (Windows)

This produces a local `.nupkg` under `src/CmdAi.Cli/nupkg`.

```powershell
dotnet restore
dotnet test
dotnet pack .\src\CmdAi.Cli\CmdAi.Cli.csproj -c Release
```

## 3) Install CmdAI on Windows (global tool)

```powershell
dotnet tool install --global --add-source .\src\CmdAi.Cli\nupkg CmdAi.Cli
```

If you already installed it before:

```powershell
dotnet tool update --global --add-source .\src\CmdAi.Cli\nupkg CmdAi.Cli
```

### Windows PATH

The .NET global tools directory should be on PATH:
- `%USERPROFILE%\.dotnet\tools`

Verify:

```powershell
Get-Command cmdai
cmdai version
cmdai diagnostics
```

## 4) Configure providers (Windows)

CmdAI loads `.env` from:
1. Your home directory: `C:\Users\<you>\.env`
2. Current directory: `.env`

Create `C:\Users\<you>\.env` based on `.env.example`, set at least OpenAI:

```text
AI__OpenAI__ApiKeys__0=your_openai_api_key
Memory__StorePath=C:\Users\<you>\OneDrive\cmdai-memory
```

Verify:

```powershell
cmdai diagnostics
cmdai git "show status"
```

## 5) Install CmdAI inside WSL (global tool)

You want a Linux `cmdai` in WSL so it executes commands in WSL (`/bin/bash`).

### Option A (recommended): install from the Windows-built package

In WSL, install from the Windows-mounted nupkg folder:

```bash
dotnet tool install --global --add-source /mnt/c/Users/tsuyo/Repos/cmdai/src/CmdAi.Cli/nupkg CmdAi.Cli
export PATH="$PATH:$HOME/.dotnet/tools"
cmdai version
```

If you already installed it before:

```bash
dotnet tool update --global --add-source /mnt/c/Users/tsuyo/Repos/cmdai/src/CmdAi.Cli/nupkg CmdAi.Cli
```

Adjust the `/mnt/c/...` path to match where you cloned the repo on your machine.

### Option B: build inside WSL and install from WSL build output

This avoids cross-filesystem paths.

```bash
cd /mnt/c/Users/tsuyo/Repos/cmdai
dotnet restore
dotnet test
dotnet pack ./src/CmdAi.Cli/CmdAi.Cli.csproj -c Release
dotnet tool install --global --add-source ./src/CmdAi.Cli/nupkg CmdAi.Cli
export PATH="$PATH:$HOME/.dotnet/tools"
cmdai version
```

## 6) Configure providers (WSL)

WSL has its own home directory, so its config is separate by default:
- `~/.env`

Create `~/.env` (copy from `.env.example`) and set keys:

```bash
cat > ~/.env << 'EOF'
AI__Providers__0=openai
AI__Providers__1=azureopenai
AI__Providers__2=anthropic
AI__Providers__3=gemini
AI__OpenAI__ApiKeys__0=your_openai_api_key
Memory__StorePath=/mnt/c/Users/<you>/OneDrive/cmdai-memory
EOF
```

Verify:

```bash
cmdai diagnostics
cmdai git "show status"
```

### Sharing config between Windows and WSL (optional)

If you want one `.env` shared across both environments, you can store it in Windows and reference it from WSL. The simplest approach is to duplicate keys, but you can also symlink depending on your org policies.

## 7) Update / Uninstall

Windows:

```powershell
dotnet tool update --global --add-source .\src\CmdAi.Cli\nupkg CmdAi.Cli
dotnet tool uninstall --global CmdAi.Cli
```

WSL:

```bash
dotnet tool update --global --add-source /mnt/c/Users/tsuyo/Repos/cmdai/src/CmdAi.Cli/nupkg CmdAi.Cli
dotnet tool uninstall --global CmdAi.Cli
```

## Common gotchas

- If `cmdai` is found on Windows but not in WSL: install it inside WSL and ensure `$HOME/.dotnet/tools` is on `PATH`.
- If `cmdai` works but doesn’t use your keys: check where `.env` is being loaded from in `cmdai diagnostics`.
- If provider failover doesn’t happen: failover only continues on transient failures (`timeout`, `network`, `429`, `5xx`).
