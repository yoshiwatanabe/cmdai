# CmdAI Architecture

CmdAI is a .NET CLI that translates natural language into CLI commands with API-provider failover and pattern fallback.

## High-Level Flow

```mermaid
graph TB
    U[User] --> CLI[CLI Command]
    CLI --> CP[Context Provider]
    CP --> MPR[MultiProviderAICommandResolver]

    MPR --> OAI[OpenAI Provider]
    MPR --> AZ[Azure OpenAI Provider]
    MPR --> AN[Anthropic Provider]
    MPR --> GM[Gemini Provider]

    MPR --> VAL[Command Validator]
    MPR --> PAT[Pattern Resolver]

    PAT --> GIT[Git Patterns]
    PAT --> AZPAT[Azure Patterns]

    VAL --> EXE[Command Executor]
    EXE --> LEARN[File Learning Service]
```

## Provider Strategy

Default provider order:
1. `openai`
2. `azureopenai`
3. `anthropic`
4. `gemini`

Failover policy:
- Continue to next provider on transient failures only: `timeout`, `network`, `429`, `5xx`.
- Stop provider-chain failover on permanent errors (invalid request/config/auth not recoverable by retry strategy).
- If all providers fail and `FallbackToPatterns=true`, use pattern resolver.

## Core Components

### Resolver Layer
- `MultiProviderAICommandResolver`
- `PatternCommandResolver`
- `GitCommandResolver`
- `AzureCommandResolver`

### AI Provider Layer
- `OpenAIProvider`
- `AzureOpenAIProvider`
- `AnthropicProvider`
- `GeminiProvider`

### Safety + Execution
- `CommandValidator`
- `CommandExecutor`

### Context + Learning
- `ContextProvider`
- `FileLearningService`

## Configuration Model

Configuration is centered on `AIConfiguration` with provider-specific settings:
- `AI.Providers[]` ordered provider IDs
- `AI.OpenAI`, `AI.AzureOpenAI`, `AI.Anthropic`, `AI.Gemini`
- Per-provider `Enabled`, `Endpoint`, `Model`, `ApiKeys[]`, optional timeout override

Compatibility behavior:
- Legacy provider value `ollama` is ignored and surfaced as a warning.
- Legacy Azure fields map into `AI.AzureOpenAI` when needed.
- Optional single-key compatibility fields can seed provider key arrays.

## Diagnostics

`cmdai diagnostics` reports:
- Effective provider order
- Provider enablement and config status
- Provider availability checks
- Last resolution failover trace (provider-by-provider attempt outcomes)

## Dependency Injection

Runtime wiring in `Program.ConfigureServices()`:
- Registers provider `HttpClient`s and `IAIProvider` implementations
- Registers `ICommandResolver` as `MultiProviderAICommandResolver`
- Registers `IResolutionDiagnostics` from the same resolver instance

## Sequence: Request Resolution

```mermaid
sequenceDiagram
    participant U as User
    participant CLI as Program
    participant R as MultiProvider Resolver
    participant P as Providers
    participant V as Validator
    participant E as Executor

    U->>CLI: cmdai git "show status"
    CLI->>R: ResolveCommandAsync(request, context)
    R->>P: Try providers in configured order
    alt Transient provider failure
        P-->>R: timeout/429/5xx/network
        R->>P: Try next provider
    end
    P-->>R: command
    R->>V: ValidateCommandAsync
    V-->>R: valid/unsafe metadata
    R-->>CLI: CommandResult
    CLI->>E: confirm + execute
```

## Extension Points

- Add a provider by implementing `IAIProvider` and registering in DI.
- Add tool fallback behavior by extending pattern resolvers.
- Add richer policy by extending provider failure classification and resolver decision rules.

## References

- `../README.md`
- `SETUP.md`
- `CONTRIBUTING.md`
