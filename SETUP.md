# CmdAI Quick Setup Guide

This setup configures CmdAI with OpenAI as the default provider and API failover across Azure OpenAI, Anthropic, and Gemini.

## 1) Install CmdAI

```bash
dotnet tool install --global --add-source . CmdAi.Cli
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
