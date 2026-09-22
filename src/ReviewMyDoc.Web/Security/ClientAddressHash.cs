// Turns the address of a client into a hash. The rate limiters have to tell two
// visitors apart without ever keeping an address, because docs/Konventionen.md
// allows client addresses only as a hash. It stands here so that every limiter,
// and later the log, derive the same value from the same request.

using System.Security.Cryptography;
using System.Text;

namespace ReviewMyDoc.Web.Security;

/// <summary>Hash of the client address of a request.</summary>
public static class ClientAddressHash
{
    /// <summary>
    /// Partition key for a request without an address, as the in-process test
    /// server sends them.
    /// </summary>
    /// <remarks>
    /// One shared key, and that is on purpose: requests without an address are
    /// counted together rather than each getting a budget of its own, which
    /// would be a way around every limiter.
    /// </remarks>
    public const string Unknown = "unknown";

    /// <summary>Hash of the address of the given request.</summary>
    /// <param name="context">The request whose connection carries the address.</param>
    /// <returns>
    /// The hash as lower case hexadecimal with 64 characters, or
    /// <see langword="null"/> if the request has no address.
    /// </returns>
    public static string? Of(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var address = context.Connection.RemoteIpAddress;

        return address is null ? null : Of(address.ToString());
    }

    /// <summary>Hash of one address given as text.</summary>
    /// <param name="address">The address, for example <c>203.0.113.7</c>.</param>
    /// <returns>The hash as lower case hexadecimal with 64 characters.</returns>
    /// <remarks>
    /// SHA-256 without a secret. It keeps the address out of memory and out of
    /// any later log and still lets two requests be recognised as coming from
    /// the same place. It does not hide a guessed address from someone who
    /// hashes it as well; that would need a secret per deployment, which this
    /// application does not have and does not need for counting.
    /// </remarks>
    public static string Of(string address)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(address);

        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(address)));
    }

    /// <summary>
    /// Hash of the address of a request, or <see cref="Unknown"/> when it has
    /// none. Used where a key is needed in every case, as in the partition of a
    /// rate limiter.
    /// </summary>
    /// <param name="context">The request whose connection carries the address.</param>
    /// <returns>The hash, or <see cref="Unknown"/>.</returns>
    public static string PartitionOf(HttpContext context) => Of(context) ?? Unknown;
}
