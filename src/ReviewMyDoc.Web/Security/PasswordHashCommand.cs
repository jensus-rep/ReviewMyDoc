// The tool that turns a password into the hash for the configuration. It is a
// command of this application and not a project of its own, so that it uses the
// very same hasher the sign-in uses and the two can never drift apart. Whoever
// receives this repository runs it once and never needs to understand it.
//
// The password is asked for and never taken as an argument. An argument stands
// in the command line, and the command line of a Windows shell is written to
// ConsoleHost_history.txt, of a bash to .bash_history, and it can be read out of
// the process list while the command runs. Asked for, it exists in this process
// and nowhere else: it is not echoed, not written to a file and not put into the
// configuration anywhere, and what the command prints is the hash.

using System.Text;
using Microsoft.AspNetCore.Identity;

namespace ReviewMyDoc.Web.Security;

/// <summary>
/// The command <c>passwort-hash</c>: reads a password without showing it and
/// prints the hash that belongs into <c>Owner:PasswordHash</c>.
/// </summary>
public static class PasswordHashCommand
{
    /// <summary>The word that calls this command instead of the web server.</summary>
    public const string Verb = "passwort-hash";

    /// <summary>Runs the command if the arguments ask for it.</summary>
    /// <param name="args">The arguments the application was started with.</param>
    /// <param name="exitCode">
    /// What the process should return: 0 for a printed hash, 1 for an input that
    /// was refused, 2 for a call that was meant differently than it is allowed.
    /// </param>
    /// <returns>
    /// <see langword="true"/> when the command ran and the application must not
    /// start a web server.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="args"/> is <see langword="null"/>.</exception>
    public static bool TryRun(string[] args, out int exitCode)
    {
        ArgumentNullException.ThrowIfNull(args);

        exitCode = 0;

        if (args.Length == 0 || !string.Equals(args[0], Verb, StringComparison.Ordinal))
        {
            return false;
        }

        UseUnicodeOutput();

        // Everything the command says about itself goes to the error output, so
        // that the standard output carries the hash and nothing else and can be
        // read by a script.
        var messages = Console.Error;

        if (args.Length > 1)
        {
            messages.WriteLine(
                $"Aufruf: dotnet run --project src/ReviewMyDoc.Web -- {Verb}");
            messages.WriteLine(
                "Das Passwort wird nicht als Argument angenommen, weil es dann in der Verlaufsdatei "
                + "der Kommandozeile und in der Prozessliste stünde. Der Befehl fragt danach.");

            exitCode = 2;

            return true;
        }

        var password = ReadPassword(messages);

        if (password is null)
        {
            exitCode = 1;

            return true;
        }

        var hash = HashOf(password);

        messages.WriteLine();
        messages.WriteLine("Der Hash für Owner:PasswordHash, siehe docs/Betrieb.md:");
        Console.Out.WriteLine(hash);

        return true;
    }

    /// <summary>The hash of a password, exactly as the command prints it.</summary>
    /// <param name="password">The password to hash.</param>
    /// <returns>The value for <c>Owner:PasswordHash</c>.</returns>
    /// <remarks>
    /// A method of its own so that a test can create a hash the way an operator
    /// does and hand it to the sign-in. Every hash carries its own random salt,
    /// so two calls with the same password differ, and only checking tells them
    /// apart.
    /// </remarks>
    public static string HashOf(string password) =>
        new PasswordHasher<OwnerAccount>().HashPassword(OwnerAccount.Instance, password);

    /// <summary>
    /// Switches the console over to Unicode, so the German texts below appear
    /// with their umlauts instead of with replacement characters.
    /// </summary>
    /// <remarks>
    /// A console that is redirected or absent refuses this, and then there is
    /// nothing to fix: the output goes to a pipe, which carries the bytes of the
    /// writer either way.
    /// </remarks>
    private static void UseUnicodeOutput()
    {
        try
        {
            Console.OutputEncoding = Encoding.UTF8;
        }
        catch (IOException)
        {
        }
    }

    /// <summary>
    /// Asks for the password, twice when somebody is typing it.
    /// </summary>
    /// <param name="messages">Where the prompts go.</param>
    /// <returns>The password, or <see langword="null"/> when it was refused.</returns>
    /// <remarks>
    /// Two ways in, and both keep the password off the command line. A person
    /// types it, and then it is asked for twice and compared, because a typo
    /// would become a hash nobody can sign in with and the mistake would only
    /// show up at the form. A script pipes it in, one line on the standard
    /// input, and then there is nothing to compare it with and nothing to show
    /// anybody.
    /// </remarks>
    private static string? ReadPassword(TextWriter messages)
    {
        if (Console.IsInputRedirected)
        {
            var piped = Console.In.ReadLine();

            if (string.IsNullOrEmpty(piped))
            {
                messages.WriteLine("Es kam kein Passwort auf der Standardeingabe an.");

                return null;
            }

            return piped;
        }

        messages.Write("Passwort: ");
        var first = ReadHidden();
        messages.WriteLine();

        if (first.Length == 0)
        {
            messages.WriteLine("Ein leeres Passwort wird nicht angenommen.");

            return null;
        }

        messages.Write("Passwort wiederholen: ");
        var second = ReadHidden();
        messages.WriteLine();

        if (!string.Equals(first, second, StringComparison.Ordinal))
        {
            messages.WriteLine("Die beiden Eingaben sind nicht gleich. Es wurde nichts erzeugt.");

            return null;
        }

        return first;
    }

    /// <summary>Reads a line from the keyboard without showing it.</summary>
    /// <returns>What was typed, without the closing return.</returns>
    /// <remarks>
    /// <c>intercept: true</c> is the point: the key is read and not echoed, so
    /// the password does not stand in the window, is not scrolled back to and is
    /// not in a screenshot of the terminal. Backspace works, because a password
    /// nobody can see is one that is mistyped.
    /// </remarks>
    private static string ReadHidden()
    {
        var typed = new List<char>();

        while (true)
        {
            var key = Console.ReadKey(intercept: true);

            if (key.Key == ConsoleKey.Enter)
            {
                var password = new string([.. typed]);

                // The list is emptied instead of being left to the collector.
                // It changes little against somebody who reads this process's
                // memory, and it costs nothing.
                typed.Clear();

                return password;
            }

            if (key.Key == ConsoleKey.Backspace)
            {
                if (typed.Count > 0)
                {
                    typed.RemoveAt(typed.Count - 1);
                }

                continue;
            }

            // Everything without a character behind it, the arrow keys and the
            // function keys, is not part of a password.
            if (!char.IsControl(key.KeyChar))
            {
                typed.Add(key.KeyChar);
            }
        }
    }
}
