# Buffered vs. Direct Writing

`AddFile` accepts a `buffered` flag that selects between two write strategies:

```csharp
builder.Logging.AddFile();                 // buffered (default)
builder.Logging.AddFile(buffered: false);  // direct
```

## Buffered mode (default)

In buffered mode, calling a log method only enqueues the formatted entry onto an in-memory channel — a fast,
non-blocking operation. A dedicated background thread drains the channel in small batches (up to 10 entries
per iteration), groups entries by resolved file name and level, and performs the actual file I/O:
rollover checks, purging, and appending. Entries at the same level that share a destination retain their
queue order; batching does not guarantee ordering across levels.

The group is captured when the log call occurs, so ending a group scope before the background write
does not affect routing. Group and level mappings are resolved from the current configuration for each
batch; a configuration reload can therefore change the destination of queued entries.

**Characteristics:**

- ✅ Minimal overhead on application threads — file I/O never blocks your code.
- ✅ Batching reduces the number of file opens/writes under load.
- ✅ Slow disks or transient I/O errors don't stall the application.
- ⚠️ Entries queued but not yet written are lost if the process crashes or is killed.
- ⚠️ The in-memory queue is unbounded; if the disk cannot keep up with sustained extreme log volume, memory
  usage grows.

**Shutdown:** disposing the logging infrastructure (which hosts do automatically on graceful shutdown)
closes the queue and blocks until the background thread has drained all accepted entries and exited.
Disposal can immediately follow the last log call; no delay is needed to let the writer catch up.
The same retry-and-drop policy described below applies while draining, and entries logged after disposal
are dropped. Entries still queued when the process crashes or is killed cannot be flushed.

## Direct mode

In direct mode, every log call performs the full write synchronously on the calling thread: the rollover and
purge checks run, and the entry is appended and flushed to disk before the call returns.

**Characteristics:**

- ✅ Every entry is durably on disk when the log call returns — nothing is lost on abrupt termination.
- ✅ No background thread, no in-memory queue.
- ⚠️ Each log call pays the cost of file I/O, on the calling thread.
- ⚠️ Higher contention under heavily concurrent logging.

## Which should I use?

| Scenario                                                       | Recommendation |
|----------------------------------------------------------------|----------------|
| Web apps, services, anything long-running                      | **Buffered**   |
| High-throughput logging                                        | **Buffered**   |
| Short-lived CLI tools that may exit immediately after logging  | **Direct**     |
| Crash diagnostics where the last entries matter most           | **Direct**     |
| Audit-style logs that must be durable per call                 | **Direct**     |

## Error handling

Both modes are designed to never take the application down due to logging failures:

- Failed file writes are retried up to five times; the exception is echoed to the console and debug output.
- In buffered mode, if a batch repeatedly fails to write, it is skipped and the writer moves on.
- In direct mode, if an entry repeatedly fails to write, it is dropped.

