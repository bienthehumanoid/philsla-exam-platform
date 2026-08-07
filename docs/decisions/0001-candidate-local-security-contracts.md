# ADR 0001: Candidate local-security contracts

- Status: Accepted for Phase 1
- Date: 2026-08-07

## Context

The candidate application currently uses development fixtures for identity,
authorization, and examination content. Device registration, signed offline
admission, encrypted package delivery, durable synchronization, idempotent final
submission, and verifiable submission receipts need stable messages before their
implementations are added.

The central backend and the Proctor LAN server do not exist in this repository yet.
Phase 1 therefore defines their shared messages without pretending that either
integration is complete.

## Decisions

1. Protocol version 1 is the only supported version. Every top-level message carries
   `protocolVersion`; unsupported versions fail before domain processing.
2. JSON is UTF-8, camel case, case-sensitive, and rejects unknown properties,
   missing constructor properties, integer enum values, and malformed payloads.
3. Device identity is based on an application-generated asymmetric key pair. A
   backend-issued device credential identifies the public key. Hardware fingerprints
   are readiness evidence only, not authentication secrets.
4. Exam admission uses a signed permit bound to candidate, device, examination,
   package, and Proctor session. Reusable password hashes are not distributed for
   offline admission.
5. The permit carries UTC validity bounds and the scheduled examination end. Permit
   issuance, revocation, emergency admission, and trusted Proctor time are deferred.
6. Exam packages use a signed manifest, an authenticated-encryption identifier, a
   wrapped content key, a payload hash, and a payload length. No cryptographic
   algorithm is implemented or selected in Phase 1.
7. Synchronization messages carry stable IDs, attempt/candidate scope, monotonic
   sequence numbers, content type, payload hash, and creation time. Ordering,
   retries, and acknowledgements are deferred to the outbox module.
8. Final submission has a client-generated stable submission ID and a signed final
   answer-chain hash. A Proctor receipt binds that submission to the hash persisted
   by the Proctor.
9. Contract types contain wire data only. Signature validation, clock policy,
   cryptography, persistence, transport, and business validation belong behind
   separate module interfaces in later phases.

## Ownership

- Central backend: device credential and permit issuance, signing-key lifecycle,
  revocation publication, and institution policy.
- Proctor LAN server: session admission, authoritative examination time, package
  transfer, durable synchronization acknowledgements, submission persistence, and
  receipt issuance.
- Candidate application: protected device private key, offline validation, encrypted
  package storage, local attempt authority while disconnected, retry, and receipt
  verification.

## Deferred decisions

- Signing and key-wrapping algorithms and canonical signed-byte representation
- Device enrollment, approval, replacement, and revocation workflows
- Trusted offline clock and emergency-admission policy
- Package chunking and transfer protocol
- Synchronization acknowledgement and conflict rules
- Retention and secure-deletion periods

These decisions require threat modeling and agreement with the Django and Proctor
implementations. Test-only algorithm names in contract tests are not production
choices.

## Consequences

The Candidate and Proctor implementations can build against one versioned seam and
use in-memory adapters until networking exists. Strict parsing makes incompatible
changes fail visibly, but additive fields require a new protocol version rather than
being silently ignored. This is intentional for exam-security messages.
