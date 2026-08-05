# US-SR-012 Candidate Task Brief

## Status

Initial brief awaiting review. This document defines Candidate ownership and
planning inputs; it is not an implementation plan.

## Story

- ID: US-SR-012
- Title: Desktop Exam Software Application
- Persona: Student
- Objective: allow a Student to use the Candidate examination software online or
  offline, complete required pre-examination checks, wait for Proctor authorization,
  take the assigned examination, and submit durable responses.
- Relevant issue or ticket: none supplied

The supplied story also contains extensive Proctor behavior. That behavior is out
of this brief's implementation scope and appears only where it defines data or
commands the Candidate must consume.

## Current repository state

The Candidate application already provides:

- a .NET 10 MAUI Blazor Hybrid desktop application for Windows and Mac Catalyst;
- temporary local email-and-password authentication;
- a local readiness and authorization flow backed by development fixtures;
- a four-block single-choice examination workspace with per-block timers;
- crash-safe SQLite WAL answer persistence with append-only integrity-chained
  revisions; and
- local examination recovery, navigation, flagging, explicit submission, and
  timeout submission.

The repository explicitly identifies package signing and encryption, local Proctor
transport, production identity, encrypted-at-rest storage, synchronization,
recording, and production security controls as planned or unresolved. Existing
fixtures must not be represented as satisfying those requirements.

## Candidate-owned scope

1. Authenticate through the supported identity path. Email and password remain the
   temporary implementation while LRN ID integration is externally dependent.
2. Receive and display the Candidate's own attendance and eligibility status.
3. Run identity, camera, microphone, and applicable connectivity checks before
   admission to Standby.
4. Report validation results and block entry to Standby after a failed mandatory
   check unless an authorized Proctor override is received.
5. Receive, validate, persist, and acknowledge only the package assigned to the
   Candidate and workstation.
6. Remain locked in Standby until both assigned-package delivery and the Proctor's
   start command have been validated.
7. Display Late or Absent status and enforce the corresponding eligibility rule.
8. For a Late Candidate, calculate remaining time from the original scheduled end
   time without granting an extension.
9. Continue the active examination through connectivity loss while saving answers
   locally and keeping the timer uninterrupted.
10. Secure final responses and synchronize them without duplicate submission when
    the configured connection becomes available.

## Explicitly out of Candidate scope

- Proctor schedule and room authorization
- roster management and attendance mutation
- attendance lock, unlock, correction, and totals
- accommodation-package selection
- package upload, Proctor download, staging, and distribution orchestration
- distribution dashboards and retry controls
- examination start, end, and emergency-stop authorization
- Proctor audit-log access
- Testing Center Administrator and System Administrator workflows

## Acceptance ownership

### Candidate-primary outcomes

| Source | Candidate outcome |
| --- | --- |
| EO-AC-011 | A validated Candidate remains in Standby until the Proctor starts the session. |
| EO-AC-013 | A Late Candidate receives only the time remaining before the scheduled session end. |
| EO-AC-014 | Failed mandatory validation blocks Standby until resolved or overridden. |
| EO-AC-015 | Responses are continuously saved locally regardless of connectivity. |
| EO-AC-016 | Reconnection synchronizes locally stored responses without duplicate submission. |
| BR-02 | Successful validation enters a locked Standby state. |
| BR-03 | Failed validation reports a Technical Incident boundary and blocks access pending resolution or override. |
| BR-10 | Late admission never extends the scheduled end time. |
| BR-12 | Local response persistence continues throughout the examination. |
| BR-13 | Network loss does not interrupt answering or the timer. |
| BR-20 | Exam access requires successful assigned-package delivery and a valid start command. |

### Candidate-consumed Proctor outcomes

| Source | Candidate dependency |
| --- | --- |
| ED-AC-005 | Present or Late status can make the Candidate eligible for package delivery. |
| ED-AC-006 | Absent or ineligible status prevents package receipt. |
| ED-AC-008 | Candidate delivery acknowledgement contributes to per-workstation status. |
| ED-AC-013 and ED-AC-014 | The Candidate receives only its intended standard or accommodation package. |
| EO-AC-005 through EO-AC-008 | The Candidate consumes Not Checked In, Late, and Absent transitions produced by session control. |
| EO-AC-012 | The timer starts from the authoritative Proctor start event. |

## Required Candidate states and transitions

The implementation plan must define a deterministic state model covering at least:

`SignedOut` -> `Authenticating` -> `Validating` -> `AwaitingPackage` -> `Standby`
-> `InProgress` -> `Submitting` -> `Completed`.

`Late`, `Absent`, `ValidationBlocked`, `Offline`, and `SynchronizationPending` are
not interchangeable. The design must decide whether each is a primary state,
eligibility attribute, connectivity attribute, or submission attribute so that
invalid combinations can be rejected.

## External inputs required by Candidate

- Candidate identity and authentication result
- examination session, workstation, and Candidate identifiers
- attendance and eligibility status with an authoritative version
- scheduled start, scheduled end, grace-period end, and trusted time evidence
- assigned package identity, variant, version, hash, signature, and decryption data
- distribution authorization and delivery correlation identifier
- Proctor start, override, end, or emergency-stop command
- central synchronization endpoint and idempotency identifiers

## Candidate outputs required by other components

- authentication/session presence without reusable plaintext credentials
- validation result per check, timestamp, and failure reason
- technical-incident request or status
- package receipt, integrity-validation result, and durable-storage acknowledgement
- workstation and examination health
- answer revisions and final-submission status
- synchronization progress, integrity result, retry state, and completion result

## Authentication constraint

The story names LRN ID and password, while the repository currently uses a local
email-and-password fixture and the architecture discusses future cloud identity and
offline admission tokens. The approved interpretation is:

- retain email and password as the temporary development path;
- do not present it as production authentication;
- isolate identity contracts so a later LRN provider can replace the temporary
  source without rewriting the examination workflow; and
- do not plan LRN integration until the third-party dependency and methodology are
  confirmed.

## User-facing messages

The source requires these Candidate messages. Copy, timing, severity, and
accessibility behavior must be validated during design rather than inferred from
the message text alone.

| Event | Type | Required message |
| --- | --- | --- |
| Login successful | Success | **Login successful. Performing pre-examination validation...** |
| Validation successful | Success | **Validation completed successfully. Waiting for the Proctor to start the examination.** |
| Validation failed | Error | **Pre-examination validation failed. Please contact the Proctor for assistance.** |
| Package received | Information | **Your examination package has been received successfully. Waiting for the examination to begin.** |
| Waiting | Information | **The examination has not started yet. Please wait for the Proctor.** |
| Exam started | Information | **The examination has started. Good luck!** |
| Late | Warning | **You have been marked Late. Your examination will end at the scheduled session end time.** |
| Absent | Error | **You are not eligible to take this examination. Please contact the Proctor or Testing Center Administrator.** |
| Auto submission | Information | **Time has expired. Your responses have been submitted automatically.** |
| Offline mode | Warning | **Network connection lost. Your responses are being saved locally and will synchronize automatically once the connection is restored.** |

## Security and reliability constraints

- Do not log passwords, package keys, decrypted package content, answers, or other
  sensitive examination data.
- Do not claim encryption, secure distribution, identity verification, or
  successful synchronization until the mechanism is implemented and verified.
- Save locally before acknowledging an answer mutation to the UI or another node.
- Authenticate commands and package metadata; transport encryption alone does not
  establish that the Candidate connected to the intended Proctor.
- Make delivery and submission replay-safe through stable operation identifiers.
- Preserve an encrypted durable copy until verified synchronization completes.
- Treat clock rollback, process restart, database corruption, disk exhaustion,
  duplicate delivery, duplicate start commands, and intermittent LAN connectivity
  as required failure cases during planning.

## Open product decisions

1. Which validation checks are mandatory in online, offline-first, and pure-offline
   modes?
2. What evidence permits a Proctor override, and can any check never be overridden?
3. What trusted time source enforces Late, Absent, grace-period, and scheduled-end
   behavior offline?
4. How does a Candidate that becomes Late after session start obtain its package
   without violating the pre-start distribution gate?
5. What package, transport, identity, and encryption formats are authoritative?
6. Which component owns synchronization to the central server: Candidate, Proctor,
   or both through different records?

## Planning gate

Do not implement from this brief. Review and resolve the open product decisions,
then produce an approved Candidate implementation plan that distinguishes existing
behavior, modifications, new contracts, security work, tests, and manual
acceptance checks.
