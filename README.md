# 🔒 LockIt

[![NuGet](https://img.shields.io/nuget/v/NLTechnologies.LockIt.svg)](https://www.nuget.org/packages/NLTechnologies.LockIt)
[![License](https://img.shields.io/badge/license-Apache--2.0-blue.svg)](LICENSE)
[![CI](https://github.com/Noctua-Lumen-Technologies/LockIt/actions/workflows/ci.yml/badge.svg)](https://github.com/Noctua-Lumen-Technologies/LockIt/actions)

**Lightweight, async, per-key locking for .NET.** Serialize concurrent operations on the same key while allowing full parallelism across different keys.

## Features

| Feature | Description |
|---|---|
| **Per-key locking** | Operations on different keys run in parallel; same-key operations are serialized |
| **Async-first** | Fully `async`/`await`-based with no thread blocking |
| **Timeout & cancellation** | Optional `TimeSpan` timeout and `CancellationToken` on every acquisition |
| **Try-pattern** | `TryAcquireAsync` returns a result struct instead of throwing on timeout |
| **Automatic idle cleanup** | Stale locks are removed on a configurable timer to prevent unbounded growth |
| **Long-held lock detection** | Warnings are logged when a lock exceeds a configurable threshold |
| **Built-in metrics** | `System.Diagnostics.Metrics` instrumentation compatible with OpenTelemetry |
| **Graceful disposal** | `DisposeAsync` drains in-flight locks with an optional timeout |
| **Dependency injection** | One-line registration via `AddLockIt()` |
| **Testable** | `IAsyncKeyedLocker<TKey>` for mocking; `TimeProvider` for deterministic tests |

## Installation

dotnet add package NLTechnologies.LockIt

## Quick Start

### Manual Instantiation

``` csharp
using NLTechnologies.LockIt; using Microsoft.Extensions.Logging;
using var loggerFactory = LoggerFactory.Create(b => b.AddConsole()); var logger = loggerFactory.CreateLogger<AsyncKeyedLocker<string>>();
await using var locker = new AsyncKeyedLocker<string>(logger);
await using (await locker.AcquireAsync("order-123")) 
{ 
    // Only one task can execute this block for "order-123" at a time. 
    // Other keys like "order-456" are NOT blocked. 
    await ProcessOrderAsync("order-123"); 
}
```

### With Dependency Injection

Register all LockIt services in one call:

Program.cs => builder.Services.AddLockIt();

Then inject `IAsyncKeyedLocker<TKey>` where needed:

``` csharp
public class OrderService 
{ 
    private readonly IAsyncKeyedLocker<string> _locker;

    public OrderService(IAsyncKeyedLockerFactory lockerFactory)
    {
        _locker = lockerFactory.Create<string>();
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

Pass `AsyncKeyedLockerOptions` to customize behavior:

``` csharp
var options = new AsyncKeyedLockerOptions 
{     
    // how often cleanup runs 
    LockIdleCleanupInterval = TimeSpan.FromSeconds(60),  

    // idle time before removal 
    LockIdleCleanupThreshold = TimeSpan.FromSeconds(30),  
    
    // threshold for warnings 
    LongHeldLockThreshold  = TimeSpan.FromMinutes(1),   
    
    // max wait during disposal 
    DisposeDrainTimeout  = TimeSpan.FromSeconds(10) 
};

await using var locker = new AsyncKeyedLocker<string>(logger, options);
```

Options available:

| Option | Default | Description |
| --- | --- | --- |
| `LockIdleCleanupInterval` | 60 s | How often the cleanup timer runs |
| `LockIdleCleanupThreshold` | 10 s | Idle time before a lock is eligible for removal |
| `LongHeldLockLoggingInterval` | 10 s | How often long-held locks are checked |
| `LongHeldLockThreshold` | 30 s | Threshold for logging a long-held warning |
| `DisposeDrainTimeout` | `null` (∞) | Max wait for in-flight locks during disposal |

## API Reference

### `IAsyncKeyedLocker<TKey>`

| Method | Description |
| --- | --- |
| `AcquireAsync(key, timeout?, ct)` | Acquires the lock. Returns an `IAsyncDisposable` lease. |
| `TryAcquireAsync(key, timeout, ct)` | Non-throwing variant. Returns `TryAcquireResult`. |
| `GetQueueDepth(key)` | Number of tasks holding or waiting on the lock for the given key. |

### `IAsyncKeyedLockerFactory`

| Method | Description |
| --- | --- |
| `Create<TKey>(options?)` | Creates a new independent `IAsyncKeyedLocker<TKey>` instance. |

### `ServiceCollectionExtensions`

| Method | Description |
| --- | --- |
| `AddLockIt()` | Registers `IAsyncKeyedLockerFactory`, `LockItMetrics`, and `TimeProvider` as singletons. |

## Metrics (OpenTelemetry)

LockIt exposes metrics under the meter name `NLTechnologies.LockIt`:

| Instrument | Type | Unit | Description |
| --- | --- | --- | --- |
| `lockit.locks.acquired` | Counter | locks | Total successful acquisitions |
| `lockit.locks.released` | Counter | locks | Total releases |
| `lockit.locks.timed_out` | Counter | locks | Total acquisition timeouts |
| `lockit.locks.active` | UpDownCounter | locks | Currently held locks |
| `lockit.locks.contention_time` | Histogram | ms | Time spent waiting to acquire |
| `lockit.cleanup.removed` | Counter | locks | Idle locks removed by cleanup |

Subscribe in your OpenTelemetry configuration:

``` csharp
builder.Services.AddOpenTelemetry().WithMetrics(m => m.AddMeter(LockItMetrics.MeterName));
```

## Project Structure

```text
LockIt/ 
├── src/ │   
         └── NLTechnologies.LockIt/ │       
                                    ├── AsyncKeyedLocker.cs │       
                                    ├── AsyncKeyedLockerFactory.cs │       
                                    ├── AsyncKeyedLockerOptions.cs │       
                                    ├── IAsyncKeyedLocker.cs │       
                                    ├── IAsyncKeyedLockerFactory.cs │       
                                    ├── LockItMetrics.cs │       
                                    ├── ServiceCollectionExtensions.cs │       
                                    └── TryAcquireResult.cs 
├── tests/ │
           └── NLTechnologies.LockIt.Tests/ 
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
