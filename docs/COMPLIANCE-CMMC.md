# CMMC Level 2 — Control Mapping

## About this document

This document maps the capabilities of the Krypteia encryption library
to the **CMMC (Cybersecurity Maturity Model Certification) Level 2**
practices that Krypteia is relevant to.

Read the following before the mapping. It is not boilerplate; it defines
what this document does and does not claim.

### Krypteia is "CMMC Level 2 compatible," not "compliant" or "certified"

- **Compatible** means: Krypteia provides cryptographic and audit
  capabilities that can *support* an organization's effort to satisfy
  certain CMMC Level 2 practices.
- It does **not** mean Krypteia is certified, compliant, or assessed.
  CMMC certification is awarded to an **organization** for a **complete
  system**, following assessment by an authorized third party. A
  software library cannot be "CMMC certified." Any claim to the
  contrary — by this project or anyone else — would be incorrect.

### A library cannot make a system compliant

CMMC compliance is a property of an entire information system and the
organization operating it: its people, processes, configuration,
physical environment, and technology together. Krypteia is one
technical component. It can contribute to *some* practices in *some*
domains. The majority of CMMC Level 2 — and the ultimate responsibility
for all of it — lies with the operator.

### How to read each practice entry

For each relevant practice this document states:

- **Practice** — the CMMC Level 2 practice identifier and a short
  description.
- **What Krypteia provides** — the specific capability, if any, that
  supports this practice.
- **What Krypteia does NOT provide** — the gap the operator must close.
  This half is deliberately given equal weight. A practice is never
  "handled by Krypteia"; at most it is *partially supported*.

### Status of this document

This document is part of Krypteia's audit-readiness materials. It was
written by the library's authors and reflects the library's *intended*
capabilities. It has not been validated by a CMMC assessor or by an
independent security audit. An organization pursuing CMMC should treat
it as a starting point for discussion with their assessor, not as
evidence of compliance. See `AUDIT-SCOPE.md`.

## Scope of this mapping

CMMC Level 2 comprises 110 practices across 14 domains. Krypteia, as a
cryptographic library, is relevant to a subset of practices in **four**
domains:

- **AC — Access Control**
- **AU — Audit and Accountability**
- **IA — Identification and Authentication**
- **SC — System and Communications Protection**

The other ten domains — Awareness and Training, Configuration
Management, Incident Response, Maintenance, Media Protection, Personnel
Security, Physical Protection, Risk Assessment, Security Assessment, and
System and Information Integrity — concern processes, people, and
operational controls that a software library does not address. They are
**entirely the operator's responsibility** and are not mapped here.

Even within the four domains above, Krypteia touches only the practices
specifically concerned with encryption, key management, and audit
logging. Practice identifiers below follow the CMMC Level 2 / NIST
SP 800-171 numbering. Organizations should confirm identifiers against
the current official CMMC documentation, as numbering and wording are
maintained by the CMMC program and may be revised.

## AC — Access Control

### AC.L2-3.1.1 — Limit system access to authorized users

**What Krypteia provides.** Krypteia's model ties the ability to decrypt
data to possession of a user's private key. Data encrypted to a user
can only be decrypted by the holder of the corresponding private key.
This is a cryptographic access-control mechanism for the *data itself*,
independent of any system-level access control.

**What Krypteia does NOT provide.** Krypteia does not authenticate users
to the system, manage sessions, enforce login, or decide who is an
"authorized user." It assumes the consuming application has already
established identity. System access control is the operator's
responsibility.

### AC.L2-3.1.3 — Control the flow of CUI

**What Krypteia provides.** Because data is held as ciphertext and only
decryptable with the appropriate private key, Krypteia constrains where
*usable* data can flow: ciphertext can be stored or moved without
exposing content. This supports controlling the flow of controlled
information.

**What Krypteia does NOT provide.** Krypteia does not implement flow-
control policy, data labeling, or boundary enforcement. It does not know
what data is CUI. Defining and enforcing flow-control rules is the
operator's responsibility.

## AU — Audit and Accountability

### AU.L2-3.3.1 — Create and retain system audit logs

**What Krypteia provides.** Krypteia generates structured audit records
for cryptographic operations — recording the operation type, the user,
a timestamp, an outcome, and related metadata. These records support
the creation of audit logs for the cryptographic activity within an
application.

**What Krypteia does NOT provide.** Krypteia does not provide
system-wide logging, log *retention*, log storage durability, or
log-management infrastructure. It produces records; the operator must
collect, retain, and protect them for the required period. Audit
coverage of non-Krypteia parts of the system is entirely the operator's
responsibility.

### AU.L2-3.3.2 — Ensure actions can be traced to individual users

**What Krypteia provides.** Krypteia's audit records associate each
cryptographic operation with a user identifier, supporting traceability
of those specific operations to an individual.

**What Krypteia does NOT provide.** The traceability is only as good as
the user identity the application supplies to Krypteia. Krypteia does
not establish or verify that identity. End-to-end accountability across
the system is the operator's responsibility.

### AU.L2-3.3.4 — Alert on audit logging process failures

**What Krypteia provides.** Krypteia surfaces failures in its own
operations (for example, an audit-relevant failure in the reset flow is
itself recorded).

**What Krypteia does NOT provide.** Krypteia does not implement alerting,
monitoring, or notification on audit-process failure. Detecting that
logging has failed and alerting personnel is an operational capability
the operator must build around the records Krypteia emits.

### AU.L2-3.3.8 — Protect audit information from unauthorized access or modification

**What Krypteia provides.** Krypteia produces audit records as data;
it does not itself expose an interface for altering past records.

**What Krypteia does NOT provide.** Krypteia does not encrypt, sign, or
tamper-protect audit records, and does not control access to wherever
they are stored. Protecting audit information — access control,
integrity protection, write-once storage if required — is the
operator's responsibility. **An auditor or assessor should note this
explicitly: Krypteia's audit records are not integrity-protected by the
library.**

## IA — Identification and Authentication

### IA.L2-3.5.1 — Identify system users and processes

**What Krypteia provides.** Krypteia operates on a per-user basis: each
user has a distinct key pair, and operations are performed in the
context of a user identifier.

**What Krypteia does NOT provide.** Krypteia consumes a user identity
supplied by the application; it does not establish, issue, or manage
system identities. Identity management is the operator's responsibility.

### IA.L2-3.5.2 — Authenticate users and devices

**What Krypteia provides.** Possession of a private key functions as a
cryptographic proof for decryption — a user without the key cannot
decrypt. In that narrow sense, key possession authenticates the ability
to access data.

**What Krypteia does NOT provide.** Krypteia is **not an authentication
system.** It does not perform login, verify credentials, authenticate
devices, or establish sessions. It does not replace the operator's
authentication mechanism. The reset flow's email-token step is an
account-recovery mechanism for key material, not a user-authentication
mechanism. Do not represent Krypteia as satisfying this practice;
it only contributes a narrow cryptographic element.

### IA.L2-3.5.10 — Store and transmit only cryptographically-protected passwords

**What Krypteia provides.** Krypteia does not handle passwords — but the
*principle* of this practice is reflected in how Krypteia handles reset
tokens: tokens are stored only as SHA-256 hashes, never in usable form.
This is the same "never store the secret in recoverable form" pattern.

**What Krypteia does NOT provide.** Krypteia has no role in the
operator's actual password storage or transmission. This practice is
satisfied by the operator's authentication system, not by Krypteia.
The mapping here is illustrative of shared principle only.

## SC — System and Communications Protection

### SC.L2-3.13.8 — Implement cryptographic mechanisms to prevent unauthorized disclosure of CUI during transmission

**What Krypteia provides.** If an adopter performs encryption before
data is transmitted (client-side encryption — see `TRUST-BOUNDARIES.md`
boundary B1), the data is in ciphertext form in transit, supporting
this practice.

**What Krypteia does NOT provide.** Krypteia does not provide transport-
layer security (TLS). It does not secure network channels. Whether data
is protected in transit depends on the operator deploying TLS and on
the adopter's choice of client-side vs server-side encryption. Krypteia
contributes a data-level mechanism; transmission security overall is
the operator's responsibility.

### SC.L2-3.13.10 — Establish and manage cryptographic keys

**What Krypteia provides.** This is the practice Krypteia most directly
supports. Krypteia implements key generation, the storage of private
keys as wrapped (encrypted) backups, key versioning, a key-reset flow,
and a pluggable master key provider model intended to allow the master
key to reside in a hardware security module or managed key service.

**What Krypteia does NOT provide.** Krypteia does not itself provide an
HSM or managed key service — it provides the *interface* to plug one in.
The development-grade file-based master key provider is **not** suitable
for production and does not satisfy this practice on its own. Secure
generation, storage, and lifecycle of the *master key* depend on the
operator's chosen key-management infrastructure. Key custody, rotation
policy, and recovery procedures are operator responsibilities. See
`TRUST-BOUNDARIES.md` boundary B3.

### SC.L2-3.13.11 — Employ FIPS-validated cryptography when used to protect the confidentiality of CUI

**What Krypteia provides.** Krypteia uses standard algorithms (RSA-OAEP,
AES-256-GCM, SHA-256) via the underlying .NET platform's cryptographic
implementations.

**What Krypteia does NOT provide.** **This practice requires particular
attention and Krypteia does not, by itself, satisfy it.** FIPS
validation is a property of a specific cryptographic *module*
implementation operating in a specific validated mode — not a property
of an algorithm name and not a property of Krypteia. Whether the
cryptography is FIPS-validated depends on the platform, operating
system, and configuration the operator runs Krypteia on, and on
operating that platform in its validated mode. An organization with a
FIPS requirement for CUI must verify the validation status of their
deployment platform independently. Krypteia makes no FIPS validation
claim.

### SC.L2-3.13.16 — Protect the confidentiality of CUI at rest

**What Krypteia provides.** This, together with SC.L2-3.13.10, is
Krypteia's central contribution. Data encrypted with Krypteia is held as
ciphertext at rest; an attacker with database access alone cannot read
it (see `THREAT-MODEL.md`, Scenario 1). This directly supports
protecting the confidentiality of controlled information at rest.

**What Krypteia does NOT provide.** Krypteia protects data that the
application chooses to encrypt with it. It does not automatically
discover or encrypt all CUI in a system, does not protect data in other
stores, and does not protect data while it is in use in server memory.
Comprehensive at-rest protection across the system is the operator's
responsibility; Krypteia is a mechanism the operator applies.

## Summary table

| Practice | Krypteia's role |
|---|---|
| AC.L2-3.1.1 — Limit access to authorized users | Partial — cryptographic data access only |
| AC.L2-3.1.3 — Control flow of CUI | Partial — keeps data as ciphertext |
| AU.L2-3.3.1 — Create and retain audit logs | Partial — emits records; no retention |
| AU.L2-3.3.2 — Trace actions to users | Partial — for crypto operations only |
| AU.L2-3.3.4 — Alert on logging failures | Minimal — no alerting |
| AU.L2-3.3.8 — Protect audit information | Minimal — records not integrity-protected |
| IA.L2-3.5.1 — Identify users | Partial — consumes supplied identity |
| IA.L2-3.5.2 — Authenticate users | Minimal — not an auth system |
| IA.L2-3.5.10 — Cryptographically protect passwords | Illustrative — shared principle only |
| SC.L2-3.13.8 — Protect CUI in transmission | Partial — data-level only, no TLS |
| SC.L2-3.13.10 — Establish and manage keys | Substantial — core capability |
| SC.L2-3.13.16 — Protect CUI at rest | Substantial — core capability |
| SC.L2-3.13.11 — FIPS-validated cryptography | Not satisfied by Krypteia — platform-dependent |

"Substantial" is the strongest word used in this table, and it appears
only twice. Krypteia's honest contribution to CMMC Level 2 is real but
narrow: it is a strong mechanism for **key management** and **at-rest
confidentiality**, and a partial or minor contributor elsewhere.

## What an organization pursuing CMMC must still do

Adopting Krypteia does not reduce the operator's obligations. At minimum,
an organization pursuing CMMC Level 2 must still, independently of
Krypteia:

- Address the ten CMMC domains Krypteia does not touch at all.
- Provide system-level access control and authentication.
- Provide and securely operate the master key infrastructure (HSM or
  managed key service) — **not** the development file-based provider.
- Verify FIPS validation status of their cryptographic platform if they
  have a FIPS requirement.
- Collect, retain, protect, and monitor audit logs, including
  integrity-protecting Krypteia's audit records.
- Engage an authorized CMMC assessor for actual assessment.
- Consider an independent security audit of Krypteia itself (see
  `AUDIT-SCOPE.md`), since this mapping reflects intended capability,
  not independently verified fact.

## Summary

Krypteia can be a useful component in a system pursuing CMMC Level 2,
contributing most directly to cryptographic key management
(SC.L2-3.13.10) and protection of CUI at rest (SC.L2-3.13.16), and
partially to several access-control, audit, and identification
practices.

It contributes to roughly a dozen of the 110 CMMC Level 2 practices, and
fully satisfies none of them on its own. CMMC certification remains a
property of the whole system and organization, established only by
formal assessment. Krypteia is "CMMC Level 2 compatible" in the specific,
limited sense defined at the top of this document — and in no broader
sense.