# CMMC Level 2 — Control Mapping

> **Status: In progress.** This document is being prepared. It will map
> Krypteia's cryptographic and audit capabilities to the relevant
> CMMC Level 2 practices.

## Scope and intent

Krypteia is a software library. It provides cryptographic and audit
*primitives* that can support an organization's CMMC Level 2 compliance
effort — but **the library cannot make a system compliant on its own**.
CMMC compliance is a property of an entire system and the organization
operating it, not of any single component.

When complete, this document will, for each relevant CMMC Level 2
practice:

- State what Krypteia provides toward that practice
- State explicitly what Krypteia does **not** provide — the gaps the
  operator must close
- Avoid any claim that using Krypteia alone satisfies a practice

## CMMC domains Krypteia is expected to be relevant to

Krypteia touches a subset of the 14 CMMC Level 2 domains. The mapping
will focus on:

- **Access Control (AC)** — key-based access to encrypted data
- **Audit & Accountability (AU)** — structured audit logging of
  cryptographic operations
- **Identification & Authentication (IA)** — user key management
- **System & Communications Protection (SC)** — encryption of
  sensitive data at rest

Other CMMC domains (physical protection, personnel security, incident
response, etc.) are outside the scope of a cryptographic library and
remain entirely the operator's responsibility.

## A note on language

Krypteia is described as CMMC Level 2 **compatible**, never **certified**
or **compliant**. Only an assessment of a complete system by an
authorized body can establish compliance. This document is an aid to
that process, not a substitute for it.

---

*This document will be completed alongside Krypteia's audit-readiness
materials. See the project roadmap.*