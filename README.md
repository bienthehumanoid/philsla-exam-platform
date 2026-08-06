# PhilSLA Exam Platform

PhilSLA Exam Platform is an offline-first examination system for institution-managed
Windows and macOS devices. Before an examination, a proctor downloads a signed and
encrypted package from the cloud. During the examination, candidate devices operate
over a private local network without internet access and synchronize with a local
proctor service. The proctor uploads the completed examination data after internet
access is restored.

## Project status

The repository currently contains an early candidate application vertical slice and
a proctor workstation attendance vertical slice:

- .NET 10 MAUI Blazor Hybrid candidate application
- Windows target: `net10.0-windows10.0.19041.0`
- Mac Catalyst target: `net10.0-maccatalyst`
- temporary local candidate authentication and readiness flow
- four-block, single-choice examination workspace with per-block timers
- SQLite WAL persistence with append-only, integrity-chained answer revisions
- question flagging, navigation, explicit block submission, timeout submission,
  and local crash recovery
- .NET 10 MAUI Blazor Hybrid proctor workstation with temporary local authentication
- seeded, offline manual attendance for assigned sessions, including Present/Late
  check-in, absence review, in-session proctor corrections before finalization,
  durable SQLite records, audit history, and durable finalization with read-only
  records

The seeded examination and timed proctor authorization are development fixtures.
Mobile QR scanning, signed permits, a LAN attendance endpoint, cross-process
candidate admission, cloud attendance synchronization, rescheduling, and
post-session administrative correction workflows are outside this workstation slice
and remain planned work. Signed exam-package delivery, encrypted local storage,
webcam recording, lockdown integration, and production security controls also
remain planned.

Successful and failed finalization operational-event logging is deferred. The
current slice persists finalization state durably but does not record a separate
finalization operation event.

Android and iOS folders remain from the generated template, but Android and iOS are
not project targets or supported platforms.

See [Architecture](docs/architecture.md) for the approved system design, component
status, security boundaries, and unresolved decisions.

## Prerequisites

For Windows development:

- Windows 10 or Windows 11
- [.NET SDK 10.0.302](https://dotnet.microsoft.com/download/dotnet/10.0), pinned by
  `global.json`
- .NET MAUI workload
- Visual Studio with the .NET Multi-platform App UI development workload, or an
  equivalent command-line build environment with the Windows App SDK requirements

Install or update the MAUI workload if needed:

```powershell
dotnet workload install maui
```

Mac Catalyst builds require macOS, Xcode, and the matching .NET MAUI toolchain.

## Restore, build, and run on Windows

Run these commands from the repository root:

```powershell
dotnet workload restore
dotnet restore .\PhilSLA.ExamPlatform.slnx
dotnet build .\src\PhilSLA.ExamPlatform.Candidate\PhilSLA.ExamPlatform.Candidate.csproj -f net10.0-windows10.0.19041.0
dotnet run --project .\src\PhilSLA.ExamPlatform.Candidate\PhilSLA.ExamPlatform.Candidate.csproj -f net10.0-windows10.0.19041.0
```

Run the Proctor application with:

```powershell
dotnet build .\src\PhilSLA.ExamPlatform.Proctor\PhilSLA.ExamPlatform.Proctor.csproj -f net10.0-windows10.0.19041.0
dotnet run --project .\src\PhilSLA.ExamPlatform.Proctor\PhilSLA.ExamPlatform.Proctor.csproj -f net10.0-windows10.0.19041.0
```

The application is currently unpackaged (`WindowsPackageType=None`).

## Temporary MVP logins

Until cloud authentication is available, the Candidate application uses an
isolated SQLite-backed presentation account:

```text
Email: candidate@example.test
Password: DemoExam!2026
```

Only a salted password hash is stored in the local database. The login page depends
on an authentication-service interface so the temporary provider can be replaced by
the cloud API without changing the candidate exam workflow.

The Proctor application uses the same temporary SQLite-backed pattern with an
isolated presentation account:

```text
Email: proctor@example.test
Password: DemoProctor!2026
```

The authenticated candidate and proctor sessions remain in memory and do not survive
an application restart.

## Repository structure

```text
.
├── docs/
│   └── architecture.md
├── src/
│   ├── PhilSLA.ExamPlatform.Candidate/
│   ├── PhilSLA.ExamPlatform.Core/
│   ├── PhilSLA.ExamPlatform.Infrastructure/
│   └── PhilSLA.ExamPlatform.Proctor/
├── tests/
│   └── PhilSLA.ExamPlatform.Candidate.Tests/
├── global.json
└── PhilSLA.ExamPlatform.slnx
```

`PhilSLA.ExamPlatform.Core` contains the first examination and attendance rules and
application services. `PhilSLA.ExamPlatform.Infrastructure` contains SQLite stores
for examination attempts and workstation attendance records.
`PhilSLA.ExamPlatform.Proctor` implements the seeded offline manual-attendance
workstation slice; it is not a local attendance service. The architecture still
plans separate local-server, contracts, and shared-UI projects. A cloud API will be
designed later.

## Security and repository hygiene

Do not commit:

- application or SQLite database files, journals, WAL files, or exam answer data
- webcam recordings or recording segments
- exam packages or exported examination results
- passwords, TOTP seeds, tokens, encryption keys, or other secrets
- signing certificates, private keys, provisioning profiles, or production
  configuration

Use synthetic data and development-only credentials for local work. The planned
encryption, signing, secure key storage, lockdown, and integrity controls are not yet
implemented; do not use the current template application for a real examination.
