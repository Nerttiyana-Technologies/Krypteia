# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

- Initial scaffolding: solution structure, six library projects, three test projects, sample Web API
- Targeting .NET 10 LTS
- `IEncryptionService` interface with RSA-2048 / OAEP-SHA256 default implementation
- `IKeyManagementService` interface (implementation pending)
- `IKeyResetService` interface (implementation pending)
- `IAuditService` interface with `LoggerAuditService` default implementation using LoggerMessage source generator
- `KeyPair` DTO with redacting `ToString()` for safe logging
- `KrypteiaException` and `KeyResetException` types
- `RsaKeyPairGenerator` static helper for creating PEM-encoded key pairs
- Unit tests for encryption roundtrip, key generation, and basic security properties
- Security tests checking that error messages do not leak which failure mode occurred
- ASP.NET Core DI extensions (`AddKrypteia()`)
- Sample Web API demonstrating encrypt/decrypt/generate-keys endpoints

[Unreleased]: https://github.com/isureshsubramanian/Krypteia/compare/HEAD
