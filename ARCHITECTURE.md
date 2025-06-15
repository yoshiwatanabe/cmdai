# 🏗️ CmdAI Architecture & Design

This document provides visual representations and detailed explanations of CmdAI's architecture, component interactions, and design decisions.

## 📊 High-Level Architecture

CmdAI follows an **AI-First Pipeline** architecture with intelligent fallback mechanisms:

```mermaid
graph TB
    User[👤 User Input] --> CLI[🖥️ CLI Interface]
    CLI --> Parser[📝 Command Parser]
    Parser --> Context[🔍 Context Provider]
    Context --> Resolver{🤖 Command Resolver}
    
    Resolver --> AI[🤖 Multi-Provider AI Resolver]
    AI --> Azure[🌐 Azure OpenAI]
    AI --> Ollama[🦙 Ollama/CodeLlama]
    AI --> Validator[🛡️ Command Validator]
    
    Resolver --> Fallback[📋 Pattern Resolver]
    Fallback --> Git[📚 Git Patterns]
    Fallback --> Azure[☁️ Azure CLI Patterns]
    
    Validator --> Safe{✅ Safe Command?}
    Safe -->|Yes| Executor[⚡ Command Executor]
    Safe -->|No| Warning[⚠️ Safety Warning]
    Warning --> Confirm{🤔 User Confirms?}
    Confirm -->|Yes| Executor
    Confirm -->|No| Cancel[❌ Cancel]
    
    Executor --> Execute[🚀 Execute Command]
    Execute --> Learning[📚 Learning Service]
    Learning --> Feedback[💾 Store Feedback]
    
    style User fill:#e1f5fe
    style AI fill:#f3e5f5
    style Ollama fill:#fff3e0
    style Validator fill:#e8f5e8
    style Learning fill:#fce4ec
```

## 🔄 Component Interaction Flow

### Multi-Provider AI Resolution Path

```mermaid
sequenceDiagram
    participant U as User
    participant CLI as CLI Interface
    participant MPR as Multi-Provider Resolver
    participant AZ as Azure OpenAI
    participant OL as Ollama AI
    participant CV as Command Validator
    participant CE as Command Executor
    participant LS as Learning Service
    
    U->>CLI: cmdai git "delete untracked files"
    CLI->>MPR: Process Request
    MPR->>AZ: Try Azure OpenAI (Priority 1)
    AZ-->>MPR: "git clean -fd"
    alt If Azure OpenAI fails
        MPR->>OL: Try Ollama (Priority 2)
        OL-->>MPR: "git clean -fd"
    end
    MPR->>CV: Validate Command
    CV-->>MPR: ⚠️ Potentially unsafe
    MPR->>CE: Execute with Warning
    CE->>U: Show: "git clean -fd" ⚠️ RISKY - Execute? (y/N)
    U->>CE: y
    CE->>CE: Run Command
    CE->>LS: Record Success
    LS-->>LS: Update Learning Data
```

### Fallback Pattern Resolution Path

```mermaid
sequenceDiagram
    participant U as User
    participant CLI as CLI Interface
    participant AIR as AI Resolver
    participant OAI as Ollama AI
    participant PR as Pattern Resolver
    participant CE as Command Executor
    
    U->>CLI: cmdai git "check status"
    CLI->>AIR: Process Request
    AIR->>OAI: Generate Command
    OAI-->>AIR: ❌ AI Unavailable
    AIR->>PR: Fallback to Patterns
    PR-->>AIR: "git status" (Pattern Match)
    AIR->>CE: Execute Command
    CE->>U: Suggested: git status
```

## 🧩 Core Components

### 1. Command Resolution Layer

```mermaid
graph LR
    Input[Natural Language Input] --> AICommandResolver
    AICommandResolver --> AIProvider[🦙 Ollama AI Provider]
    AIProvider --> Context[📋 Context Builder]
    
    AICommandResolver --> Fallback{AI Available?}
    Fallback -->|No| PatternResolver
    PatternResolver --> GitPatterns[Git Patterns]
    PatternResolver --> AzurePatterns[Azure Patterns]
    
    AICommandResolver --> Output[Generated Command]
    PatternResolver --> Output
    
    style AICommandResolver fill:#e3f2fd
    style AIProvider fill:#f3e5f5
    style PatternResolver fill:#fff3e0
```

### 2. Safety & Validation System

```mermaid
graph TB
    Command[Generated Command] --> Validator[🛡️ Command Validator]
    Validator --> DangerousCheck{Contains Dangerous Patterns?}
    
    DangerousCheck -->|Yes| HighRisk[🔴 High Risk]
    DangerousCheck -->|No| BasicCheck{Basic Validation}
    
    BasicCheck -->|Valid| SafeCommand[✅ Safe to Execute]
    BasicCheck -->|Invalid| InvalidCommand[❌ Invalid Command]
    
    HighRisk --> Warnings[⚠️ Generate Warnings]
    Warnings --> UserConfirm[🤔 Require User Confirmation]
    
    SafeCommand --> Execute[Execute with Standard Confirmation]
    UserConfirm --> Execute
    InvalidCommand --> Reject[Reject Command]
    
    style HighRisk fill:#ffebee
    style SafeCommand fill:#e8f5e8
    style Warnings fill:#fff3e0
```

### 3. Learning & Feedback Loop

```mermaid
graph LR
    Execution[Command Execution] --> Success{Successful?}
    Success -->|Yes| PositiveFeedback[✅ Positive Feedback]
    Success -->|No| NegativeFeedback[❌ Negative Feedback]
    
    UserAcceptance[User Accepted?] --> AcceptedFeedback[👍 Accepted]
    UserAcceptance --> RejectedFeedback[👎 Rejected]
    
    PositiveFeedback --> LearningData[📊 Learning Database]
    NegativeFeedback --> LearningData
    AcceptedFeedback --> LearningData
    RejectedFeedback --> LearningData
    
    LearningData --> FutureRequests[🔮 Improve Future Requests]
    FutureRequests --> ContextualExamples[📚 Contextual Examples]
    ContextualExamples --> BetterCommands[🎯 Better Command Generation]
    
    style LearningData fill:#f3e5f5
    style BetterCommands fill:#e8f5e8
```

## 🎯 Design Patterns & Principles

### 1. Strategy Pattern - Command Resolvers

```mermaid
classDiagram
    class ICommandResolver {
        <<interface>>
        +CanResolve(tool: string) bool
        +ResolveCommandAsync(request, context) CommandResult
    }
    
    class MultiProviderAICommandResolver {
        +CanResolve(tool: string) bool
        +ResolveCommandAsync(request, context) CommandResult
        -TryAIResolutionWithPriority() CommandResult
        -GetOrderedProviders() IAIProvider[]
        -BuildAIContext() string
    }
    
    class PatternCommandResolver {
        +CanResolve(tool: string) bool
        +ResolveCommandAsync(request, context) CommandResult
        -MatchPatterns() CommandResult
    }
    
    class GitCommandResolver {
        +CanResolve(tool: string) bool
        +ResolveCommandAsync(request, context) CommandResult
        -GitSpecificPatterns() CommandResult
    }
    
    ICommandResolver <|-- MultiProviderAICommandResolver
    ICommandResolver <|-- PatternCommandResolver
    ICommandResolver <|-- GitCommandResolver
    
    MultiProviderAICommandResolver --> IAIProvider
    MultiProviderAICommandResolver --> ICommandValidator
    MultiProviderAICommandResolver --> ILearningService
```

### 2. Dependency Injection Architecture

```mermaid
graph TB
    DI[Dependency Injection Container] --> Services[Core Services]
    
    Services --> IContextProvider
    Services --> ICommandExecutor
    Services --> ICommandValidator
    Services --> ILearningService
    Services --> IAIProvider
    
    IContextProvider --> ContextProvider
    ICommandExecutor --> CommandExecutor
    ICommandValidator --> CommandValidator
    ILearningService --> FileLearningService
    IAIProvider --> OllamaAIProvider
    
    Services --> Resolvers[Command Resolvers]
    Resolvers --> AICommandResolver
    Resolvers --> PatternCommandResolver
    Resolvers --> GitCommandResolver
    Resolvers --> AzureCommandResolver
    
    style DI fill:#e1f5fe
    style Services fill:#f3e5f5
    style Resolvers fill:#fff3e0
```

## 🚀 User Experience Flow

### Complete User Journey

```mermaid
journey
    title CmdAI User Experience Journey
    section Installation
      Install .NET Tool: 5: User
      Verify Installation: 4: User
      
    section First Use
      Try Basic Command: 3: User
      See Pattern Fallback: 4: User
      Learn About AI Features: 5: User
      
    section AI Setup
      Configure Azure OpenAI: 5: User
      Install Ollama (Optional): 3: User
      Download CodeLlama: 4: User
      Test with Diagnostics: 5: User
      
    section Daily Usage
      Natural Language Query: 5: User
      AI Generates Command: 5: AI
      Safety Validation: 5: System
      User Confirms: 4: User
      Command Executes: 5: System
      Learning Updates: 5: System
      
    section Advanced Usage
      Complex Queries: 5: User
      Multi-tool Support: 5: AI
      Custom Configurations: 4: User
      Contribution: 5: Community
```

## 🔧 Configuration & Extensibility

### Configuration Flow

```mermaid
graph LR
    AppSettings[appsettings.json] --> Config[AIConfiguration]
    EnvVars[Environment Variables] --> Config
    CommandLine[Command Line Args] --> Config
    
    Config --> AIProvider[AI Provider Setup]
    Config --> Learning[Learning Service Setup]
    Config --> Safety[Safety Validation Setup]
    
    AIProvider --> Azure[Azure OpenAI Configuration]
    AIProvider --> Ollama[Ollama Configuration]
    AIProvider --> Future[Future AI Providers]
    
    Learning --> Feedback[Feedback Collection]
    Learning --> Storage[Data Storage]
    
    Safety --> Validation[Command Validation]
    Safety --> Warnings[Warning System]
    
    style Config fill:#e3f2fd
    style Future fill:#f3e5f5
```

### Extension Points

```mermaid
graph TB
    Core[CmdAI Core] --> Extensions[Extension Points]
    
    Extensions --> NewResolvers[New Command Resolvers]
    Extensions --> NewProviders[New AI Providers]
    Extensions --> NewValidators[New Validators]
    Extensions --> NewLearning[New Learning Algorithms]
    
    NewResolvers --> Terraform[Terraform Resolver]
    NewResolvers --> Kubernetes[Kubernetes Resolver]
    NewResolvers --> Custom[Custom Tool Resolver]
    
    NewProviders --> OpenAI[OpenAI Provider]
    NewProviders --> Anthropic[Anthropic Provider]
    NewProviders --> LocalLLM[Local LLM Provider]
    
    style Extensions fill:#e8f5e8
    style Core fill:#e1f5fe
```

## 📈 Performance & Scalability

### Response Time Optimization

```mermaid
graph LR
    Request[User Request] --> Cache{Cache Hit?}
    Cache -->|Yes| FastResponse[⚡ Cached Response <100ms]
    Cache -->|No| AIProcess[🧠 AI Processing]
    
    AIProcess --> Parallel[Parallel Processing]
    Parallel --> AIGeneration[AI Generation ~2-5s]
    Parallel --> PatternCheck[Pattern Fallback ~50ms]
    Parallel --> ContextBuild[Context Building ~10ms]
    
    AIGeneration --> Response[Final Response]
    PatternCheck --> Response
    ContextBuild --> Response
    
    Response --> UpdateCache[Update Cache]
    
    style FastResponse fill:#e8f5e8
    style AIGeneration fill:#fff3e0
    style PatternCheck fill:#e3f2fd
```

## 🛡️ Security Architecture

### Security Layers

```mermaid
graph TB
    Input[User Input] --> Sanitization[Input Sanitization]
    Sanitization --> CommandGen[Command Generation]
    CommandGen --> Validation[Multi-layer Validation]
    
    Validation --> PatternCheck[Dangerous Pattern Detection]
    Validation --> ContextCheck[Context Validation]
    Validation --> PermissionCheck[Permission Analysis]
    
    PatternCheck --> Risk{Risk Level}
    Risk -->|Low| Proceed[✅ Proceed]
    Risk -->|Medium| Warn[⚠️ Warning + Confirmation]
    Risk -->|High| Block[🛑 Block + Manual Review]
    
    Proceed --> Execute[Execute Command]
    Warn --> UserConfirm{User Confirms?}
    UserConfirm -->|Yes| Execute
    UserConfirm -->|No| Cancel[Cancel]
    Block --> Cancel
    
    Execute --> Audit[Audit Log]
    
    style Validation fill:#f3e5f5
    style Block fill:#ffebee
    style Execute fill:#e8f5e8
```

---

## 📚 Further Reading

- **[Implementation Guide](CONTRIBUTING.md)**: How to extend and contribute to CmdAI
- **[API Documentation](cmdai/README.md)**: Detailed usage examples and API reference
- **[Setup Guide](cmdai/OLLAMA_SETUP.md)**: Complete AI setup instructions

This architecture is designed for **extensibility**, **safety**, and **user experience** while maintaining **high performance** and **privacy**.