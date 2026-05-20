# Krypteia Cryptographic Inventory

## About this document

This document catalogs every cryptographic decision in Krypteia: the
algorithms, parameters, formats, and design choices, together with the
rationale for each, the alternatives considered, and known limitations.

It is part of Krypteia's **audit-readiness** materials and is **not** a
security audit. It was written by the library's authors. Its purpose is
to let an independent auditor verify each decision against the
implementation, and to let adopters understand exactly what cryptography
they are relying on.

Where this document says a choice is "sound" or "appropriate," that is
the authors' reasoning, not an independent finding. Confirming it is the
job of the audit described in `AUDIT-SCOPE.md`.

## Summary table

| Purpose | Algorithm / Choice |
|---|---|
| Asymmetric encryption | RSA-2048, OAEP padding, SHA-256 |
| Private-key backup wrapping | AES-256-GCM |
| Reset token hashing | SHA-256 |
| Random number generation | Platform cryptographic RNG |
| Reset token size | 32 bytes (256 bits) |

Each row is detailed below.

## 1. Asymmetric encryption — RSA-2048 with OAEP-SHA256

**What.** User data is encrypted with RSA using OAEP (Optimal
Asymmetric Encryption Padding) with SHA-256 as the padding hash. Keys
are 2048-bit.

**Why.** RSA-OAEP is a standard, well-understood asymmetric encryption
scheme with broad, mature support in the .NET platform. OAEP is the
correct padding choice for RSA encryption — it is the modern
replacement for PKCS#1 v1.5 padding, which is vulnerable to padding-
oracle attacks (Bleichenbacher). SHA-256 as the OAEP hash is a
conventional, conservative selection.

**Key size.** 2048 bits is the current widely accepted minimum for RSA
and is considered adequate against classical attackers for the
foreseeable near term. 3072 or 4096 bits would provide a larger margin
at a performance cost.

**Alternatives considered.** Elliptic-curve schemes (e.g. ECIES) offer
equivalent security at smaller key sizes and better performance. RSA
was chosen for the maturity and ubiquity of its tooling and for the
straightforwardness of its key handling. This is a reasonable choice,
but an ECC-based design would also have been defensible — an auditor
may wish to comment on the trade-off.

**Known limitations.**
- RSA-2048 is **not quantum-resistant.** A sufficiently capable quantum
  computer running Shor's algorithm would break it. No such machine is
  known to exist, but data encrypted today could be captured and
  decrypted later ("harvest now, decrypt later"). See section 7.
- RSA encryption is limited in the size of plaintext it can directly
  encrypt (bounded by the key size minus padding overhead). Adopters
  encrypting large payloads should encrypt a symmetric key with RSA and
  the bulk data with that symmetric key. Whether and how Krypteia's API
  guides adopters on this is a point for audit review.

## 2. Private-key backup wrapping — AES-256-GCM

**What.** A user's private key, when stored as a recoverable backup, is
encrypted ("wrapped") with AES-256 in GCM (Galois/Counter Mode) under
the master key.

**Why.** AES-256-GCM is an authenticated encryption mode: it provides
both confidentiality and integrity. The integrity property matters here
— it means a wrapped backup that has been tampered with in the database
will fail authentication on unwrap rather than silently decrypting to
corrupted key material. AES-256 is a conservative key size.

**Wire format.** A wrapped value is laid out as:

    [version: 1 byte][nonce: 12 bytes][tag: 16 bytes][ciphertext: variable]

and then Base64-encoded for storage. The components:
- **Version byte** — allows the format to evolve. A future change to
  the wrapping scheme can be distinguished from version 1.
- **Nonce (12 bytes)** — the GCM nonce, the standard 96-bit size for
  GCM. Generated fresh per wrap operation (see section 4).
- **Tag (16 bytes)** — the GCM authentication tag, the full 128-bit
  size.
- **Ciphertext** — the encrypted private key.

**Nonce uniqueness.** GCM security depends critically on never reusing
a (key, nonce) pair. Krypteia generates a fresh random nonce for every
wrap operation using the platform cryptographic RNG. With a 96-bit
random nonce, reuse probability is negligible for any realistic number
of operations under a single master key. **This is a security-critical
property and an explicit item for audit verification** — the auditor
should confirm that no code path can reuse a nonce.

**Alternatives considered.** AES-KW (the NIST key-wrap algorithm) is
purpose-built for wrapping key material. AES-256-GCM was chosen instead
as a general authenticated-encryption primitive with first-class .NET
support and an explicit, inspectable wire format. Both are defensible.

**Known limitations.**
- GCM's nonce-reuse fragility is the main footgun of this choice. The
  mitigation (random 96-bit nonce per operation) is sound, but it is a
  property the implementation must get exactly right.

## 3. Reset token hashing — SHA-256

**What.** When a key-reset token is issued, the token itself is sent to
the user (by email). Only the SHA-256 hash of the token is stored in
the database.

**Why.** This is the same principle as not storing passwords in
plaintext. An attacker who reads the database obtains only token
hashes. Because SHA-256 is a one-way function, the attacker cannot
derive the original token — and the original token is what the reset
flow requires. When a user presents a token, Krypteia hashes it and
compares against the stored hash.

**Why a plain hash is acceptable here (unlike for passwords).** Password
hashing requires slow, salted algorithms (bcrypt, Argon2, etc.) because
passwords have low entropy and are guessable. A Krypteia reset token is
**not** low-entropy: it is 32 bytes from a cryptographic RNG (section
5). It is not guessable, so the slow-hashing and salting protections
that passwords need do not apply. A single pass of SHA-256 over a
high-entropy secret is appropriate. This reasoning is worth an auditor's
explicit confirmation.

**Known limitations.**
- The argument above depends entirely on tokens genuinely having full
  256-bit entropy. If token generation were ever weakened, plain
  SHA-256 would no longer be sufficient. The two decisions are linked.

## 4. Random number generation — platform cryptographic RNG

**What.** All security-sensitive random values — RSA key generation
inputs, GCM nonces, reset tokens — are produced by the .NET platform's
cryptographic random number generator, via `RandomNumberGenerator`.
Krypteia does not use `System.Random` or any non-cryptographic source
for security-sensitive values.

**Why.** `System.Random` is a non-cryptographic PRNG: predictable and
unsuitable for security use. The platform cryptographic RNG is designed
for exactly this purpose and draws on the operating system's entropy.

**Known limitations.**
- Krypteia inherits the quality of the platform RNG. On a correctly
  functioning, properly seeded OS this is not a concern. A defective or
  poorly seeded RNG environment (some constrained or virtualized
  environments historically) would undermine every random value in the
  library. This is part of the trust assumption on platform primitives
  stated in `THREAT-MODEL.md`.

## 5. Reset token size and properties — 32 bytes, single-use, time-limited, rate-limited

**What.** A reset token is 32 bytes (256 bits) drawn from the platform
cryptographic RNG. Each token is:
- **Single-use** — once consumed, it cannot be used again.
- **Time-limited** — valid for 15 minutes from issue.
- **Rate-limited** — at most 3 reset attempts per user per hour.

**Why.**
- **256 bits** places the token far beyond brute-force reach. There is
  no feasible number of guesses that meaningfully searches a 2^256
  space.
- **Single-use** means a token observed or replayed after legitimate
  use is worthless.
- **15-minute expiry** bounds the window in which a token — for
  example one sitting in an email inbox — is useful to an attacker.
- **3-per-user-per-hour** limits both abuse of the reset flow as a
  nuisance/DoS vector and the rate at which an attacker could ever
  cause tokens to be generated.

**Known limitations.**
- The 15-minute TTL and 3-per-hour limit are policy values. They are
  reasonable defaults; an adopter with different requirements would
  need to weigh usability against exposure. Whether these are
  configurable, and whether their defaults are clearly documented for
  adopters, is a point for audit review.

## 6. Error handling — generic decryption failures

**What.** When a decryption or unwrap operation fails, Krypteia returns
a single generic error. It does **not** report whether the failure was
due to a wrong key, malformed ciphertext, a failed authentication tag,
or any other specific cause.

**Why.** Detailed cryptographic error messages are a classic side
channel. Padding-oracle and related attacks work by submitting many
crafted ciphertexts and learning, from the *type* of error returned,
something about the plaintext or key. By returning one
indistinguishable error for all failure modes, Krypteia denies an
attacker that signal.

**Known limitations.**
- Error *messages* are uniform by design. Whether observable error
  *timing* is also uniform is a separate question — see section 8.
- The guarantee depends on no code path anywhere (including logging,
  exceptions, or audit records visible to an attacker) leaking the
  specific failure cause. This is a whole-codebase property and an
  explicit audit item.

## 7. Post-quantum considerations

Krypteia currently uses RSA-2048, which is **not** quantum-resistant.

The practical risk today is the "harvest now, decrypt later" pattern:
an adversary who records ciphertext now could decrypt it once a
cryptographically relevant quantum computer exists. No such machine is
publicly known, and timelines are uncertain, but for data with a long
confidentiality lifetime this is a genuine consideration an adopter
should weigh.

The .NET platform has begun introducing post-quantum primitives
(lattice-based KEM and signature schemes standardized by NIST). A future
version of Krypteia could offer a post-quantum or hybrid encryption
option. This is noted as a forward-looking item, not a current feature.
The format version byte in the AES-GCM wire format (section 2) is one
small piece of forward-compatibility groundwork.

## 8. Open items explicitly flagged for independent audit

The following are the points in this inventory that most warrant an
external auditor's attention. They are collected here so they are not
lost in the detail above:

1. **GCM nonce uniqueness** — confirm that no code path can produce a
   repeated (master key, nonce) pair (section 2).
2. **Generic error handling** — confirm that no path (messages,
   exceptions, logs, audit records, or timing) leaks the specific cause
   of a cryptographic failure (sections 6 and below).
3. **Decryption timing** — a timing-sensitivity test for decryption
   exists in the test suite but is skipped by default. Constant-time
   behavior of the platform primitives is *assumed*, not independently
   measured. An auditor should assess whether observable timing
   differences exist between success and failure paths.
4. **Token entropy** — confirm reset tokens genuinely receive a full 32
   bytes from the cryptographic RNG, since the sufficiency of plain
   SHA-256 token hashing (section 3) depends on it.
5. **RSA plaintext-size handling** — confirm the API does not allow an
   adopter to silently attempt to RSA-encrypt a payload larger than the
   key can accommodate, or that it guides them appropriately.
6. **Algorithm choice trade-offs** — RSA vs ECC (section 1) and
   AES-GCM vs AES-KW (section 2) are reasonable but not the only
   defensible choices; auditor commentary is welcomed.

## Summary

Krypteia's cryptographic choices are conventional and conservative:
standard algorithms (RSA-OAEP, AES-GCM, SHA-256), standard parameters,
no custom cryptography, and platform-provided primitives throughout.
The design avoids known footguns where it can — generic error handling
against padding oracles, hashed token storage, a cryptographic RNG for
all secrets.

The main residual concerns are the ones inherent to the chosen
primitives: GCM's nonce-reuse fragility, RSA's lack of quantum
resistance, and the fact that several guarantees (nonce uniqueness,
non-leaking errors, token entropy) are *design intentions* whose correct
*implementation* only an independent audit can confirm.