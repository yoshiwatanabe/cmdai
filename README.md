# CmdAI

CmdAI is a documentation-grounded command-line assistant written in Python.

The design goal:

1. Capture real command help from the machine where the command runs.
2. Store that help in a portable SQLite database.
3. Retrieve relevant help snippets for a natural-language request.
4. Ask an optional LLM to generate a command using only the retrieved snippets.

This keeps model knowledge as a reasoning layer, not the source of truth.

## Quick Start

From the repository root:

```powershell
python -m cmdai doctor
python -m cmdai groom git grep pwsh powershell "git remote"
python -m cmdai ask "with git, show remote repo urls"
python -m cmdai ask "find text recursively in ts files" --tool grep --shell powershell
```

From PowerShell 7:

```powershell
.\cmdai.ps1 doctor
.\cmdai.ps1 doctor --test-llm
.\cmdai.ps1 groom-shell powershell
.\cmdai.ps1 groom Get-ChildItem "git remote" --shell powershell
.\cmdai.ps1 ask "show all files that ends with .pdf in a directory recursively using PowerShell"
.\cmdai.ps1 ask --no-ai "show all files that ends with .pdf in a directory recursively using PowerShell"
```

PowerShell requires `.\` for scripts in the current directory.

Azure CLI examples:

```powershell
.\cmdai.ps1 groom az "az account" "az group" "az resource" "az aks" "az storage account" "az keyvault" "az webapp" "az vm" --shell powershell
.\cmdai.ps1 ask "with Azure CLI, list my subscriptions"
.\cmdai.ps1 ask "show Azure resource groups"
```

Git history example:

```powershell
.\cmdai.ps1 cache show "git log"
.\cmdai.ps1 ask "show me the list of submitters for the last 5 commits"
.\cmdai.ps1 cache show "git log"
```

If `git log` is not already cached, `ask` infers it from words like commits, history, authors, or submitters and grooms it automatically.

Normal `ask` is inference-first: it retrieves groomed docs and asks Gemini. Use `--no-ai` only for cache/retrieval debugging.

Usage history and reporting:

```powershell
.\cmdai.ps1 history
.\cmdai.ps1 history --limit 5
.\cmdai.ps1 stats
.\cmdai.ps1 stats --limit 20
```

Every `ask` records the natural-language request, inferred tool, final command if available, result source, and timestamp in the same SQLite database. These records are the lexical/vector memory layer.

From regular Windows Command Prompt:

```cmd
cmdai.cmd doctor
cmdai.cmd groom Get-ChildItem "git remote" --shell powershell
cmdai.cmd ask --no-ai "show all files that ends with .pdf in a directory recursively using PowerShell"
cmdai.cmd ask --tool get-childitem --no-ai "show all .pdf files recursively"
```

For common PowerShell requests, you do not need to know the cmdlet name first. Run `.\cmdai.ps1 groom-shell powershell` to build a local command catalog from `Get-Command`, `Get-Help`, and `Get-Command -Syntax`; future asks can discover cmdlets from local metadata instead of hardcoded phrase lists.

To run the generated PowerShell command from Command Prompt:

```cmd
powershell -NoProfile -Command "Get-ChildItem -Path . -Recurse -File -Filter *.pdf"
```

## Installation & Configuration

All configuration is managed via environment variables or a local `.env` file at the repository root.

### 1. Configure Gemini API Key
Create a `.env` file by copying the template:
```cmd
copy .env.example .env
```
Inside your `.env` file, configure your Gemini key and model:
```ini
GEMINI_API_KEY=your-gemini-key
CMDAI_GEMINI_MODEL=gemini-2.5-flash
```

### 2. Configure Database Storage (Optional)
By default, the SQLite database is stored at `~/.cmdai/cmdai.sqlite3`.
To sync your database across machines via OneDrive, configure the `CMDAI_DB` path in your `.env` file:
```ini
CMDAI_DB=C:\Users\tsuyo\OneDrive\Data\cmdai_data\cmdai.sqlite3
```

*(Note: CmdAI checks the local repository `.env` file, the current folder `.env`, and your user profile directory `~/.cmdai/.env` in order of precedence)*.


## Commands

```text
ask <query>                 Resolve a natural-language request.
groom <tool...>             Capture local help output for tools.
tools                       List tools with cached documentation.
cache search <query>        Search cached docs.
cache show <tool>           Show cached docs for a tool.
catalog search <query>      Search discovered shell commands.
history                     List recent asks and generated commands.
stats                       Show usage by tool, source, and command.
doctor                      Show runtime and database information.
```

## Architecture

- `cmdai.storage` owns SQLite schema, FTS search, and portable paths.
- `cmdai.collector` captures `--help`, `-h`, `help`, and PowerShell `Get-Help`.
- `cmdai.resolver` infers tools, retrieves snippets, and formats grounded prompts.
- `cmdai.llm` contains the optional Google AI Studio Gemini API call.

