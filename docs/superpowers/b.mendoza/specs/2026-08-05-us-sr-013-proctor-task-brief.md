# US-SR-013 Proctor Task Brief

## Status

Initial brief awaiting review. This document defines Proctor ownership and planning
inputs; it is not an implementation plan.

## Story

- ID: US-SR-013
- Title: Desktop Exam Software Application (Proctor)
- Corrected persona for this brief: Proctor
- Objective: allow an authorized Proctor to prepare and control an assigned
  examination session, manage attendance and package assignment, distribute
  packages, supervise Candidate readiness, start the examination, monitor progress,
  and preserve auditable online/offline records.
- Relevant issue or ticket: none supplied

The source incorrectly repeats US-SR-012's `As a Student` persona and Candidate goal.
The user confirmed that US-SR-013 is the Proctor story. This brief corrects ownership
without altering the source acceptance-criterion identifiers.

## Current repository state

The Proctor application already provides a .NET 10 MAUI Blazor Hybrid shell,
temporary local email-and-password authentication, and a fixture-based home screen.
The architecture classifies its operational controls as a scaffold and the local
ASP.NET Core Proctor server as planned. Package security and transport, attendance,
live session control, Candidate connectivity, audit persistence, recording transfer,
and central synchronization are not implemented.

## Proctor-owned scope

1. Authorize a Proctor only for assigned schedules, rooms, packages, and rosters.
2. Download and validate assigned examination packages and stage the required
   standard and accommodation variants after the package workflow is clarified.
3. Record, lock, unlock, confirm, and audit attendance changes.
4. Default eligible Candidates to the standard package and assign approved
   accommodation variants according to the resolved assignment policy.
5. Distribute each intended package over the local network and track per-workstation
   pending, successful, and failed delivery states.
6. Block session start until the applicable distribution gate is satisfied.
7. Transition Not Checked In Candidates to Late at start and Late Candidates to
   Absent when the grace period expires.
8. Receive Candidate validation results, record technical incidents, and issue
   authorized overrides with reasons.
9. Emit authoritative start and session-control events and preserve the original
   scheduled end for Late Candidates.
10. Monitor the active session and synchronization health in online, offline-first,
    and pure-offline operation.
11. Record every security-relevant Proctor action and protocol outcome in a durable,
    queryable audit trail.

## Explicitly out of Proctor scope

- Candidate credential entry and identity-provider UI
- Candidate-local hardware APIs and validation execution
- Candidate examination rendering, navigation, and answer editing
- Candidate-local answer transaction semantics
- Student Portal launch-option behavior
- Testing Center Administrator assignment workflows
- System Administrator package deletion and system configuration
- unconfirmed third-party LRN integration

## Acceptance ownership

### Examination delivery

US-SR-013 assigns the Proctor side of ED-AC-001 through ED-AC-014 to this brief:

- show only packages assigned to the logged-in Proctor and scheduled session;
- support one standard and one or more accommodation-specific variants;
- default Candidates to the standard set;
- change approved accommodation assignments before attendance is locked;
- distribute only to eligible Present or Late Candidates and never to Absent or
  otherwise ineligible Candidates;
- show successful, pending, and failed totals plus per-workstation state;
- prevent start during distribution or while a required delivery remains
  unsuccessful; and
- prevent accommodation variants from reaching unintended recipients.

### Examination operations

| Source | Proctor outcome |
| --- | --- |
| EO-AC-001 and EO-AC-002 | Enforce assigned schedule, room, session, and roster scope. |
| EO-AC-003 | Record all Proctor actions in the audit trail. |
| EO-AC-004 and EO-AC-005 | Check in physically present Candidates and preserve Not Checked In for all others. |
| EO-AC-006 through EO-AC-009 | Apply Late and Absent transitions and update attendance totals. |
| EO-AC-010 | Require explicit confirmation and audit attendance corrections. |
| EO-AC-011 and EO-AC-012 | Keep validated Candidates in Standby and issue the authoritative start event. |
| EO-AC-013 | Preserve the scheduled end time for Late Candidates. |
| EO-AC-014 | Consume validation failures and control authorized overrides. |
| EO-AC-015 and EO-AC-016 | Monitor Candidate persistence and synchronization outcomes through shared contracts; Candidate owns local answer storage. |

## Required Proctor session states

The implementation plan must define a deterministic state model covering at least:

`Assigned` -> `PackageStaged` -> `AttendanceOpen` -> `AttendanceLocked` ->
`Distributing` -> `ReadyToStart` -> `InProgress` -> `Ended` ->
`SynchronizationPending` -> `Completed`.

Unlocking attendance, correcting attendance, retrying distribution, admitting a Late
Candidate, overriding validation, operating offline, and executing an emergency stop
must have explicit allowed source states, resulting states, authorization rules, and
audit effects.

## Candidate and service dependencies

The Proctor workflow requires:

- Testing Center Administrator assignments for Proctor, schedule, room, roster,
  package, and accommodation eligibility;
- a local server with authenticated Candidate discovery and session channels;
- Candidate validation, package receipt, device health, and synchronization events;
- stable Candidate and workstation identities;
- trusted time evidence online and offline;
- durable package, distribution, attendance, incident, session, and audit stores;
  and
- central synchronization with integrity verification and idempotency.

## Audit requirements

The source requires durable events for:

- session opened;
- package downloaded;
- standard or accommodation package uploaded;
- attendance marked Present, locked, unlocked, or corrected;
- assigned exam set updated;
- distribution started, succeeded per Candidate, failed per Candidate, and
  completed;
- exam started or ended;
- emergency stop executed;
- technical incident raised;
- Candidate validation override approved; and
- offline synchronization completed.

Each event must include a stable event ID, actor identity where applicable, session
and correlation identifiers, an authoritative timestamp with clock provenance, the
source-specified fields, and enough before/after data to reconstruct protected state
changes. Audit records must not contain plaintext credentials, package keys,
decrypted questions, Candidate answers, or unnecessary sensitive data.

## Proctor messages

| Event | Type | Required message |
| --- | --- | --- |
| Exam package downloaded | Success | The examination package was downloaded successfully. |
| Standard package uploaded | Success | Standard examination package uploaded successfully. |
| Accommodation package uploaded | Success | Accommodation-specific examination package uploaded successfully. |
| Invalid package | Error | The uploaded examination package is invalid or corrupted. Please upload a valid package. |
| Duplicate package | Warning | This examination package has already been uploaded. |
| Attendance locked | Success | Attendance has been locked successfully. |
| Attendance unlocked | Success | Attendance has been unlocked. You may now update attendance and exam set assignments. |
| Distribution started | Information | Examination package distribution has started. Please wait until all packages have been delivered. |
| Distribution complete | Success | All examination packages have been successfully distributed. You may now start the examination. |
| Distribution failed | Error | One or more student workstations failed to receive the examination package. Please retry the distribution. |
| Start during distribution | Error | The examination cannot be started while package distribution is still in progress. |
| Start before completion | Error | All eligible students must successfully receive their assigned examination package before the examination can begin. |
| Candidate has no package | Error | One or more students do not have an assigned examination package. Please review the attendance list before distribution. |
| Candidate validation failed | Warning | The student failed the pre-examination validation and cannot enter the examination until resolved. |
| Offline mode activated | Warning | Network connectivity was lost. The examination session will continue in Offline Mode. |
| Synchronization completed | Success | Examination data has been successfully synchronized. |
| Synchronization failed | Error | Unable to synchronize examination data. The system will retry automatically when connectivity is restored. |

## Source conflicts requiring resolution

### Accommodation assignment

The narrative says both that the Proctor selects a variant and that the system
automatically assigns it from the Candidate profile. ED-AC-004, ED-AC-011 through
ED-AC-014, and BR-05 consistently require manual selection. The brief does not choose
between them because that changes workflow, authorization, audit, and distribution
behavior.

### Package lifecycle

The source requires package download, upload, selection, and distribution without
defining the authoritative source or transitions. The repository architecture
expects the Proctor to download, validate, and stage a signed encrypted package.
The implementation plan requires an approved lifecycle before file handling or UI
work is specified.

### Late Candidate distribution

At start, Not Checked In Candidates become Late and therefore eligible for package
delivery. The same source blocks start until all currently eligible Candidates have
successful delivery. The post-start eligibility and distribution exception needs an
explicit sequence, retry policy, and audit behavior.

## Security and reliability constraints

- Enforce authorization in domain/application services, not only in the UI.
- Authenticate packages, commands, delivery acknowledgements, Candidate identity,
  and the intended local Proctor endpoint.
- Keep decrypted packages and keys out of logs, audit payloads, notifications, and
  unprotected temporary files.
- Persist attendance and distribution transitions before reporting success.
- Make delivery, retry, acknowledgement, start, override, and synchronization
  operations replay-safe and idempotent.
- Continue local session control during central-network loss without claiming that
  the private examination LAN is available when it is not.
- Test process restart, Candidate reconnect, duplicate messages, checksum failure,
  partial distribution, disk exhaustion, clock skew, and central sync failure.

## Open product decisions

1. Is accommodation assignment manual, automatic, or automatic with confirmed
   Proctor override?
2. What are the authoritative package download, upload, validation, staging,
   replacement, and deletion transitions?
3. Which Candidates count toward the start gate, and how are post-start Late
   Candidates distributed safely?
4. Can attendance be unlocked after distribution begins, and exactly which package
   records become invalid when it is corrected?
5. What trusted timestamp and reconciliation policy governs pure-offline sessions?
6. What local-server protocol, trust bootstrap, certificate lifecycle, retry policy,
   and message versioning are required?
7. Which system receives synchronized attendance, audit, incident, and result data,
   and which identifiers prevent duplicates?

## Planning gate

Do not implement from this brief. Review and resolve the open product decisions,
then produce an approved Proctor implementation plan covering the Proctor UI, local
server, contracts, persistence, security, automated tests, failure injection, and
manual acceptance checks as separate reviewable deliverables.
