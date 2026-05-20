# Krypteia Threat Model

## About this document

This is a **threat model** for the Krypteia encryption library: a
structured description of what Krypteia is designed to protect, the
attackers and scenarios it considers, what it defends against, and —
importantly — what it does **not** defend against.

This document is part of Krypteia's **audit-readiness** materials. It is
**not** a security audit. It was prepared by the library's authors and
therefore cannot serve as an independent assessment. Its purpose is to
give an external auditing firm a clear starting point, and to let
adopters make informed decisions about whether Krypteia fits their
security requirements.

A real security audit must be performed by an independent third party.
See `AUDIT-SCOPE.md`.

## What Krypteia is

Krypteia is a .NET library providing zero-knowledge encryption of
per-user data. Each user has an asymmetric key pair. Data is encrypted
to a user's public key; only the holder of the corresponding private
key can decrypt it. Private keys are never stored in usable form on the
server — they are held only as backups wrapped (encrypted) under a
master key, and they are returned to the user at generation time for the
user to store themselves.

The design intent: an attacker who compromises the application database
— even a database administrator with full read access — cannot read
user data, because the database contains only ciphertext and public
keys.

## Assets being protected

In priority order:

1. **User plaintext data** — the information users encrypt with Krypteia.
   The primary asset. Everything else matters only insofar as it
   protects this.
2. **User private keys** — the asymmetric private keys that can decrypt
   user data. Compromise of a private key compromises that user's data.
3. **The master key** — the key that wraps private-key backups.
   Compromise of the master key exposes every private-key backup it
   protects.
4. **Reset tokens** — short-lived secrets that authorize a key reset.
   Compromise of a valid token allows an attacker to run the reset flow
   as that user.
5. **Audit records** — the integrity of the audit log. Not
   confidential, but their completeness and accuracy matter for
   detection and compliance.

## Trust assumptions

Krypteia's guarantees hold **only if** these assumptions hold. They are
stated plainly because every one of them is a potential point of
failure, and adopters must satisfy themselves that each holds in their
deployment.

1. **The .NET runtime and OS cryptographic primitives are sound.**
   Krypteia uses the platform's RSA, AES-GCM, SHA-256, and
   cryptographic RNG. It does not implement its own primitives. If the
   platform's crypto is compromised, so is Krypteia.
2. **The master key is stored securely and access to it is
   controlled.** Krypteia ships a file-based master key provider for
   development. Production deployments are expected to use a hardware
   security module or managed key service. The library cannot enforce
   this; it is the operator's responsibility.
3. **The server process is not compromised at runtime.** Krypteia
   protects data at rest in the database. It cannot protect plaintext
   that exists in server memory during an operation. An attacker with
   live code execution on the server is outside Krypteia's protection.
4. **Users protect their own private keys.** Once a private key is
   delivered to a user, its safekeeping is the user's responsibility.
   Krypteia cannot protect a key the user has mishandled.
5. **The email channel used for key reset is reasonably trustworthy.**
   The reset flow delivers a one-time token by email. If an attacker
   controls a user's email account, they can complete a reset as that
   user. This is an accepted limitation, discussed below.
6. **The consuming application is not itself hostile.** Krypteia is a
   library; it runs inside an application that the operator controls.
   A malicious application can misuse any library.

## Attacker scenarios

Each scenario states the attacker's capability, what Krypteia does to
limit the damage, and what remains exposed.

### Scenario 1 — Database compromise (read access)

**Capability:** The attacker can read the entire application database.
This models a leaked backup, SQL injection with read access, or a
malicious or compromised database administrator.

**What Krypteia does:** The database contains only public keys,
master-key-wrapped private-key backups, ciphertext, reset-token hashes,
and audit records. No plaintext user data and no usable private keys
are present.

**What remains exposed:** Nothing of the primary asset. The attacker
sees that users exist, how many keys exist, timestamps, and audit
metadata. They cannot decrypt user data. **This is the central scenario
Krypteia is designed to defeat, and it does.**

### Scenario 2 — Database compromise + master key

**Capability:** The attacker has the database (Scenario 1) **and** the
master key — for example, by compromising the key vault or the
file-based master key in a development-grade deployment.

**What Krypteia does:** Limited mitigation. The master key wraps the
private-key *backups*. With both, the attacker can unwrap the
private-key backups and then decrypt user data.

**What remains exposed:** User data is exposed for every user whose
private-key backup is in the database. This is a severe compromise.
The defense is **preventive, not built into the library**: the master
key must be held in a system separate from the database, with its own
access controls and audit trail, so that one breach does not yield
both. Krypteia's pluggable master key provider exists precisely so the
master key can live in an HSM or managed key service. **If an operator
runs the file-based provider in production, this scenario becomes much
more likely — that configuration is for development only.**

### Scenario 3 — Database + master key + user email access

**Capability:** Everything in Scenario 2, plus the ability to receive
email for a target user.

**What Krypteia does:** This is the accepted worst case. The attacker
can initiate a key reset for the user, receive the token, complete the
reset, and obtain a new key pair under their control.

**What remains exposed:** Full compromise of the targeted user. Krypteia
does not defend against this combination. The mitigations are
operational: protect the master key (Scenario 2), and ensure the audit
log is monitored — a reset is always audited, so this attack is
**detectable after the fact** even though it is not prevented.

### Scenario 4 — User device compromise

**Capability:** The attacker compromises a user's own device, where
that user stores their private key.

**What Krypteia does:** Nothing — and cannot. Once a private key is in
the user's possession, its protection is the user's responsibility.

**What remains exposed:** That user's data. This is outside Krypteia's
boundary by design. It is listed here for completeness, not because
Krypteia has a mitigation.

### Scenario 5 — The data re-encryption window

**Capability:** This is not an external attacker but a structural
property worth stating explicitly. The optional
`IDataReencryptionService` hook lets an application re-encrypt a user's
data during a key reset. To do so, the application must — for the
duration of that operation — be able to access the user's data in a
form it can re-encrypt.

**What Krypteia does:** The re-encryption hook is optional and
consumer-implemented. Krypteia does not ship one. When it runs, the old
private-key backup is unwrapped so the old data can be read and
re-encrypted under the new key.

**What remains exposed:** During a reset with re-encryption,
application code can access the affected user's data. This is inherent
to the feature — re-encrypting data requires reading it. The
mitigations: the hook is optional (an application that does not need it
does not implement it), every reset is audited, and master-key access
should be limited and rotated. Operators who consider this window
unacceptable should not implement `IDataReencryptionService`; the
trade-off is that, without it, data encrypted under an old key becomes
unreadable after a reset.

### Scenario 6 — Server runtime compromise

**Capability:** The attacker achieves code execution inside the running
server process.

**What Krypteia does:** Nothing — this is outside scope. Krypteia
protects data at rest. An attacker running code in the server process
can observe plaintext as it is processed, regardless of any at-rest
encryption.

**What remains exposed:** Any data processed while the attacker has
control. Defense is the operator's: standard application and host
security, least privilege, monitoring. Krypteia is an at-rest
protection, not a runtime one.

## Cryptographic attack surface

These are addressed in detail in `CRYPTOGRAPHIC-INVENTORY.md`; summarized
here for completeness.

- **Padding-oracle attacks.** Decryption failures return a single
  generic error. Krypteia does not distinguish "wrong key" from
  "malformed ciphertext" in error messages, denying an attacker the
  signal such attacks depend on.
- **Token guessing.** Reset tokens are 32 bytes from a cryptographic
  RNG. The search space is far too large to brute-force. Tokens are
  also single-use, time-limited (15 minutes), and rate-limited (3 per
  user per hour).
- **Token theft from the database.** Only the SHA-256 hash of a reset
  token is stored. An attacker reading the database cannot recover the
  token needed to complete a reset.
- **Timing side channels.** A known performance-sensitive test for
  decryption timing exists but is skipped by default. Constant-time
  behavior of the underlying platform primitives is assumed, not
  independently verified — this is explicitly an item for external
  audit.

## What this threat model does not cover

- **Implementation correctness.** This document describes intended
  design. Whether the code correctly implements it is exactly what an
  independent audit must determine.
- **Denial of service.** Krypteia does not specifically defend against
  resource-exhaustion attacks beyond the reset-flow rate limit.
- **Supply chain.** The security of Krypteia's own dependencies, build
  pipeline, and distribution is not analyzed here.
- **Post-quantum threats.** Krypteia uses RSA-2048, which is not
  quantum-resistant. See `CRYPTOGRAPHIC-INVENTORY.md` for discussion.
- **Operational security** of the deployment — host hardening, network
  security, personnel — is the operator's responsibility.

## Summary

Krypteia's core guarantee is narrow and specific: **an attacker who
compromises only the database cannot read user data.** That guarantee is
real and is the reason the library exists.

Every guarantee weakens as an attacker gains more — the master key, a
user's email, the server process, a user's device. None of those
additional compromises is something a cryptographic library can prevent;
they are addressed, if at all, by operational controls the operator must
provide.

Adopters should treat Krypteia as one component of a security posture,
not as a security posture in itself.