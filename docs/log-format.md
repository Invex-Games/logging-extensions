# File Log Output Format

Each file log entry has a bracketed prefix followed by the formatter's message and a platform newline:

```text
[{timestamp} {level} {category}] {message}
```

Files are appended as UTF-8 text. Messages are written verbatim and may contain embedded newlines, so
one entry can span several physical lines.

Example:

```text
[2026-06-11 09:41:23.123 +10:00 INF MyApp.Services.OrderService] Order 42 submitted
[2026-06-11 09:41:23.456 +10:00 ERR MyApp.Services.OrderService] Payment failed for order 42
```

## Fields

### Timestamp

Local time with offset, formatted as `yyyy-MM-dd HH:mm:ss.fff zzz` — e.g. `2026-06-11 09:41:23.123 +10:00`.
Millisecond precision; the UTC offset makes entries unambiguous across time zones and DST transitions.
The timestamp is captured when the log call is made, before buffered entries are queued. Formatting
uses the logging thread's current culture, including its calendar and time separator; the examples
assume a Gregorian calendar and `:` time separator.

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
Category names are cached case-insensitively within the provider, so requests that differ only in case
reuse the first category spelling. The category is not escaped.

### Message

The message produced by the standard `Microsoft.Extensions.Logging` formatter, with all structured
placeholders (`{OrderId}` etc.) already rendered into the string.
When calling `ILogger.Log<TState>` directly, the supplied formatter determines the entire message.
Structured property names and values are not persisted separately.

## Behavior notes

- **Empty messages are skipped.** If the formatter produces a `null` or empty string, no line is written.
- **Exceptions are not appended automatically.** The exception is passed to the formatter, and only
  that formatter's returned string is written. The usual `LogError(exception, "message")` helpers do not
  include exception details in that string. To include details, put them in the message, for example
  `logger.LogError("Payment failed: {Exception}", exception.ToString())`, or supply a formatter that renders them.
- **Event IDs are not printed.** Include an identifier in the message if it must appear in the file.
- **Whitespace is preserved.** A whitespace-only message is written, and embedded newlines are not escaped.
- **Scopes support group routing.** `BeginGroupScope` or a structured scope's `"Group"` property selects
  a configured file destination. Scope data is not printed; the line format stays the same. See
  [group routing](configuration.md#routing-groups-to-separate-files).
- **No level filtering happens in the provider.** Use the standard `Logging:File:LogLevel` configuration to
  filter (see [Getting started](getting-started.md#filtering-log-levels)).

## Parsing tips

The bracketed prefix has a fixed shape, so a simple regex can split entries:

```regex
^\[(?<ts>\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}\.\d{3} [+-]\d{2}:\d{2}) (?<level>[A-Z?]{3}) (?<category>[^\]]*)\] (?<message>.*)$
```

> [!NOTE]
> This pattern assumes the culture used in the examples and a category without `]` or newlines. Treat
> nonmatching lines after an entry as continuation lines. A message can itself contain a line that looks
> like a prefix, so this text format cannot guarantee lossless recovery of arbitrary multiline messages.
> If precise structured parsing is required, use a provider with a structured output format.

