# cmdai-next

`cmdai-next` is a Python prototype for a documentation-grounded rewrite of CmdAI.

The design goal is different from the current .NET CLI:

1. Capture real command help from the machine where the command runs.
2. Store that help in a portable SQLite database.
3. Retrieve relevant help snippets for a natural-language request.
4. Ask an optional LLM to generate a command using only the retrieved snippets.

This keeps model knowledge as a reasoning layer, not the source of truth.

## Quick Start

From the repository root:

```powershell
cd .\cmdai_next
python -m cmdai_next doctor
python -m cmdai_next groom git grep pwsh powershell "git remote"
python -m cmdai_next ask "with git, show remote repo urls"
python -m cmdai_next ask "find text recursively in ts files" --tool grep --shell powershell
```

From PowerShell 7:

```powershell
cd C:\Users\tsuyo\Repos\cmdai\cmdai_next
.\cmdai-next.ps1 doctor
.\cmdai-next.ps1 doctor --test-llm
.\cmdai-next.ps1 groom-shell powershell
.\cmdai-next.ps1 groom Get-ChildItem "git remote" --shell powershell
.\cmdai-next.ps1 ask "show all files that ends with .pdf in a directory recursively using PowerShell"
.\cmdai-next.ps1 ask --no-ai "show all files that ends with .pdf in a directory recursively using PowerShell"
```

PowerShell requires `.\` for scripts in the current directory.

Azure CLI examples:

```powershell
.\cmdai-next.ps1 groom az "az account" "az group" "az resource" "az aks" "az storage account" "az keyvault" "az webapp" "az vm" --shell powershell
.\cmdai-next.ps1 ask "with Azure CLI, list my subscriptions"
.\cmdai-next.ps1 ask "show Azure resource groups"
```

Git history example:

```powershell
.\cmdai-next.ps1 cache show "git log"
.\cmdai-next.ps1 ask "show me the list of submitters for the last 5 commits"
.\cmdai-next.ps1 cache show "git log"
```

If `git log` is not already cached, `ask` infers it from words like commits, history, authors, or submitters and grooms it automatically.

Normal `ask` is inference-first: it retrieves groomed docs, asks Gemini, and uses deterministic fallback only if Gemini is unavailable or returns `Command: unknown`. Use `--no-ai` only for cache/retrieval debugging.

Usage history and reporting:

```powershell
.\cmdai-next.ps1 history
.\cmdai-next.ps1 history --limit 5
.\cmdai-next.ps1 stats
.\cmdai-next.ps1 stats --limit 20
```

Every `ask` records the natural-language request, inferred tool, final command if available, result source, and timestamp in the same SQLite database. These records are intended to become the future lexical/vector memory layer.

From regular Windows Command Prompt:

```cmd
cd C:\Users\tsuyo\Repos\cmdai\cmdai_next
cmdai-next.cmd doctor
cmdai-next.cmd groom Get-ChildItem "git remote" --shell powershell
cmdai-next.cmd ask --no-ai "show all files that ends with .pdf in a directory recursively using PowerShell"
cmdai-next.cmd ask --tool get-childitem --no-ai "show all .pdf files recursively"
```

For common PowerShell requests, you do not need to know the cmdlet name first. Run `.\cmdai-next.ps1 groom-shell powershell` to build a local command catalog from `Get-Command`, `Get-Help`, and `Get-Command -Syntax`; future asks can discover cmdlets from local metadata instead of hardcoded phrase lists.

To run the generated PowerShell command from Command Prompt:

```cmd
powershell -NoProfile -Command "Get-ChildItem -Path . -Recurse -File -Filter *.pdf"
```

By default the SQLite database is stored at:

```text
~\.cmdai\cmdai_next.sqlite3
```

To keep it in OneDrive or another shared folder:

```powershell
$env:CMDAI_DB = "$env:OneDrive\cmdai\cmdai_next.sqlite3"
python -m cmdai_next groom git grep pwsh powershell
```

## Optional LLM

Without an LLM, `ask` shows the most relevant cached help snippets. With a Google AI Studio Gemini API key, it also produces a candidate command grounded in the snippets.

Create `cmdai_next\.env` from `.env.example`:

```cmd
copy .env.example .env
notepad .env
```

Set:

```text
GEMINI_API_KEY=your-key
CMDAI_GEMINI_MODEL=gemini-2.5-flash
```

Then run:

```powershell
python -m cmdai_next ask "with git, show remote repo urls"
```

The loader checks `cmdai_next\.env`, the current directory `.env`, and `~\.cmdai\.env`. Real `.env` files are ignored by git; `.env.example` is tracked.

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

- `cmdai_next.storage` owns SQLite schema, FTS search, and portable paths.
- `cmdai_next.collector` captures `--help`, `-h`, `help`, and PowerShell `Get-Help`.
- `cmdai_next.resolver` infers tools, retrieves snippets, and formats grounded prompts.
- `cmdai_next.llm` contains the optional Google AI Studio Gemini API call.

This folder is intentionally separate from the existing `src/` .NET implementation.
