# Contributing to LockIt

Thank you for your interest in contributing! This guide will help you get started.

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- Git

## Build & Test

git clone https://github.com/Noctua-Lumen-Technologies/LockIt.git cd LockIt dotnet restore dotnet build dotnet test

## Pull Request Process

1. Fork the repository and create a feature branch from `main`.
2. Ensure all existing tests pass and add tests for any new functionality.
3. Follow the code style enforced by `.editorconfig`.
4. Update `CHANGELOG.md` under an `[Unreleased]` section.
5. Open a PR with a clear description of the change and its motivation.

## Code Style

- File-scoped namespaces.
- Private fields prefixed with `_` (e.g. `_logger`).
- `nullable enable` and `implicit usings` in all projects.
- XML documentation comments on every public API.
- NUnit for unit tests.

## Reporting Issues

Use [GitHub Issues](https://github.com/Noctua-Lumen-Technologies/LockIt/issues) to report bugs or request features.

## License

By contributing you agree that your contributions will be licensed under the [Apache License 2.0](LICENSE).
