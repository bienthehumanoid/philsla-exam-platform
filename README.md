# PhilSLA Exam Platform

PhilSLA Exam Platform is an offline-first examination system for institution-managed
Windows and macOS devices. Before an examination, a proctor downloads a signed and
encrypted package from the cloud. During the examination, candidate devices operate
over a private local network without internet access and synchronize with a local
proctor service. The proctor uploads the completed examination data after internet
access is restored.

## Project status

The repository currently contains only the initial candidate application baseline:

- .NET 10 MAUI Blazor Hybrid application generated from the standard template
- Windows target: `net10.0-windows10.0.19041.0`
- macOS target: `net10.0-maccatalyst`
- verified Windows restore, build, and launch

The template UI is not an examination workflow. Authentication, exam delivery,
offline persistence, proctor services, webcam recording, synchronization, lockdown
integration, and production security controls are planned work.

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

The application is currently unpackaged (`WindowsPackageType=None`).

## Repository structure

```text
.
├── docs/
│   └── architecture.md
├── src/
│   └── PhilSLA.ExamPlatform.Candidate/
├── global.json
└── PhilSLA.ExamPlatform.slnx
```

Only `PhilSLA.ExamPlatform.Candidate` exists today. The architecture plans separate
projects for the proctor UI, local proctor server, core domain and application logic,
contracts, infrastructure, shared UI, and tests. A cloud API will be designed later.

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
