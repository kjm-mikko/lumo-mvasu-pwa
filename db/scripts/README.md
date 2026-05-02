# Database schema scripts

This folder holds hand-runnable SQL scripts that introduce or modify schema
elements owned by `mVasu.Api`. The application **never** emits DDL — every
schema change is applied manually by the database owner.

## Naming

`NNN_short_description.sql` — three-digit prefix, applied in numeric order.
Increment the prefix for each new script; never edit one that has already been
applied to a shared environment.

## How to apply

```powershell
sqlcmd -S <server> -d <database> -U <user> -i db/scripts/001_create_mvasuusersettings.sql
```

Or open the script in SSMS / Azure Data Studio against the target database and
run it once. No idempotency wrapping is added by default; if you need a
re-runnable variant, copy the script, add an `IF NOT EXISTS` guard and bump the
prefix.

## Current scripts

| Script | Phase | Purpose |
| --- | --- | --- |
| `001_create_mvasuusersettings.sql` | 5 | Creates `MVasuUserSettings` for per-user theme / language / preferredName / locationConsent settings |
