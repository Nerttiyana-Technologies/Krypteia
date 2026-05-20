# Security Policy

## Reporting a vulnerability

If you believe you have found a security vulnerability in Krypteia, please report it privately. **Do not open a public GitHub issue** — public disclosure before a patch is available puts every user of the library at risk.

### How to report

- **Preferred:** Open a [GitHub Security Advisory](https://github.com/isureshsubramanian/Krypteia/security/advisories/new) on the repository. This creates a private channel between you and the maintainers.
- **Alternate:** Email `i.suresh.subramanian@gmail.com` (replace with actual address once provisioned). For sensitive details, encrypt with the PGP key linked from the GitHub security page.

### What to include

- A description of the vulnerability and its potential impact
- Steps to reproduce, ideally with a minimal proof-of-concept
- Affected versions (if known)
- Your name and contact details (for credit, if desired)

## Our commitments

- **Acknowledgment** within **48 hours** of your report
- **Initial assessment** within **7 days**, including severity classification (CVSS v3.1)
- **Coordinated disclosure** — we will work with you on a disclosure timeline, typically 90 days from initial report
- **Credit** in the published advisory, unless you prefer to remain anonymous

## Supported versions

| Version | Status | Security patches |
|---|---|---|
| 1.x (current) | Active | Yes — bug fixes + security |
| 0.x (preview) | Not supported | None — upgrade to 1.x |

This table will be updated as new major versions are released. Previous major versions receive **security patches only** for 12 months after the next major version's release.

## Out of scope

The following are explicitly **not** considered vulnerabilities for the purposes of this policy:

- Vulnerabilities requiring physical access to a user's device or unattended unlocked session
- Issues in third-party packages we depend on — please report those upstream (we will track and update)
- Social engineering or phishing scenarios that do not involve a flaw in Krypteia itself
- Findings that require non-default, explicitly insecure configuration
- Reports generated solely by automated scanners without manual validation

## Hall of fame

Researchers who responsibly disclose valid vulnerabilities will be listed here (with their consent).

*No disclosures yet.*

## Bounty

This project does not currently offer a paid bug bounty. We do offer public credit and our sincere thanks. If commercial sponsors fund a bounty program in the future, this section will be updated.
