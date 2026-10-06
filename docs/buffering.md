# Buffered vs. Direct Writing

`AddFile` accepts a `buffered` flag that selects between two write strategies:

```csharp
builder.Logging.AddFile();                 // buffered (default)
builder.Logging.AddFile(buffered: false);  // direct
```

Choose one registration line for an application. Repeated registrations of the same mode reuse its
provider, but registering both modes adds both providers and can write each entry twice.

## Buffered mode (default)

In buffered mode, a log call formats the message, captures its timestamp and scope group, then enqueues
the entry onto an unbounded in-memory channel. A dedicated writer thread drains the channel in small
batches (up to 10 entries per iteration), groups entries by resolved file name and level, and performs the actual file I/O:
rollover checks, purging, and appending. Entries at the same level that share a destination retain their
queue order; batching does not guarantee ordering across levels.

The group is captured when the log call occurs, so ending a group scope before the background write
does not affect routing. Group and level mappings are resolved from the current configuration for each
batch; a configuration reload can therefore change the destination of queued entries.
Message text and timestamps remain those captured by the original log call.

**Characteristics:**

- ✅ File I/O runs on the writer thread; application threads perform formatting, scope capture, and queueing.
- ✅ Batching reduces the number of file opens/writes under load.
- ✅ Slow disks or transient I/O errors don't stall the application.
- ⚠️ Entries queued but not yet written are lost if the process crashes or is killed.
- ⚠️ The in-memory queue is unbounded; if the disk cannot keep up with sustained extreme log volume, memory
  usage grows.

**Shutdown:** disposing the host or logger factory
closes the queue and blocks until the background thread has drained all accepted entries and exited.
Disposal can immediately follow the last log call; no delay is needed to let the writer catch up.
The same retry-and-drop policy described below applies while draining, and entries logged after disposal
are dropped. Entries still queued when the process crashes or is killed cannot be flushed.

For a Generic Host, retain its disposal scope and await application shutdown:

```csharp
using var host = builder.Build();
await host.RunAsync();
```

There is no public flush method. Disposing the host or logger factory is the way to wait for accepted
buffered entries. A slow or blocked filesystem can delay disposal.

## Direct mode

In direct mode, every log call performs the full write synchronously on the calling thread: the rollover and
purge checks run, and a successful write appends the entry and flushes stream buffers before returning.
The provider flushes its text writer and closes the stream after each write. This hands the bytes to
the operating system; it does not request a durable storage flush. A successful return therefore does
not guarantee survival through power loss or storage failure, and exhausted write retries can still
drop the entry.

**Characteristics:**

- ✅ Successful writes finish before the log call returns, so entries do not remain in an application queue.
- ✅ No background thread, no in-memory queue.
- ⚠️ Each log call pays the cost of file I/O, on the calling thread.
- ⚠️ Higher contention under heavily concurrent logging.

Concurrent direct log calls have no guaranteed order. Buffered mode preserves queue order within the
same destination and level, but grouping can change ordering across levels.

## Which should I use?

| Scenario                                                       | Recommendation |
|----------------------------------------------------------------|----------------|
| Web apps, services, anything long-running                      | **Buffered**   |
| High-throughput logging                                        | **Buffered**   |
| Short-lived CLI tools that may exit immediately after logging  | **Direct**     |
| Crash diagnostics where the last entries matter most           | **Direct**     |
| Code that needs synchronous completion of each write attempt   | **Direct**     |

## Error handling

Both modes catch failures from routing and file operations:

- Each protected operation gets an initial attempt and up to five retries: **six attempts total**, with
  no delay or backoff. Each failure is reported to standard console output and debug output; failures
  while reporting are also caught.
- In buffered mode, an entry that cannot be routed is skipped. If a destination/level bucket repeatedly
  fails to write, that bucket is dropped and the writer continues with the other buckets and later batches.
- In direct mode, if an entry repeatedly fails to write, it is dropped.

Log calls provide no success result or failure callback. Retries repeat the operation, so a failure
after part of an append has succeeded can produce duplicate text; delivery is not exactly once.
These protections cover the provider's writer and scope capture. A custom formatter is invoked before
them, and an exception from that formatter can propagate to the logging caller.

