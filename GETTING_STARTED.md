# Getting Started

This is the complete Krypteia project, ready to build on .NET 10.

## To use it

1. Extract the zip into `/Users/[USERNAME]/[DIRECTORY]/`.
   This will create a `Krypteia/` folder containing everything.
2. Open `Krypteia.sln` in JetBrains Rider (or Visual Studio 2026 / VS Code with C# Dev Kit).
3. Build: `dotnet build` (or use the IDE).
4. Run tests: `dotnet test`.
5. Run the sample API: `dotnet run --project samples/Krypteia.Samples.WebApi` — then open https://localhost:7160/scalar/v1.

## What you should see

```
✓ Krypteia.Abstractions
✓ Krypteia.Core
✓ Krypteia.KeyReset
✓ Krypteia.Audit
✓ Krypteia.AspNetCore
✓ Krypteia.EntityFrameworkCore
✓ Krypteia.UnitTests          (8 tests pass)
✓ Krypteia.IntegrationTests   (1 placeholder test)
✓ Krypteia.SecurityTests      (2 tests pass, 1 skipped intentionally)
✓ Krypteia.Samples.WebApi
```

Build outcome: **10 of 10 projects built successfully, 11 tests pass, 1 skipped.**

## What's in the box

```
Krypteia/
├── README.md, LICENSE, SECURITY.md, CONTRIBUTING.md
├── CODE_OF_CONDUCT.md, CHANGELOG.md, GETTING_STARTED.md
├── Directory.Build.props        ← common build settings
├── global.json                  ← pins .NET 10 SDK
├── .editorconfig                ← code-style enforcement
├── .gitignore
├── Krypteia.sln                 ← open this in Rider
│
├── src/                         ← the library (will become NuGet packages)
│   ├── Directory.Build.props
│   ├── Krypteia.Abstractions/   ← interfaces + DTOs, zero dependencies
│   ├── Krypteia.Core/           ← RsaEncryptionService + RsaKeyPairGenerator
│   ├── Krypteia.KeyReset/       ← skeleton; reset flow goes here
│   ├── Krypteia.Audit/          ← LoggerAuditService (modern, allocation-free)
│   ├── Krypteia.AspNetCore/     ← AddKrypteia() DI extension
│   └── Krypteia.EntityFrameworkCore/ ← skeleton; EF value converters
│
├── tests/
│   ├── Directory.Build.props    ← test-friendly analyzer settings
│   ├── Krypteia.UnitTests/      ← 8 tests covering encryption + key gen
│   ├── Krypteia.IntegrationTests/
│   └── Krypteia.SecurityTests/  ← timing-safety + key-material redaction
│
├── samples/
│   ├── Directory.Build.props
│   └── Krypteia.Samples.WebApi/ ← runnable Swagger demo
│
└── docs/                        ← compliance + design docs go here
```

