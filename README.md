# Dalamud Plugin Template (MVU Architecture)

A template for Final Fantasy XIV Dalamud plugins using Model-View-Update (MVU) pattern with strict separation of concerns.

## Features

- **4-Project Architecture** - Clean separation between core logic, Dalamud integration, tests, and UI sandbox
- **MVU Pattern** - Predictable state management with immutable models
- **Module Organization** - Related functionality grouped together
- **Dependency Injection** - Using Microsoft.Extensions.DependencyInjection
- **Testable** - Business logic completely independent of Dalamud
- **UI Sandbox** - Develop and test UI without launching the game

## Quick Start

1. **Use this template** (click the button on GitHub)
2. **Clone your new repository**
3. **Find & Replace** "SamplePlugin" with your plugin name
4. **Update metadata** in `SamplePlugin/SamplePlugin.json`
5. **Start coding** in the Core project!

## Project Structure

```
SamplePlugin/           # Dalamud plugin host (composition root)
SamplePlugin.Core/      # Business logic (no Dalamud dependencies)
SamplePlugin.Tests/     # Unit tests
SamplePlugin.UISandbox/ # Standalone UI development
```

## Build & Run

```bash
# Build all projects
dotnet build

# Run tests
dotnet test

# Run UI Sandbox (test UI without game)
dotnet run --project SamplePlugin.UISandbox
```

## Architecture Guidelines

1. **All business logic** goes in Core project
2. **No Dalamud references** in Core
3. **Plugin project** is only for wiring/composition
4. **Group related code** in module folders
5. **Test everything** in the Tests project
