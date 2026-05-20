# Krypteia Security Audit — Scoping Document

## About this document

This document defines the scope for an **independent third-party
security audit** of the Krypteia encryption library. It is intended to
be given to a prospective auditing firm as the starting point for a
statement of work.

It is itself part of Krypteia's **audit-readiness** materials, prepared
by the library's authors. It is **not** an audit and does not substitute
for one. Its purpose is to make a real audit faster, cheaper, and
better-targeted by giving the auditor a clear scope, a map of the
codebase, and a prioritized list of concerns the authors already
consider most important.

## Why an independent audit is necessary

Krypteia is a cryptographic library. Its other audit-readiness documents
— `THREAT-MODEL.md`, `CRYPTOGRAPHIC-INVENTORY.md`, `TRUST-BOUNDARIES.md`
— describe the library's *intended* design and the authors' reasoning.

None of those documents can establish that the **implementation is
correct**, because they were written by the same people who wrote the
code. A developer cannot independently audit their own work: they share
the blind spots. The value of an audit is precisely its independence.

This is stated plainly so there is no ambiguity: the authors of Krypteia
have **not** audited it and cannot. An organization adopting Krypteia
for security-sensitive use — and certainly any organization relying on
it as part of a CMMC Level 2 posture — should ensure an independent
audit has been performed, or commission one.

## What the audit should cover (in scope)

### 1. Cryptographic implementation correctness

The core of the audit. The auditor should verify that the implementation
correctly realizes the design in `CRYPTOGRAPHIC-INVENTORY.md`:

- **RSA-OAEP encryption** — correct use of OAEP padding with SHA-256;
  correct key sizes; correct handling of the platform RSA API.
- **AES-256-GCM wrapping** — correct construction of the wire format
  (`[version][nonce][tag][ciphertext]`); correct use of the GCM API;
  correct tag verification on unwrap.
- **GCM nonce uniqueness** — this is called out separately because it
  is the single most security-critical implementation property in the
  library. The auditor should trace every path that produces a GCM
  nonce and confirm a fresh, cryptographically random nonce is used for
  every wrap operation, with no reuse possible under any code path,
  error path, or retry.
- **SHA-256 token hashing** — confirm tokens are hashed before storage
  and that no path stores a token in usable form.
- **Random number generation** — confirm all security-sensitive random
  values come from the platform cryptographic RNG and that no
  non-cryptographic source is used anywhere in a security-sensitive
  path.

### 2. Key management and the reset flow

- **Private-key handling** — confirm private keys are never persisted
  in usable (unwrapped) form; confirm the generation-time delivery to
  the user is the only point a usable private key leaves the library's
  control.
- **The key-reset flow end to end** — token generation, hashing,
  storage, expiry enforcement, single-use enforcement, rate-limit
  enforcement. Confirm each property holds and cannot be bypassed
  (e.g. by races, replay, or clock manipulation).
- **The `IDataReencryptionService` window** — review how Krypteia opens
  and closes the re-encryption window; confirm the old private-key
  backup is handled correctly and not left exposed beyond the operation.

### 3. Error handling and side channels

- **Generic error handling** — confirm that no code path (exception
  messages, exception types, log output, audit records, or API
  responses) leaks the specific cause of a cryptographic failure, as
  required by `CRYPTOGRAPHIC-INVENTORY.md` section 6.
- **Timing side channels** — the test suite contains a decryption-
  timing test that is skipped by default. The auditor should assess
  whether observable timing differences exist between success and
  failure paths in decryption and unwrap, and whether constant-time
  behavior of the platform primitives can be relied upon in the
  deployment environments of interest.

### 4. Trust-boundary enforcement

Using `TRUST-BOUNDARIES.md` as a map, confirm that each item crosses
each boundary only in the form claimed — in particular boundary B2
(nothing sensitive reaches the database except as ciphertext or hash)
and B3 (the master key boundary).

### 5. API safety and misuse resistance

- Whether the public API makes insecure use difficult — for example,
  whether an adopter can accidentally attempt to RSA-encrypt a payload
  too large for the key, or accidentally select the development-only
  file-based master key provider or console email transport in a
  production configuration.
- Whether defaults are safe defaults.

### 6. Dependency review

A review of Krypteia's third-party dependencies for known
vulnerabilities and for whether each is appropriate and current.

## What the audit need not cover (out of scope)

Stating this keeps the engagement focused and the cost predictable.

- **The consuming application.** Krypteia is a library. An audit *of
  Krypteia* does not extend to any particular application built on it.
  In particular, a consumer's own `IDataReencryptionService`
  implementation (trust boundary B5) is the consumer's code and is out
  of scope for a library audit — though it is firmly in scope for an
  audit of a specific *deployment*.
- **Operational deployment** — host hardening, network security, the
  configuration and security of the chosen master key service or email
  transport, personnel and physical security. These are the operator's
  responsibility.
- **The platform.** Krypteia relies on the .NET runtime's cryptographic
  primitives. Auditing those primitives themselves is out of scope; the
  audit may, however, comment on whether Krypteia uses them correctly.
- **Denial of service** beyond the existing reset-flow rate limit,
  unless the engaging party specifically requests it.
- **Formal cryptographic proofs.** The audit is an implementation and
  design review, not a formal verification effort, unless specifically
  commissioned as the latter.

## Prioritized concerns — where to focus first

If audit effort is limited, the authors consider these the highest-value
targets, in order:

1. **GCM nonce uniqueness** (in-scope item 1). A nonce-reuse bug would
   be critical and is the kind of defect that is easy to introduce and
   easy to miss.
2. **The key-reset flow** (in-scope item 2). It is the most complex
   stateful part of the library and has the most enforcement properties
   that must all hold simultaneously.
3. **Generic error handling and timing** (in-scope item 3). Side
   channels are subtle and exactly the class of issue an independent
   reviewer is more likely to catch than the original author.
4. **Private-key handling** (in-scope item 2). Confirming a usable
   private key is never persisted is the foundation of the whole
   threat model.

This prioritization reflects the authors' view of where risk
concentrates. An auditor should treat it as a starting hypothesis, not
a constraint.

## Materials available to the auditor

To make the engagement efficient, the following are available:

- Full source code and commit history (public repository).
- This audit-readiness document set: `THREAT-MODEL.md`,
  `CRYPTOGRAPHIC-INVENTORY.md`, `TRUST-BOUNDARIES.md`, this document,
  and `COMPLIANCE-CMMC.md`.
- The existing automated test suite, including unit, integration, and
  security-focused tests.
- A runnable sample application demonstrating the library in use.

The auditor should be aware that the test suite and these documents were
produced by the authors and are themselves part of what an audit might
reasonably scrutinize — for example, whether the tests actually exercise
the properties they claim to.

## Choosing an auditor

The engaging organization should select a firm with demonstrable
experience in **applied cryptography and secure code review**, not
general IT security or penetration testing alone. Cryptographic
implementation review is a specialized skill.

Firms that perform this class of work include — listed as well-known
examples, not as endorsements or recommendations — Trail of Bits, NCC
Group, Cure53, and Include Security, among others. The engaging
organization should evaluate current candidates on their own merits:
relevant published reports, cryptography-specific expertise, .NET
familiarity, availability, and cost.

## Indicative budget and effort

Costs vary substantially by firm, depth, and timing, and the figures
below are rough planning estimates only, not quotes:

- A focused cryptographic and key-management review of a library of
  Krypteia's size is commonly in the region of **USD 15,000–50,000**.
- The lower end corresponds to a tightly scoped review concentrating on
  the prioritized concerns above; the higher end to a more exhaustive
  review including extended side-channel analysis and a full dependency
  and API-misuse assessment.
- Calendar time is typically a few weeks from engagement to report,
  depending on firm availability.

The engaging organization should obtain current quotes from at least
two firms.

## After the audit

An audit produces findings, not a permanent guarantee. To be meaningful:

- Findings should be remediated and the fixes verified — ideally by the
  same firm in a re-test.
- The audit is valid for the version audited. Material changes to
  cryptographic code after the audit warrant re-review.
- The audit report's status (firm, date, version audited, findings
  summary, remediation status) should be recorded honestly in project
  documentation so adopters can see exactly what has and has not been
  independently reviewed.

Until an independent audit has been completed and its findings
remediated, Krypteia's documentation should not imply the library has
been independently validated — because it has not.

## Summary

Krypteia is ready to be audited: the design is documented, the code and
tests are available, and the concerns the authors consider most
important are identified above. What remains is the part the authors
cannot do themselves — an independent firm verifying that the
implementation is as sound as the design intends.

Commissioning that audit is a decision for the adopting or sponsoring
organization. This document exists to make it straightforward.