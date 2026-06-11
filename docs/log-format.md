# Log Output Format

Each log entry is written as a single line (plus the platform newline) in a fixed, easily parseable format:

```text
[{timestamp} {level} {category}] {message}
```

Example:

```text
[2026-06-11 09:41:23.123 +10:00 INF MyApp.Services.OrderService] Order 42 submitted
[2026-06-11 09:41:23.456 +10:00 ERR MyApp.Services.OrderService] Payment failed for order 42
```

## Fields

### Timestamp

Local time with offset, formatted as `yyyy-MM-dd HH:mm:ss.fff zzz` — e.g. `2026-06-11 09:41:23.123 +10:00`.
Millisecond precision; the UTC offset makes entries unambiguous across time zones and DST transitions.

### Level

A fixed three-letter code:

| `LogLevel`    | Code  |
|---------------|-------|
| `Trace`       | `TRC` |
| `Debug`       | `DBG` |
| `Information` | `INF` |
| `Warning`     | `WRN` |
| `Error`       | `ERR` |
| `Critical`    | `CRT` |
| (other)       | `???` |

### Category

The logger category name — typically the fully qualified type name passed to `ILogger<T>` or
`ILoggerFactory.CreateLogger(string)`.

### Message

The message produced by the standard `Microsoft.Extensions.Logging` formatter, with all structured
placeholders (`{OrderId}` etc.) already rendered into the string.

## Behavior notes

- **Empty messages are skipped.** If the formatter produces a `null` or empty string, no line is written.
- **Exceptions** are included only insofar as the standard formatter renders them; pass exceptions via the
  `ILogger` exception parameter and they will be formatted by the framework's default formatter.
- **Scopes are not supported.** `BeginScope` is a no-op, and scope data does not appear in the output.
- **No level filtering happens in the provider.** Use the standard `Logging:File:LogLevel` configuration to
  filter (see [Getting started](getting-started.md#filtering-log-levels)).

## Parsing tips

The bracketed prefix has a fixed shape, so a simple regex can split entries:

```regex
^\[(?<ts>\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}\.\d{3} [+-]\d{2}:\d{2}) (?<level>[A-Z?]{3}) (?<category>[^\]]+)\] (?<message>.*)$
```

> [!NOTE]
> Messages may themselves contain newlines (e.g. rendered exceptions), in which case continuation lines will
> not match the prefix pattern — treat any line that doesn't match as a continuation of the previous entry.

