# Contributing to Krypteia

Thank you for considering a contribution to Krypteia. This document explains how to get set up and what to expect from the review process.

## Code of Conduct

This project follows the [Contributor Covenant](CODE_OF_CONDUCT.md). By participating, you agree to abide by it. Please report unacceptable behavior to the maintainers.

## Ground rules for a cryptography library

Krypteia is a cryptography library. A bug here can affect every consuming application. We hold contributions to a higher bar than most projects:

- **No custom cryptographic algorithms.** Use only published, standardized primitives via `System.Security.Cryptography`.
- **No reduced key sizes.** RSA below 2048 bits is rejected at the API surface for a reason; do not add escape hatches.
- **No logging of secrets.** Plaintext, private keys, tokens, and anything derived from them are forbidden from log output.
- **Tests are required.** Every new public API needs unit tests. Security-sensitive changes also need entries in `tests/Krypteia.SecurityTests`.
- **Generic error messages.** Cryptographic failure paths must not distinguish "wrong key" from "bad ciphertext" to avoid side-channel disclosure.

## Development setup

Requirements:

- .NET 10 SDK
- JetBrains Rider, Visual Studio 2026, or VS Code with the C# Dev Kit
- (Optional) `dotnet-format` and `dotnet-coverage` global tools

```bash
git clone https://github.com/isureshsubramanian/Krypteia
cd Krypteia
dotnet restore
dotnet build
dotnet test
```

Running just the security tests:

```bash
dotnet test tests/Krypteia.SecurityTests
```

## Pull request process

1. **Open an issue first** for anything beyond a typo or doc fix. This avoids wasted effort if the proposed change doesn't fit the project direction.
2. **Branch from `main`** with a descriptive name (`feat/hybrid-encryption`, `fix/key-version-mismatch`).
3. **Sign your commits** with GPG if you can. We require signed-off-by (DCO) at minimum.
4. **Keep PRs focused.** One logical change per PR makes review tractable.
5. **Update documentation** alongside code changes, including XML doc comments on public APIs.
6. **Update CHANGELOG.md** under the "Unreleased" section.

## Review process

- All PRs require **at least two maintainer approvals** before merge.
- All CI checks must pass: build, tests, CodeQL, dependency review.
- Crypto-related changes will get extra scrutiny and may take longer to review. Please be patient.
- We may ask for changes even on excellent PRs — this is normal and not a reflection of contribution quality.

## Reporting bugs (non-security)

Use the [GitHub issue tracker](https://github.com/isureshsubramanian/Krypteia/issues) with the bug report template. For security vulnerabilities, see [SECURITY.md](SECURITY.md) instead.

## Licensing

By contributing, you agree that your contributions will be licensed under the project's [MIT License](LICENSE). You also confirm that you have the right to submit the work under that license (the Developer Certificate of Origin).

## Questions

Open a [Discussion](https://github.com/isureshsubramanian/Krypteia/discussions) — questions there often help future contributors find answers.
