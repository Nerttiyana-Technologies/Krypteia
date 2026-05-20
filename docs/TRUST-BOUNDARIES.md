# Krypteia Trust Boundaries

## About this document

This document maps the **trust boundaries** in and around Krypteia: the
points where data or control passes between a more-trusted zone and a
less-trusted one. For each boundary it states what crosses, in what
form (plaintext, ciphertext, or hash), and what an attacker positioned
at that boundary would observe.

It is part of Krypteia's **audit-readiness** materials and is **not** a
security audit. It was written by the library's authors. Its purpose is
to give an independent auditor a precise map of where to probe, and to
let adopters understand the boundaries their own deployment must
enforce.

This document builds on `THREAT-MODEL.md` and `CRYPTOGRAPHIC-INVENTORY.md`
and assumes familiarity with both.

## How to read this document

A **trust boundary** is a line across which trust changes. Code and data
on one side cannot assume the other side is benign. The security
questions at any boundary are always the same three:

- What crosses the boundary?
- In what form does it cross — plaintext, ciphertext, or hash?
- What would an attacker who controls or observes the boundary learn or
  be able to do?

Krypteia itself is a library: it has no process, no network, no storage
of its own. It runs *inside* a consuming application. So most of these
boundaries are really boundaries of the **application that hosts
Krypteia** — but Krypteia's design determines what data is in what form
when it reaches each one. That is what this document describes.

## Boundary overview

    +-------------------------------------------------------------+
    |  USER ZONE                                                  |
    |  (user's device, holds the private key)                     |
    +-------------------------------------------------------------+
                |  (B1) key delivery / data submission
                v
    +-------------------------------------------------------------+
    |  APPLICATION ZONE                                           |
    |  (the consuming app + Krypteia, running in a server process)|
    |                                                             |
    |   +-----------------------------------------------------+   |
    |   |  Krypteia library                                   |   |
    |   +-----------------------------------------------------+   |
    +-------------------------------------------------------------+
        |  (B2)        |  (B3)            |  (B4)
        v              v                  v
    +---------+   +-------------+   +------------------+
    | DATABASE|   | MASTER KEY  |   | EMAIL CHANNEL    |
    | ZONE    |   | PROVIDER    |   | (reset delivery) |
    +---------+   +-------------+   +------------------+

        |  (B5) consumer-implemented re-encryption hook
        v
    +-------------------------------------------------------------+
    |  IDataReencryptionService  (application-provided code)      |
    +-------------------------------------------------------------+

Five boundaries, B1 through B5. Each is detailed below.

## B1 — User Zone <-> Application Zone

**Where.** Between a user's own device and the application hosting
Krypteia. In a typical deployment this is a network connection.

**What crosses, and in what form:**

- **At key generation:** a freshly generated **private key** is returned
  *to the user* so the user can store it. This is the one and only time
  a private key crosses this boundary in usable form, by design — the
  application does not retain it in usable form (see B2).
- **On encrypt/decrypt requests:** depending on how the adopter builds
  their application, either **plaintext** crosses this boundary (if the
  application performs encryption server-side using the user's stored
  public key) or only **ciphertext** crosses it (if the application
  performs encryption on the client). Krypteia's core operations can be
  invoked either way; the sample application demonstrates the
  server-side pattern.

**What an attacker at this boundary observes.**

- If the application encrypts server-side: an attacker who can observe
  this boundary sees **plaintext in transit**. The boundary must
  therefore be protected by transport encryption (TLS). Krypteia does
  not and cannot provide transport security; that is the application's
  responsibility.
- If the application encrypts client-side: the attacker sees only
  ciphertext, a materially stronger position.
- At key generation, an attacker observing this boundary at that moment
  would see the private key in transit. This is unavoidable — the key
  must reach the user somehow — and is again why the boundary requires
  transport encryption.

**Audit note.** The server-side-vs-client-side encryption decision is
the single most consequential architectural choice an adopter makes
with Krypteia, and it determines whether plaintext ever crosses B1. An
auditor reviewing a specific deployment should establish which model is
in use. The library does not force either; `THREAT-MODEL.md` Scenario 6
(server runtime compromise) is also relevant here.

## B2 — Application Zone <-> Database Zone

**Where.** Between Krypteia (via Entity Framework Core) and the
relational database storing user keys, reset tokens, and audit records.

**What crosses, and in what form:**

- **Public keys** — cross in the clear. Public keys are not secret.
- **Private-key backups** — cross **only as AES-256-GCM-wrapped
  ciphertext** (see `CRYPTOGRAPHIC-INVENTORY.md` section 2). A usable
  private key never crosses B2.
- **User data ciphertext** — crosses as RSA-OAEP ciphertext, if the
  application stores encrypted user data via Krypteia's persistence.
- **Reset tokens** — cross **only as SHA-256 hashes**. The usable token
  never crosses B2.
- **Audit records** — cross in the clear. They are operational metadata,
  not confidential, but their integrity matters.

**What an attacker at this boundary observes.** This boundary *is*
Scenario 1 of the threat model. An attacker with full read access to
the database zone — a leaked backup, injection, a malicious DBA — sees
public keys, wrapped (unreadable) private-key backups, ciphertext,
token hashes, and audit metadata. They see that users exist, counts,
and timestamps. **They cannot read user data and cannot obtain a usable
private key or a usable reset token.** This is the boundary Krypteia is
fundamentally built to make safe, and it is.

**Audit note.** The strength of this boundary depends on two
implementation facts the auditor should verify: that no code path
writes a private key to the database in unwrapped form, and that no
code path writes a reset token in unhashed form. The design says
neither happens; the audit confirms it.

## B3 — Application Zone <-> Master Key Provider

**Where.** Between Krypteia and whatever supplies the master key — a
file on disk (development), or, in a correct production deployment, an
HSM or managed key service. The boundary is crossed through the
pluggable `IMasterKeyProvider` interface.

**What crosses, and in what form:**

- The **master key itself**, or the ability to perform operations with
  it, crosses into the application zone so that private-key backups can
  be wrapped and unwrapped.

**What an attacker at this boundary observes.** This is the most
sensitive boundary in the system. An attacker who compromises the
master key provider obtains the master key — and, combined with
database access (B2), can unwrap every private-key backup. This is
Scenario 2 of the threat model.

**The critical property: B2 and B3 must be independently protected.**
Krypteia's security in the database-compromise scenario rests on the
master key *not* being obtainable from the database. If the master key
and the database live in the same trust zone — most starkly, the
**file-based master key provider sitting on the same host as the
database** — then B2 and B3 collapse into one boundary, and a single
breach yields both. The pluggable provider model exists precisely so
that, in production, the master key lives behind its own boundary with
its own access control and audit trail.

**Audit note.** For any specific deployment, the auditor must establish
**where the master key actually lives** relative to the database. The
file-based provider is for development only; its use in production is a
serious finding. This boundary, more than any other, is where an
adopter can unknowingly defeat the library's central guarantee.

## B4 — Application Zone <-> Email Channel

**Where.** Between Krypteia's key-reset flow and the email transport
that delivers reset tokens to users. Krypteia supports console (dev),
SMTP, and SendGrid transports.

**What crosses, and in what form:**

- A **reset token in usable (plaintext) form** crosses this boundary,
  inside an email message, on its way to the user. This is the one
  point in the system where a usable token is deliberately exposed
  outside the application zone — it has to be, because the user needs
  it.

**What an attacker at this boundary observes.** An attacker who can
read email destined for a user — by compromising the mail transport,
the mailbox, or the path in between — obtains a usable reset token.
Combined with the other Scenario 3 prerequisites, this enables a reset
in the user's name. The token's protections limit the damage: it is
single-use, expires in 15 minutes, and is rate-limited — so a token is
only useful briefly and a stolen-but-unused token has a short life.

**Audit note.** The security of B4 is largely outside Krypteia — it
depends on the chosen transport and the user's mailbox security. The
auditor should confirm the reset *email content* does not leak more
than the token requires, and that the SMTP/SendGrid transports are
configured for TLS. The console transport must never be used outside
development; an auditor should confirm production configuration does
not select it.

## B5 — Krypteia <-> IDataReencryptionService (consumer-provided code)

**Where.** Between Krypteia's reset flow and the optional, **consumer-
implemented** `IDataReencryptionService`. This boundary is unusual: it
is a boundary *into code the adopter writes*, not code Krypteia ships.

**What crosses, and in what form:**

- When a reset runs *and* the adopter has implemented this hook,
  Krypteia unwraps the user's old private-key backup and the
  re-encryption service is given what it needs to read the user's old
  data and re-encrypt it under the new key. For the duration of that
  operation, **plaintext user data is accessible to application code.**

**What an attacker at this boundary observes.** This is Scenario 5 of
the threat model. The boundary is only "open" during a reset with
re-encryption, and only for the user being reset. But during that
window, the adopter's own re-encryption code handles plaintext. A
vulnerability in *that consumer code* — which Krypteia neither writes
nor reviews — is exposed here.

**Audit note.** Because the code on the far side of B5 is written by
the adopter, it is outside the scope of an audit *of Krypteia* but
squarely inside the scope of an audit of a *deployment*. An auditor
reviewing a specific adopter's system must review their
`IDataReencryptionService` implementation if they have one. Krypteia's
responsibility at this boundary is narrow: to open it only during a
reset, only for the affected user, and to audit every reset.
Adopters who do not implement the hook do not have boundary B5 at all —
at the cost that data under an old key becomes unreadable after a reset.

## Data-form summary

A consolidated view of what form each sensitive item takes at each
boundary it crosses:

| Item | B1 user | B2 database | B3 master key | B4 email | B5 re-encrypt |
|---|---|---|---|---|---|
| User plaintext data | plaintext* | — | — | — | plaintext (during reset) |
| User data, encrypted | ciphertext | ciphertext | — | — | — |
| Private key | plaintext (at generation only) | wrapped ciphertext | — | — | unwrapped during reset |
| Master key | — | — | crosses into app | — | — |
| Reset token | plaintext (to user) | SHA-256 hash | — | plaintext (in email) | — |
| Public key | plaintext | plaintext | — | — | — |
| Audit records | — | plaintext | — | — | — |

\* At B1, user plaintext crosses only if the adopter encrypts
server-side. With client-side encryption, only ciphertext crosses B1.

## Summary

Krypteia's design concentrates its strongest guarantee at **B2, the
database boundary**: everything sensitive that crosses into the database
does so as ciphertext or as a hash. That is the boundary the library
exists to make safe.

The boundaries where sensitive material is deliberately exposed are few
and specific: **B3** (the master key enters the application zone — which
is why it must be protected independently of the database), **B4** (a
usable reset token reaches the user by email — which is why tokens are
short-lived and single-use), and **B5** (plaintext is accessible during
re-encryption — which is why that hook is optional and every reset is
audited).

The boundary most often weakened by adopters is **B3**: running the
development file-based master key provider in production collapses B3
into B2 and defeats the library's central guarantee. Any audit of a
real deployment should start there.