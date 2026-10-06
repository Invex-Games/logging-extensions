# File Rollover & Retention

Log files are rolled over (renamed and replaced with a fresh file) based on **size** and **time**, and old
archives are purged when their combined size reaches a threshold for their resolved file name.
Size and retention settings are thresholds rather than hard disk quotas.

## File naming

In the names below, `name` means `LogName` (which defaults to the application friendly name), followed by any
nonempty group and level suffixes: `{baseName}[_{groupSuffix}][_{levelSuffix}]`. Omitted suffixes add no
separator. See [group routing](configuration.md#routing-groups-to-separate-files) for the selection rules.

| File                        | Name                                |
|-----------------------------|--------------------------------------|
| Active log file             | `{name}.log`                      |
| Rolled-over log file        | `{name}_{yyMMdd-HHmmss}.log`      |
| Rolled-over (name collision)| `{name}_{yyMMdd-HHmmss}_{n}.log`  |

The timestamp is the **local time at the moment of rollover**. If a file with that name already exists
(e.g. two rollovers within the same second), a numeric suffix `_1`, `_2`, … is appended. Configured active
destinations are also skipped when choosing an archive name, even if their files do not yet exist.

Case-only path aliases are checked against the destination filesystem for buffered routing, reserved
active names, and archive retention. For example, `app_orders` and `app_ORDERS` share a destination on
a case-insensitive directory and remain separate on a case-sensitive directory. Archive extensions
follow the same rule, including `.LOG` when the directory ignores case.

Ambiguous case-only comparisons create and delete a unique temporary `.tmp` file in the destination
directory. Results are reused within the batch or rollover/retention operation. The directory must
permit creating and deleting this file; failures follow the usual write retry and drop behavior.

Example:

```text
Logs/
    my-app.log                    <- active
    my-app_260610-000001.log
    my-app_260611-093015.log
    my-app_260611-093015_1.log    <- second rollover in the same second
```

## Size-based rollover

Before each write to an **existing** active file, the provider checks whether its size plus the UTF-8 byte
count of the pending entries would meet or exceed `FileSizeLimitBytes` (default **100 MiB**). The pending
size includes the formatted prefix, message, and trailing platform newline. If the threshold is reached,
the active file is renamed using the scheme above and the entire pending write goes to a new active file.

> [!NOTE]
> The check is performed *before* the write. For example, an existing 90-byte file with a 100-byte threshold
> rolls over before a 10-byte append. New files are written without splitting entries or batches, so an
> oversized entry or batch can create a file larger than the threshold. Buffered writes check each
> destination/level bucket separately, with at most 10 queued entries in the overall batch.

Zero or negative size thresholds cause every write to an existing active file to roll over. Use a large
positive `FileSizeLimitBytes` if you want size-based rollover to be infrequent.

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
> Intervals are **elapsed durations from file creation**, not calendar boundaries. `Day` means 24 hours:
> a file created at 14:30 becomes eligible at 14:30 the next day if the UTC offset has not changed. A daylight
> saving transition can change that local clock time. `Month` means 30 days and `Year` means 365 days.

Rollover checks only happen when an entry is written — an idle application will not roll files until the
next log entry arrives. New active files are assigned the current UTC creation time. Existing active
files are appended after a restart, subject to the same size and creation-time checks; restarting the
application does not itself trigger rollover. Size is checked first, and a size rollover starts a fresh
file without a second time rollover for the same write.

## Retention (purging)

Whenever a rollover occurs (or a brand-new log file is created), the provider sums the sizes of all
rolled-over files matching the exact resolved base name followed by `_yyMMdd-HHmmss`, optionally a positive numeric
collision suffix, and `.log` in the log directory. If the total meets or exceeds
`MaxTotalSizeBytes` (default **10 GiB**), the **oldest** rolled-over file (by creation time) is deleted.

The archive suffix is matched by its shape: six digits, a hyphen, six digits, and optionally `_` followed
by a positive integer with no leading zero. For example, `_260611-093015_1.log` qualifies, while
`_260611-093015_0.log`, `_260611-093015_01.log`, and `_backup.log` do not. The digits are not validated as
a calendar date. The filename timestamp records rollover time; it is not used to select the oldest file.

> [!NOTE]
> - Active destinations from the current configuration are never purged, even if a configured name
>   resembles another file's archive name.
> - At most one file is deleted per check, before the new active file is written. The total can remain above
>   the threshold afterward, especially after lowering the setting or writing oversized entries. There is
>   no cleanup loop that immediately brings the directory below the threshold.
> - Names with group, level, or combined suffixes roll and purge independently — `MaxTotalSizeBytes`
>   applies per resolved base name, not across all files. Routes that resolve to the same name share the limit.
> - Archives for `app_orders_errors` do not count toward the limit for `app_orders`; similar prefixes alone do
>   not make a file part of that route's archive set.

Retention is checked only when creating a new active file, including after a rollover. Ordinary appends
and periods with no logging do not purge archives. Files belonging to routes removed or renamed by a
configuration reload are left in place unless a later write selects the same resolved route again.
Zero or negative retention thresholds still delete at most one qualifying archive per check.

## Choosing limits

A starting storage budget per resolved log name is:

```text
estimated budget ≈ MaxTotalSizeBytes + FileSizeLimitBytes
```

(the archive threshold, plus an active file near the rollover threshold). This is an estimate, not an
upper bound: oversized writes, existing archive backlogs, and deletion of only one file per check can
exceed it. Budget for every distinct resolved file name and allow additional headroom. Monitor actual
disk usage if running out of space would affect the application.

Common setups:

| Scenario                         | Suggested settings                                              |
|----------------------------------|------------------------------------------------------------------|
| Long-running service             | `RolloverInterval = Day`, defaults otherwise                     |
| High-volume service              | `RolloverInterval = Hour`, `FileSizeLimitBytes = 50 MiB`         |
| Disk-constrained device          | `MaxTotalSizeBytes = 512 MiB`, `FileSizeLimitBytes = 10 MiB`     |
| Short-lived CLI tool             | `RolloverInterval = Infinite`, small `MaxTotalSizeBytes`         |

