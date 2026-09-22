// The one exception an implementation of IObjectStore is allowed to throw. It
// exists so that a lost connection, a missing permission or a broken
// configuration reaches a service as the same type no matter which store is
// configured, and so that no type of Azure or of the file system ever escapes
// through it.

namespace ReviewMyDoc.Core.Storage;

/// <summary>
/// Reports a storage failure that no caller could have planned for.
/// </summary>
/// <remarks>
/// <para>
/// Expected outcomes are result values, not exceptions: a missing entry is
/// <see cref="ObjectReadResult.NotFound"/>, a condition that did not hold is
/// <see cref="ObjectWriteResult.Conflict"/>. This exception is for everything
/// else, and a service is not expected to catch it; it belongs to the error
/// page, not to a message beside a form.
/// </para>
/// <para>
/// The original failure belongs in <see cref="Exception.InnerException"/> so
/// the log keeps it, while the message stays free of anything a log must not
/// hold: no token, no path of a foreign system, no personal data. See
/// <c>docs/Konventionen.md</c>, section Code.
/// </para>
/// </remarks>
public sealed class ObjectStoreException : Exception
{
    /// <summary>States what failed, in terms of the store and not of its implementation.</summary>
    /// <param name="message">What was attempted and did not work.</param>
    public ObjectStoreException(string message)
        : base(message)
    {
    }

    /// <summary>States what failed and keeps the original failure for the log.</summary>
    /// <param name="message">What was attempted and did not work.</param>
    /// <param name="innerException">The failure the underlying storage reported.</param>
    public ObjectStoreException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
