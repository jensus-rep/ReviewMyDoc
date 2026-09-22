// Checks a typed password against the hash from the configuration. This is the
// whole of what ASP.NET Core Identity would otherwise do for a single account,
// and the reason it is only this much is written down in docs/Konventionen.md,
// section Stack: there is one account, no user store and no database, so the
// password hasher of Identity is used and nothing else of it.

using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace ReviewMyDoc.Web.Security;

/// <summary>Answers the one question the sign-in page asks.</summary>
/// <remarks>
/// A service and not a static method, so the page takes it from the container
/// and a test can configure it through the configuration like the running
/// application does.
/// </remarks>
public sealed class OwnerPasswordCheck
{
    private readonly IOptions<OwnerOptions> _options;
    private readonly ILogger<OwnerPasswordCheck> _logger;

    /// <summary>
    /// The hasher, shared by this check and by the command that prints a hash.
    /// </summary>
    /// <remarks>
    /// One instance with the default settings on both sides. The default is the
    /// current format of Identity, PBKDF2 with HMAC-SHA512 and a work factor
    /// that is deliberately high; the format carries its own parameters, so a
    /// hash written today keeps being readable when that default changes.
    /// </remarks>
    private readonly PasswordHasher<OwnerAccount> _hasher = new();

    /// <summary>
    /// A hash of a value nobody knows, checked against whenever no hash is
    /// configured.
    /// </summary>
    /// <remarks>
    /// Without it an unconfigured application would answer a sign-in attempt
    /// noticeably faster than a configured one, and the measurement would say
    /// whether there is anything to guess at here at all. Built once per
    /// instance, and the instance is a singleton, so it costs one hash at start
    /// and nothing afterwards.
    /// </remarks>
    private readonly string _absentHash;

    /// <summary>Takes the configured hash and a log.</summary>
    /// <param name="options">The section <c>Owner</c> of the configuration.</param>
    /// <param name="logger">
    /// The log. It is told that a hash is missing or unreadable, never what it
    /// says: docs/Konventionen.md, section Code, keeps secrets out of the log,
    /// and a hash in a log file is a hash outside the configuration it was put
    /// into.
    /// </param>
    /// <exception cref="ArgumentNullException">A parameter is <see langword="null"/>.</exception>
    public OwnerPasswordCheck(IOptions<OwnerOptions> options, ILogger<OwnerPasswordCheck> logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);

        _options = options;
        _logger = logger;
        _absentHash = _hasher.HashPassword(OwnerAccount.Instance, Guid.NewGuid().ToString("N"));
    }

    /// <summary>Whether this password is the password of the owner.</summary>
    /// <param name="password">What was typed into the form.</param>
    /// <returns>
    /// <see langword="true"/> only when a hash is configured and the password
    /// belongs to it.
    /// </returns>
    /// <remarks>
    /// Every way of failing returns the same <see langword="false"/> and the
    /// page answers all of them with the same sentence. Whether the password was
    /// wrong, empty or whether the application has no hash at all is something
    /// the person at the form must not be able to tell apart, because each of
    /// those answers would be a step of the guessing done for them.
    /// </remarks>
    public bool Matches(string? password)
    {
        var configured = _options.Value.PasswordHash;
        var hash = string.IsNullOrWhiteSpace(configured) ? _absentHash : configured;

        if (string.IsNullOrEmpty(password))
        {
            // Still checked against a hash, for the same reason the absent hash
            // exists: an empty field must not be the fast answer.
            password = string.Empty;
        }

        PasswordVerificationResult result;

        try
        {
            result = _hasher.VerifyHashedPassword(OwnerAccount.Instance, hash, password);
        }
        catch (FormatException)
        {
            // A configured value that is not a hash at all. Named as a fault,
            // without its content, and answered like a wrong password: an
            // application that let everybody in because its hash was mistyped
            // would be the worst of the possible readings.
            _logger.LogError(
                "The configured value of {Key} is not a password hash. Nobody can sign in until it is set again; see docs/Betrieb.md.",
                $"{OwnerOptions.SectionName}:{nameof(OwnerOptions.PasswordHash)}");

            return false;
        }

        // SuccessRehashNeeded means the hash is older than the current format of
        // Identity. The password is right, and there is nowhere to write a new
        // hash to: it lives in the configuration, and only the operator puts it
        // there. So it counts as success, and the note belongs to the operator.
        if (result == PasswordVerificationResult.SuccessRehashNeeded)
        {
            _logger.LogInformation(
                "The password hash in {Key} uses an older format. It keeps working; a new one can be created with the command described in docs/Betrieb.md.",
                $"{OwnerOptions.SectionName}:{nameof(OwnerOptions.PasswordHash)}");

            return true;
        }

        return result == PasswordVerificationResult.Success;
    }
}
