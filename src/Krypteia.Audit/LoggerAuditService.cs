using Krypteia.Abstractions;
using Microsoft.Extensions.Logging;

namespace Krypteia.Audit;

/// <summary>
/// A simple <see cref="IAuditService"/> implementation that writes audit entries
/// via <see cref="ILogger"/>.
/// </summary>
/// <remarks>
/// <para>
/// This implementation is suitable for development and small deployments. Production
/// CMMC environments should use an implementation that writes to append-only storage
/// (e.g., AWS S3 Object Lock, Azure Blob immutable storage, or a SIEM ingestion endpoint).
/// </para>
/// <para>
/// Compliance frameworks require audit logs to be tamper-evident and retained for
/// 7 years. <see cref="LoggerAuditService"/> alone does not satisfy that requirement
/// unless your logging sink does.
/// </para>
/// <para>
/// Uses the <c>LoggerMessage</c> source generator (introduced in .NET 6) for
/// allocation-free, source-generated logging. The <see cref="Log"/> nested class
/// holds the generated delegates.
/// </para>
/// </remarks>
public sealed partial class LoggerAuditService : IAuditService
{
    private readonly ILogger<LoggerAuditService> _logger;

    /// <summary>Initializes a new instance.</summary>
    public LoggerAuditService(ILogger<LoggerAuditService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public Task RecordAsync(AuditEntry entry, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entry);

        Log.AuditRecord(
            _logger,
            entry.Timestamp,
            entry.Operation,
            entry.UserId,
            entry.ActorId ?? "(none)",
            entry.FieldName ?? "(none)",
            entry.KeyVersion,
            entry.Result,
            entry.IpAddress ?? "(unknown)",
            entry.CorrelationId ?? "(none)",
            entry.ErrorCode ?? "(none)");

        return Task.CompletedTask;
    }

    /// <summary>
    /// Source-generated log delegates. Marked partial so the
    /// <c>LoggerMessage</c> source generator can emit the implementation.
    /// </summary>
    private static partial class Log
    {
        [LoggerMessage(
            EventId = 1000,
            Level = LogLevel.Information,
            Message =
                "[KrypteiaAudit] {Timestamp:o} {Operation} user={UserId} actor={ActorId} " +
                "field={FieldName} keyVersion={KeyVersion} result={Result} ip={IpAddress} " +
                "correlationId={CorrelationId} errorCode={ErrorCode}")]
        public static partial void AuditRecord(
            ILogger logger,
            DateTimeOffset timestamp,
            AuditOperation operation,
            string userId,
            string actorId,
            string fieldName,
            int keyVersion,
            AuditResult result,
            string ipAddress,
            string correlationId,
            string errorCode);
    }
}
