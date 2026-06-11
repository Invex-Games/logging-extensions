# File Rollover & Retention

Log files are rolled over (renamed and replaced with a fresh file) based on **size** and **time**, and old
files are purged once the total disk usage hits a configurable cap.

## File naming

| File                        | Name                                |
|-----------------------------|--------------------------------------|
| Active log file             | `{LogName}.log`                      |
| Rolled-over log file        | `{LogName}_{yyMMdd-HHmmss}.log`      |
| Rolled-over (name collision)| `{LogName}_{yyMMdd-HHmmss}_{n}.log`  |

The timestamp is the **local time at the moment of rollover**. If a file with that name already exists
(e.g. two rollovers within the same second), a numeric suffix `_1`, `_2`, … is appended.

Example:

```text
Logs/
    my-app.log                    <- active
    my-app_260610-000001.log
    my-app_260611-093015.log
    my-app_260611-093015_1.log    <- second rollover in the same second
```

## Size-based rollover

Before each write, the provider checks whether the active file's size plus the size of the pending entries
would reach `FileSizeLimitBytes` (default **100 MiB**). If so, the active file is renamed using the scheme
above and a new active file is started.

> [!NOTE]
> The check is performed *before* the write, so an individual file may finish slightly under the limit, and a
> single very large batch is never split across files.

## Time-based rollover

Before each write, the provider also compares the active file's **creation time** against
`RolloverInterval` (default **`Day`**). If the elapsed time meets or exceeds the interval, the file is
rolled over.

| `FileRolloverInterval` | Rolls over after |
|------------------------|------------------|
| `Infinite`             | Never (size-based rollover still applies) |
| `Year`                 | 365 days         |
| `Month`                | 30 days          |
| `Day`                  | 1 day            |
| `Hour`                 | 1 hour           |
| `Minute`               | 1 minute         |

> [!IMPORTANT]
> Intervals are **elapsed durations from file creation**, not calendar boundaries. A file created at 14:30
> with `Day` rollover rolls at ~14:30 the next day, not at midnight. Similarly, `Month` means 30 days and
> `Year` means 365 days.

Rollover checks only happen when an entry is written — an idle application will not roll files until the
next log entry arrives.

## Retention (purging)

Whenever a rollover occurs (or a brand-new log file is created), the provider sums the sizes of all
rolled-over files matching `{LogName}_*.log` in the log directory. If the total meets or exceeds
`MaxTotalSizeBytes` (default **10 GiB**), the **oldest** rolled-over file (by creation time) is deleted.

> [!NOTE]
> - The active `{LogName}.log` file is never purged.
> - One file is deleted per rollover. Because purging runs on every rollover, disk usage stays bounded over
>   time; however, if you drastically lower `MaxTotalSizeBytes` on an existing large directory, it will take
>   several rollovers to converge to the new cap.
> - When using `PerLevelLogName`, each base name is rolled and purged independently — `MaxTotalSizeBytes`
>   applies per name, not across all files.

## Choosing limits

A quick rule of thumb for worst-case disk usage per log name:

```text
worst case ≈ MaxTotalSizeBytes + FileSizeLimitBytes
```

(the cap on rolled-over files, plus one full active file).

Common setups:

| Scenario                         | Suggested settings                                              |
|----------------------------------|------------------------------------------------------------------|
| Long-running service             | `RolloverInterval = Day`, defaults otherwise                     |
| High-volume service              | `RolloverInterval = Hour`, `FileSizeLimitBytes = 50 MiB`         |
| Disk-constrained device          | `MaxTotalSizeBytes = 512 MiB`, `FileSizeLimitBytes = 10 MiB`     |
| Short-lived CLI tool             | `RolloverInterval = Infinite`, small `MaxTotalSizeBytes`         |

