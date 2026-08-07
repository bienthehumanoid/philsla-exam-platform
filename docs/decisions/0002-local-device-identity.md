# ADR 0002: Local candidate-device identity

- Status: Accepted for Phase 2
- Date: 2026-08-07

## Context

Future device registration and offline exam permits need a stable cryptographic
identity for each Candidate app installation. The central registration endpoint,
credential issuance, revocation, replacement workflow, and enrollment UI do not
exist yet.

## Decisions

1. The Candidate app lazily creates one ECDSA P-256 key pair and a random device ID
   when a caller first requests the local device identity.
2. The private key, device ID, creation time, and storage-format version are written
   as one JSON value through the operating-system secure store. Keeping one value
   prevents partially initialized identity state.
3. Only the public key, SHA-256 public-key thumbprint, device ID, algorithm, and
   creation time leave the device-identity module.
4. The protected storage format is version 1 and uses strict JSON parsing. Missing,
   corrupt, or unsupported protected state blocks identity use and is not silently
   replaced.
5. Device identity creation is serialized within the application process so
   concurrent callers receive the same identity.
6. ECDSA P-256 identifies the device key type only. Permit/package issuer signing,
   signature encoding, canonical signed bytes, and key-wrapping algorithms remain
   separate decisions.

## Consequences

The device can later prove possession of its private key without placing that key in
SQLite, application settings, logs, contracts, or UI state. Loss or corruption of
the secure-store entry requires an authorized replacement workflow; automatic
regeneration would create an unregistered identity and could bypass operational
controls.

The module does not claim that the device is registered. Registration status,
backend credentials, revocation, and replacement remain deferred until the central
backend interface is available.
