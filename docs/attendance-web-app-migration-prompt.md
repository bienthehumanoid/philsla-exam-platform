# Web app: attendance sync migration prompt

Use this prompt in the web-app repository to prepare the database and API work needed to sync proctor attendance. Do not apply a migration until the existing web-app schema, ORM conventions, and deployment process have been inspected.

```text
Implement the persistence migration and backend contract required for examination-session attendance syncing.

First inspect the repository. Identify:
- the database and ORM/migration tooling in use;
- the current models/tables for candidates, examination schedules or sessions, rooms, proctors, permits, and seat assignments;
- the API authentication and authorization conventions;
- existing audit-log, concurrency, soft-delete, timestamp, and migration naming conventions.

Do not duplicate existing concepts. Extend the established schema where possible, and report the files and existing entities you chose before generating the migration.

## Business rules to support

- Attendance belongs to one candidate's unique issued examination session, not to the candidate globally.
- A session is scheduled in one room and has one or more assigned proctors.
- Every candidate has a pre-assigned seat for that session. Seat labels must be non-empty and unique within the room session, case-insensitively.
- The only final attendance statuses are `Present`, `Late`, and `Absent`. A newly scheduled candidate is `Unmarked` until recorded; do not treat `Unmarked` as `Absent`.
- A proctor can record attendance manually, or by scanning the candidate's examination-permit QR code through a companion mobile app.
- The QR code identifies the candidate's issued examination session. It must not encode a reusable or predictable identifier and must be validated server-side.
- Attendance can be corrected until the session ends. After the session ends, normal proctor edits are rejected.
- A candidate marked `Absent` cannot start that session's exam. They must use the separate rescheduling process. This migration does not implement rescheduling.
- Multiple proctors or offline devices may sync the same attendance record. Preserve the full event history and detect conflicting updates rather than silently losing one.

## Data model requirements

Adapt the names to the existing domain model. Add only what is missing.

1. Ensure the session assignment/registration has:
   - candidate ID;
   - examination session or schedule ID;
   - room assignment ID;
   - pre-assigned seat label;
   - an active, revocable opaque QR/permit token reference (store a hash or secure token record; never require the raw QR payload to be stored);
   - a uniqueness constraint preventing more than one active assignment for the same candidate and examination session;
   - a case-insensitive unique constraint/index for seat labels within a room session.

2. Add an attendance-current-state record keyed uniquely by the candidate's session assignment. It should contain:
   - current status: `Unmarked`, `Present`, `Late`, or `Absent`;
   - recorded-at timestamp in UTC;
   - source: `Manual`, `QrScan`, or `Sync`;
   - recorded-by proctor/user ID when known;
   - device ID and client-generated event ID when supplied by the companion app;
   - a server-side concurrency token/version;
   - created/updated timestamps in UTC.

3. Add an append-only attendance-event/audit table. Each event should contain:
   - immutable event ID (accept a client-generated UUID for idempotent offline retry when appropriate);
   - candidate session assignment ID;
   - status transition (previous and new status, when applicable);
   - event type/source (`Manual`, `QrScan`, `Sync`, `Correction`);
   - actor/proctor ID, device ID, and optional sync batch ID;
   - client-recorded-at UTC and server-received-at UTC as separate fields;
   - an optional concise correction reason;
   - the resulting state version or reference required for conflict resolution.

4. Add indexes for:
   - roster lookup by room session and seat;
   - current attendance status by room session;
   - audit history by candidate session assignment and server-received time;
   - idempotency lookup by device ID plus client event ID (or the repository's equivalent).

## Sync/API contract requirements

- Add authenticated proctor-only endpoints/services for retrieving a room-session roster, recording one manual attendance action, and uploading an offline batch of scan events.
- Verify the proctor is assigned to the room session before exposing or changing its roster.
- Validate the QR/permit token against the candidate's issued examination session, its active/revoked state, and the target room session. Do not accept a QR code as proof without those checks.
- Make batch uploads idempotent: resending the same client event must return the prior accepted result, not create a duplicate audit event.
- Include the state version in responses and require it on corrections. On a stale/conflicting update, return a machine-readable conflict result containing the server's current state and version; do not use last-write-wins without recording the conflict.
- Apply the session-end edit gate server-side, using UTC/session timezone rules already established in the application. The UI gate is not sufficient.
- Return a useful result for each item in a batch (`accepted`, `duplicate`, `invalid`, `conflict`, or `session-closed`) so the mobile app can reconcile offline work.
- Do not automatically send the candidate into an exam from this sync endpoint. Exam-start authorization remains a separate server-side check against the current attendance state.

## Migration and delivery requirements

- Generate an additive, reversible migration following the repository's naming and migration conventions. Do not drop or rewrite existing data.
- Backfill existing session assignments as `Unmarked` only when that is consistent with the existing data; otherwise provide a safe explicit mapping and document it.
- Include model/entity configuration, DTOs/requests/responses, authorization checks, and tests—not only the migration.
- Add tests for seat uniqueness, token/session mismatch, proctor authorization, idempotent retry, stale-version conflict, session-end rejection, and audit-event append-only behavior.
- Report the migration name, schema changes, assumptions, rollout order, rollback plan, and any unresolved schema mismatches before applying it.
```

## Deliberate boundaries

This prompt does not add candidate rescheduling, QR generation/email delivery, device provisioning, or exam delivery. Those are separate web-app changes; the migration merely exposes the data and sync behavior they depend on.
