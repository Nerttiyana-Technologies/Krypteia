namespace Krypteia.Abstractions;

/// <summary>
/// Base exception type for all errors raised by Krypteia.
/// </summary>
/// <remarks>
/// <para>
/// Messages on this exception type are deliberately generic. Cryptographic failures
/// must not disclose whether a key was wrong, a payload was corrupt, padding was
/// invalid, or any other detail that could aid an attacker (this is the same principle
/// behind avoiding padding-oracle vulnerabilities).
/// </para>
/// <para>
/// Detailed diagnostic information should be written to internal logs only,
/// not returned to the caller.
/// </para>
/// </remarks>
public class KrypteiaException : Exception
{
    /// <summary>Initializes a new instance with the supplied message.</summary>
    /// <param name="message">A generic, user-safe error message.</param>
    public KrypteiaException(string message) : base(message) { }

    /// <summary>Initializes a new instance with the supplied message and inner exception.</summary>
    /// <param name="message">A generic, user-safe error message.</param>
    /// <param name="innerException">The underlying exception. Kept for internal diagnostics only.</param>
    public KrypteiaException(string message, Exception innerException) : base(message, innerException) { }
}

/// <summary>
/// Raised when a key reset operation cannot complete.
/// </summary>
/// <remarks>
/// The message is always generic to prevent leaking why a particular reset failed.
/// Concrete reasons (expired token, reused token, missing user) are written to audit
/// logs and never communicated to the caller.
/// </remarks>
public sealed class KeyResetException : KrypteiaException
{
    /// <summary>Initializes a new instance with the default generic message.</summary>
    public KeyResetException() : base("The key reset request could not be completed.") { }

    /// <summary>Initializes a new instance with a custom (still generic) message.</summary>
    public KeyResetException(string message) : base(message) { }

    /// <summary>Initializes a new instance with a custom message and inner exception.</summary>
    public KeyResetException(string message, Exception innerException) : base(message, innerException) { }
}
