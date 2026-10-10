# Decision log

Decisions made while building the submission, with the alternatives considered. The [README](../README.md) summarizes the outcome; this file records the reasoning. Built against upstream commit `87768d6`.

## 1. Interpreting the assignment

| # | Question | Decision | Reasoning |
| --- | --- | --- | --- |
| A1 | Which requirements document? | The PDF in the repository | It is newer than the emailed PDF, the README names it as the source, and the MVC starter the older PDF describes no longer exists |
| A2 | "Do not add or remove files unless instructed" | No starter file deleted or renamed; required files added | Taken literally the rule would forbid building anything. The README instructs adding EF Core, endpoints, UI, scripts, and the database |
| A3 | Soft delete scope | Columns and protections on Provider and License; delete endpoint for providers only | "All delete operations must use soft deletion," but only provider delete is required |
| A4 | Licenses of a deleted provider | Hidden through the provider filter, not flagged individually | No cascading writes; restore stays a one-row change; the audit trail reflects what the user actually did |
| A5 | What does "expired" mean? | `ExpirationDate < today`, whatever the stored status | The assignment says expiration is determined by `ExpirationDate`. Stored status can lag behind the date |
| A6 | Is the expiration date inclusive? | Yes; valid through that day | The usual reading of "expires on" |
| A7 | Which "today"? | US Eastern, from an injectable clock | A Georgia agency; a UTC date changes at 7 or 8 p.m. local time. Injectable so tests can fix the date |
| A8 | "Appearing active but having expired licenses" | Active, not deleted, at least one expired license; plus a `hasNoValidLicense` flag | Covers both the "any expired" and the "nothing valid" readings |
| A9 | "Active providers and their active licenses" | Active providers with their valid licenses; providers with none still listed | Hiding them would hide a compliance gap |
| A10 | License number uniqueness | Unique per provider, including deleted licenses | Data dictionary: "usually unique per provider." Numbers are not reused |
| A11 | Provider name uniqueness | Unique per county among live providers, case-insensitive | Prevents double entry; allows the same name in another county; a deleted provider does not block re-creation |
| A12 | Status versus deletion | Separate columns | Inactive is a business state and still listed; deleted is a record state |
| A13 | "Excluded from standard operations" | GET, PUT, DELETE on a deleted provider return 404 | The record does not exist as far as standard operations are concerned |
| A14 | Restore | Out of scope | Not required; the schema supports it |
| A15 | "Audit-safe persistence" | Created, modified, and deleted by and at; concurrency stamp; delete-blocking triggers | Answers who and when without a full history table |
| A16 | County | Lookup table of Georgia's 159 counties with a foreign key | Free text produces spelling variants that break reporting |
| A17 | Tests | Added for backend and frontend | Not required, but they are how the soft-delete guarantees are demonstrated |
| A18 | .NET 8 or .NET 10 | .NET 8 | Matches the starter and its listed prerequisites. End of support in November 2026 is noted as follow-up work |
| A19 | UI to API wiring | Vite dev proxy for `/api` | One origin in the browser; no CORS to configure or get wrong |
| A20 | Search, filters, paging | `search`, `status`, `countyId`, `page`, `pageSize` (maximum 100) | The data dictionary calls the name "searchable" |
| A21 | Sample dates drifting | Dates far in the past or far in the future, and a test that asserts each scenario has rows | A date that is "active" today would silently expire later |

## 2. Design decisions

### 2.1 Soft-delete mechanism

| Option | For | Against |
| --- | --- | --- |
| **`IsDeleted` flag plus `DeletedUtc` and `DeletedBy` (chosen)** | Intent is obvious in the table; partial indexes read naturally | Flag and timestamp could disagree; prevented with a CHECK constraint |
| `DeletedUtc` only | One source of truth | Slightly less clear to read; same enforcement work |
| Move deleted rows to an archive table | Live table stays small | Breaks foreign keys from licenses; restore becomes a data move; more ways to lose data |

Enforcement is layered: global query filter, save interceptor, delete-blocking triggers, CHECK constraint, and views. Any one layer can be explained and tested alone, and a mistake in one is caught by another.

### 2.2 Schema source of truth

| Option | For | Against |
| --- | --- | --- |
| **EF Core migrations, with SQL for triggers and views (chosen)** | One mechanism builds everything; standard for .NET teams; repeatable | The generated script is less readable than hand-written SQL |
| Hand-written SQL, EF maps to it | Most readable for a DBA | Model and scripts can drift |
| `EnsureCreated()` | Fastest | No upgrade path |

### 2.3 Status storage

Text with a CHECK constraint, mapped to a C# enum. Readable in a raw query and rejected by the database when invalid. An integer enum is unreadable to support staff; a lookup table is more than three fixed values need.

### 2.4 API shape

Controllers, a thin service layer, and the `DbContext`. No repository pattern over EF Core. Services return a value or an expected failure (not found, conflict, validation) and controllers map that to RFC 7807 responses. Request and response types are separate from entities.

### 2.5 Concurrency

A `ConcurrencyStamp` replaced on every save. The update compares the stamp the client sent, and the `UPDATE` statement also carries it in its `WHERE` clause, so a change made between the check and the write is still caught.

## 3. Decisions made during implementation

These differ from, or add to, the original plan.

| Decision | Reasoning |
| --- | --- |
| The sample data is one SQL script (`03_seed_data.sql`), embedded in the API and run by it | A C# seeder plus an exported script would be two definitions that could drift. One file serves the application, the tests, and a reviewer running it by hand |
| `01_schema.sql` is not idempotent | EF Core cannot generate idempotent migration scripts for SQLite. The script builds a database from empty |
| County rows are part of the migration | Reference data belongs with the schema; it must exist in production and in tests, unlike sample providers |
| The standard view is named `vw_Providers` | The planned name `vw_ActiveProviders` would be confused with provider status `Active`; the view means "not deleted" |
| The county filter is `countyId`, not a county name | Exact, and matches what the dropdown holds |
| `dotnet run -- --init-db` | A repeatable way to build and populate the database that exits cleanly, so no `-wal` or `-shm` file is left beside the committed `.db` |
| HTTPS redirection is disabled in Development only | The dev proxy targets the http endpoint; a redirect would move the browser to another origin |
| A page size above 100 is rejected with 400 | Explicit is better than silently returning fewer rows than asked for |
| Request status is a string validated against the enum | An unknown value produces a field-level message instead of a JSON deserialization error |
| Backend tests use SQLite in memory, built by the migrations | The EF Core InMemory provider enforces no constraints, indexes, triggers, or views |
| The dashboard uses CSS bars and an SVG pie, not a charting library | A few counts do not justify a dependency |
| Every dashboard number opens a read-only list of its records, selected by the API | The reader can check any number. The count and its list share one query in `DashboardService`, so they cannot disagree, and the filtering is not repeated in the browser |
| Dashboard record lists are capped at 500 rows, without paging | An overview, not an export. Paging would be the next step for larger data |
| The UI has no router | Four views; component state is enough |
| The UI validates required fields and length; the API validates everything | Immediate feedback for the user, with one authority for the rules |
| Provider licenses open in a modal | Keeps the list in place instead of pushing the detail below the table |
| Issuing a license was added, limited to providers with no valid license | Lets a reviewer close the compliance gap the reports reveal. The eligibility rule is enforced by the API (409), not only by hiding the button |
| The license number is derived from the new row's id | "Highest existing number plus one" can hand the same number to two simultaneous requests; an identity value cannot. The sample numbers already follow this pattern |
| The number is assigned in the same transaction with a direct update | It is part of creating the row, so it does not stamp the license as modified |
| A `Deleted (Audit)` view was added to the UI | Makes "deleted records remain queryable" visible to a reviewer without opening the database |

## 4. Known limits

- The county list was entered from memory and checked for count, duplicates, and order only.
- No authentication; audit columns hold the placeholder `anonymous`.
- SQLite allows one writer at a time.
