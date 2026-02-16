# CmdAI Quick Setup Guide

This setup configures CmdAI with OpenAI as the default provider and API failover across Azure OpenAI, Anthropic, and Gemini.

## 1) Install CmdAI

```bash
dotnet tool install --global --add-source . CmdAi.Cli
```

## WSL Notes

If you want CmdAI to run inside WSL and execute commands in WSL, install it inside WSL (Linux) rather than using the Windows `cmdai.exe`.

1) Build the package on Windows:

```powershell
dotnet pack .\src\CmdAi.Cli\CmdAi.Cli.csproj -c Release
```

2) Install inside WSL:

```bash
dotnet tool install --global --add-source /mnt/c/Users/tsuyo/Repos/cmdai/src/CmdAi.Cli/nupkg CmdAi.Cli
export PATH="$PATH:$HOME/.dotnet/tools"
cmdai diagnostics
```

## 2) Create config

Copy `.env.example` to `~/.env` and set your keys.

Minimum required:

```bash
AI__OpenAI__ApiKeys__0=your_openai_api_key
```

Recommended full fallback chain:

```bash
AI__OpenAI__ApiKeys__0=...
AI__AzureOpenAI__ApiKeys__0=...
AI__Anthropic__ApiKeys__0=...
AI__Gemini__ApiKeys__0=...
```

Provider-specific keys are preferred over legacy Azure compatibility keys:
- Preferred: `AI__AzureOpenAI__ApiKeys__0`, `AI__AzureOpenAI__Endpoint`
- Legacy (still supported): `AI__AzureOpenAIApiKey`, `AI__AzureOpenAIEndpoint`

Gemini endpoint should be:

```bash
AI__Gemini__Endpoint=https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent
```

## 3) Provider order

Default order is already set:

```bash
AI__Providers__0=openai
AI__Providers__1=azureopenai
AI__Providers__2=anthropic
AI__Providers__3=gemini
```

## 4) Verify

```bash
cmdai diagnostics
cmdai git "show current status"
```

## Behavior

- Transient failures (`timeout`, `network`, `429`, `5xx`) trigger provider failover.
- Non-transient request/config errors do not continue provider-chain failover.
- If all providers fail, CmdAI falls back to pattern resolvers when enabled.
