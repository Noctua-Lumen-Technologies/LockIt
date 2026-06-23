
# 🔒 LockIt

[![NuGet](https://img.shields.io/nuget/v/NLTechnologies.LockIt.svg)](https://www.nuget.org/packages/NLTechnologies.LockIt)
[![License](https://img.shields.io/badge/license-Apache--2.0-blue.svg)](LICENSE)
[![CI](https://github.com/Noctua-Lumen-Technologies/LockIt/actions/workflows/ci.yml/badge.svg)](https://github.com/Noctua-Lumen-Technologies/LockIt/actions)

**Lightweight, async, per-key locking for .NET.** Serialize concurrent operations on the same key while allowing full parallelism across different keys. Supports configurable concurrency limits per key.

## Features

| Feature                      | Description                                                                                                          |
|------------------------------|----------------------------------------------------------------------------------------------------------------------|
| **Per-key locking**          | Operations on different keys run in parallel; same-key operations are serialized (or limited by concurrency setting) |
| **Configurable concurrency** | Control how many operations can hold a lock for the same key simultaneously (default: 1)                             |
| **Async-first**              | Fully `async`/`await`-based with no thread blocking                                                                  |
| **Timeout & cancellation**   | Optional `TimeSpan` timeout and `CancellationToken` on every acquisition                                             |
| **Try-pattern**              | `TryAcquireAsync` returns a result struct instead of throwing on timeout                                             |
| **Automatic idle cleanup**   | Stale locks are removed on a configurable timer to prevent unbounded growth                                          |
| **Long-held lock detection** | Warnings are logged when a lock exceeds a configurable threshold                                                     |
| **Built-in metrics**         | `System.Diagnostics.Metrics` instrumentation compatible with OpenTelemetry                                           |
| **Graceful disposal**        | `DisposeAsync` drains in-flight locks with an optional timeout                                                       |
| **Dependency injection**     | One-line registration via `AddLockIt()`                                                                              |
| **Testable**                 | `IAsyncKeyedLocker<TKey>` for mocking; `TimeProvider` for deterministic tests                                        |

## Installation

```bash
dotnet add package NLTechnologies.LockIt
```

## Quick Start

### Manual Instantiation (Simple Scenarios)

For simple use cases, you can instantiate `AsyncKeyedLocker<TKey>` directly. **All configuration is optional** – sensible defaults are applied automatically.

```csharp
using NLTechnologies.LockIt;
using Microsoft.Extensions.Logging;

using var loggerFactory = LoggerFactory.Create(b => b.AddConsole());
var logger = loggerFactory.CreateLogger<AsyncKeyedLocker<string>>();

// Use defaults - exclusive locking (1 operation per key)
await using var locker = new AsyncKeyedLocker<string>(logger);

await using (await locker.AcquireAsync("order-123")) 
{ 
    // Only one task can execute this block for "order-123" at a time. 
    // Other keys like "order-456" are NOT blocked. 
    await ProcessOrderAsync("order-123"); 
}
```

### With Dependency Injection (Recommended for Applications)

For applications using DI, register LockIt services once and use the factory to create locker instances:

**Step 1: Register services**
```csharp
// Program.cs
builder.Services.AddLockIt();
```

**Step 2: Inject and create locker instances**
```csharp
public class OrderService 
{ 
    private readonly IAsyncKeyedLocker<string> _locker;

    public OrderService(IAsyncKeyedLockerFactory lockerFactory)
    {
        // Create with defaults
        _locker = lockerFactory.Create<string>();
        
        // Or create with custom options
        // _locker = lockerFactory.Create<string>(new AsyncKeyedLockerOptions 
        // { 
        //     MaxConcurrentLocksPerKey = 3 
        // });
    }

    public async Task HandleAsync(string orderId, CancellationToken ct)
    {
        await using (await _locker.AcquireAsync(orderId, cancellationToken: ct))
        {
            await ProcessOrderAsync(orderId);
        }
    }
}
```

### Try-Pattern (Non-Throwing Timeout)

```csharp
await using var result = await locker.TryAcquireAsync("key", TimeSpan.FromSeconds(5));

if (result.Acquired) 
{ 
    // critical section 
} 
else 
{ 
    // lock was not acquired within the timeout 
}
```

## Configuration

### Customizing Behavior with Options

You can customize behavior by passing `AsyncKeyedLockerOptions` during instantiation. **If you don't provide options, defaults are used.**

#### Manual Instantiation with Custom Options

```csharp
var options = new AsyncKeyedLockerOptions 
{     
    MaxConcurrentLocksPerKey = 3,  // Allow 3 concurrent operations per key (default: 1)
    LockIdleCleanupInterval = TimeSpan.FromSeconds(60),  // Default: 60s
    LockIdleCleanupThreshold = TimeSpan.FromSeconds(30),  // Default: 10s
    LongHeldLockThreshold = TimeSpan.FromMinutes(1),  // Default: 30s
    DisposeDrainTimeout = TimeSpan.FromSeconds(10)  // Default: null (infinite)
};

await using var locker = new AsyncKeyedLocker<string>(logger, options);
```

#### With Factory (DI)

```csharp
public class OrderService 
{ 
    private readonly IAsyncKeyedLocker<string> _locker;

    public OrderService(IAsyncKeyedLockerFactory lockerFactory)
    {
        var options = new AsyncKeyedLockerOptions 
        {
            MaxConcurrentLocksPerKey = 5
        };
        
        _locker = lockerFactory.Create<string>(options);
    }
}
```

### Semaphore-Style Concurrency

By default, LockIt provides **exclusive locking** (one operation per key). Configure `MaxConcurrentLocksPerKey` to allow multiple concurrent operations on the same key:

```csharp
var options = new AsyncKeyedLockerOptions 
{
    MaxConcurrentLocksPerKey = 5  // Allow up to 5 concurrent operations per key
};

await using var locker = new AsyncKeyedLocker<string>(logger, options);

// Now up to 5 tasks can hold the lock for "shared-resource" simultaneously
await using (await locker.AcquireAsync("shared-resource"))
{
    // Up to 5 operations can be in this section concurrently
    await ProcessAsync();
}
```

**Use cases:**
- **Database connection pooling per tenant**: Limit concurrent queries per tenant to 5
- **API rate limiting per user**: Allow 3 concurrent requests per user
- **Resource throttling**: Control access to shared resources with configurable limits

### Configuration Options Reference

All options are optional. If not specified, sensible defaults are applied:

| Option                        | Default           | Description                                                                                                                         |
|-------------------------------|-------------------|-------------------------------------------------------------------------------------------------------------------------------------|
| `MaxConcurrentLocksPerKey`    | `1`               | Maximum concurrent locks per key. `1` = exclusive lock (serialized access), `>1` = semaphore-style (allows N concurrent operations) |
| `LockIdleCleanupInterval`     | `60s`             | How often the cleanup timer runs to remove idle locks                                                                               |
| `LockIdleCleanupThreshold`    | `10s`             | How long a lock must be idle (no holders or waiters) before being eligible for removal                                              |
| `LongHeldLockLoggingInterval` | `10s`             | How often long-held locks are checked and logged                                                                                    |
| `LongHeldLockThreshold`       | `30s`             | Duration threshold for logging a warning about long-held locks                                                                      |
| `DisposeDrainTimeout`         | `null` (infinite) | Maximum time to wait for in-flight locks to drain during disposal. `null` = wait indefinitely                                       |

## API Reference

### `IAsyncKeyedLocker<TKey>`

| Method                              | Description                                                       |
|-------------------------------------|-------------------------------------------------------------------|
| `AcquireAsync(key, timeout?, ct)`   | Acquires the lock. Returns an `IAsyncDisposable` lease.           |
| `TryAcquireAsync(key, timeout, ct)` | Non-throwing variant. Returns `TryAcquireResult`.                 |
| `GetQueueDepth(key)`                | Number of tasks holding or waiting on the lock for the given key. |

### `IAsyncKeyedLockerFactory`

| Method                   | Description                                                                                                |
|--------------------------|------------------------------------------------------------------------------------------------------------|
| `Create<TKey>(options?)` | Creates a new `IAsyncKeyedLocker<TKey>` instance. Options are optional; defaults are used if not provided. |

### `AsyncKeyedLocker<TKey>` Constructor

```csharp
public AsyncKeyedLocker(
    ILogger<AsyncKeyedLocker<TKey>> logger,
    AsyncKeyedLockerOptions? options = null,  // Optional - defaults used if null
    TimeProvider? timeProvider = null,  // Optional - defaults to TimeProvider.System
    LockItMetrics? metrics = null)  // Optional - creates standalone instance if null
```

All parameters except `logger` are optional with sensible defaults.

### `ServiceCollectionExtensions`

| Method        | Description                                                                              |
|---------------|------------------------------------------------------------------------------------------|
| `AddLockIt()` | Registers `IAsyncKeyedLockerFactory`, `LockItMetrics`, and `TimeProvider` as singletons. |

## Metrics (OpenTelemetry)

LockIt exposes metrics under the meter name `NLTechnologies.LockIt`:

| Instrument                     | Type          | Unit  | Description                   |
|--------------------------------|---------------|-------|-------------------------------|
| `lockit.locks.acquired`        | Counter       | locks | Total successful acquisitions |
| `lockit.locks.released`        | Counter       | locks | Total releases                |
| `lockit.locks.timed_out`       | Counter       | locks | Total acquisition timeouts    |
| `lockit.locks.active`          | UpDownCounter | locks | Currently held locks          |
| `lockit.locks.contention_time` | Histogram     | ms    | Time spent waiting to acquire |
| `lockit.cleanup.removed`       | Counter       | locks | Idle locks removed by cleanup |

Subscribe to your OpenTelemetry configuration:

```csharp
builder.Services.AddOpenTelemetry()
    .WithMetrics(m => m.AddMeter(LockItMetrics.MeterName));
```

## Project Structure

```text
LockIt/ 
├── src/ 
│   └── NLTechnologies.LockIt/ 
│       ├── AsyncKeyedLocker.cs 
│       ├── AsyncKeyedLockerFactory.cs 
│       ├── AsyncKeyedLockerOptions.cs 
│       ├── IAsyncKeyedLocker.cs 
│       ├── IAsyncKeyedLockerFactory.cs 
│       ├── LockItMetrics.cs 
│       ├── ServiceCollectionExtensions.cs 
│       └── TryAcquireResult.cs 
├── tests/ 
│   └── NLTechnologies.LockIt.Tests/ 
├── .github/workflows/ci.yml 
├── .editorconfig 
├── CHANGELOG.md 
├── CONTRIBUTING.md 
├── Directory.Build.props 
├── LICENSE 
├── NLTechnologies.LockIt.slnx 
└── README.md
```

## Contributing

Contributions are welcome! See [CONTRIBUTING.md](CONTRIBUTING.md) for guidelines.

## License

Licensed under the [Apache License 2.0](LICENSE).

Copyright © 2026 Noctua Lumen Technologies.
```
