# B.Mendoza Superpowers Task Workspace

## Status

Planning and review only. Application implementation is not authorized until the
story briefs have been reviewed and an implementation plan has been approved.

## Developer identity

- Developer: Bienvenido Mendoza
- Documentation code: `B.Mendoza`
- Filesystem code: `b.mendoza`
- Implementation record: [b.mendoza.implement.md](implement/b.mendoza.implement.md)

## Repository baseline

Recorded on 2026-08-05 from
`C:\Users\bienvenido.mendoza\projects\philsla-exam-platform`.

- Repository: PhilSLA Exam Platform
- Branch: `master`
- Commit: `740c3c2`
- Working tree at inspection: clean
- Worktrees: one primary worktree at the repository path above
- On-disk `AGENTS.md` files: none found
- Existing shared Superpowers documents: preserved under the existing
  `docs/superpowers/plans/` and `docs/superpowers/specs/` directories

No repository instruction requiring a separate `b.mendoza` worktree was found.
This developer directory is additive and does not replace the shared structure.

## Assigned stories

| Story | Application owner | Brief | State |
| --- | --- | --- | --- |
| US-SR-012, Desktop Exam Software Application | Candidate | [Candidate task brief](specs/2026-08-05-us-sr-012-candidate-task-brief.md) | Awaiting review |
| US-SR-013, Desktop Exam Software Application (Proctor) | Proctor | [Proctor task brief](specs/2026-08-05-us-sr-013-proctor-task-brief.md) | Awaiting review |

No relevant issue or ticket link was supplied for either story.

## Ownership boundary

| Capability | Primary owner | Cross-application dependency |
| --- | --- | --- |
| Candidate authentication and local session | Candidate | Identity provider or temporary local account source |
| Attendance and eligibility | Proctor | Candidate consumes its own status |
| Exam-set assignment | Proctor | Candidate receives only its assigned package |
| Package validation, staging, and distribution | Proctor/local server | Candidate validates, receives, and acknowledges delivery |
| Hardware and connectivity checks | Candidate | Proctor receives status and may approve an override |
| Standby and examination start | Proctor controls; Candidate enforces | Requires an authenticated session-control contract |
| Answer capture and crash-safe local storage | Candidate | Proctor receives durable synchronized records |
| Distribution and device monitoring | Proctor | Candidate reports receipt and health |
| Offline and central synchronization | Shared protocol | Both applications require idempotency and integrity checks |
| Audit trail | Proctor for Proctor actions; shared for protocol events | Stable identities, timestamps, and event identifiers are required |

## Confirmed interpretation decisions

- US-SR-012 is scoped to Student/Candidate behavior. Proctor behavior is an
  external dependency unless needed to define an integration boundary.
- US-SR-013 is scoped to Proctor behavior. Its copied `As a Student` persona is a
  confirmed source defect; the source discrepancy remains recorded in its brief.
- The current email-and-password Candidate login is an intentional temporary path.
  LRN ID authentication depends on an unconfirmed third-party methodology and is
  not an immediate migration requirement.
- The two supplied stories duplicate most narrative, criteria, business rules,
  messages, and permissions. Criteria are assigned to one primary application and
  referenced as dependencies by the other rather than implemented twice.

## Product decisions required before implementation planning

1. Choose manual or automatic accommodation-package assignment. The narrative
   states both; ED-AC-004, ED-AC-011 through ED-AC-014, and BR-05 specify manual
   Proctor selection.
2. Define whether a Proctor downloads an assigned signed package, uploads package
   variants into the local software, or performs both operations, including which
   system is authoritative at each step.
3. Define the late-arrival sequence: when a newly Late candidate becomes eligible,
   how post-start distribution is initiated, and how that exception relates to the
   all-eligible-deliveries start gate.
4. Define the trusted-clock policy for offline start time, scheduled end time,
   grace-period expiry, and later reconciliation.
5. Define package format, signing, encryption, key distribution, local transport,
   delivery acknowledgement, retry, and idempotency contracts.
6. Define what `encrypted SQLite datastore` means in this architecture, including
   key custody, database encryption, record-level protection, and recovery.
7. Define central synchronization ownership and duplicate-prevention identifiers.
8. Confirm which pre-examination checks are mandatory in pure offline mode and who
   may override each failed check.

## Review gate

The next permitted action is review and correction of the two task briefs. After
approval, create a separate implementation plan with explicit file boundaries,
tests, security decisions, and incremental deliverables. Do not implement
application code directly from these briefs.
