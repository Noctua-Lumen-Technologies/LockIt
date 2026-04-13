# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [2.0.0] - 2026-04-10

### Added

- `AsyncKeyedLocker<TKey>` — async per-key locking with automatic idle cleanup.
- `IAsyncKeyedLocker<TKey>` — interface for mocking and DI decoupling.
- `AsyncKeyedLockerFactory` / `IAsyncKeyedLockerFactory` — factory pattern for creating locker instances.
- `TryAcquireResult` — non-throwing try-pattern for lock acquisition with timeout.
- `AsyncKeyedLockerOptions` — configurable cleanup intervals, long-held thresholds, and drain timeouts.
- `LockItMetrics` — built-in `System.Diagnostics.Metrics` instrumentation (OpenTelemetry-compatible).
- `ServiceCollectionExtensions.AddLockIt()` — one-line DI registration.
- Long-held lock detection with configurable warning logging.
- Graceful async disposal with optional drain timeout.
- `TimeProvider` support for deterministic unit testing.

## [1.0.0] - Initial release

### Added

- Initial implementation.
