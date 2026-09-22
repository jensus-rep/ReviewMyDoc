// The contract every implementation of IObjectStore has to keep. This is the
// single place where the assurances of docs/Datenmodell.md are checked against
// a store, so every implementation inherits the check instead of repeating it
// and a case missing here is missing everywhere.

using ReviewMyDoc.Core.Storage;

namespace ReviewMyDoc.Tests.Storage;

/// <summary>
/// Checks one implementation of <see cref="IObjectStore"/> against everything
/// the interface and <c>docs/Datenmodell.md</c> promise.
/// </summary>
/// <remarks>
/// <para>
/// The class is abstract and names neither a directory nor a blob; it only
/// touches the store handed to it by <see cref="CreateStore"/>. That is what
/// lets the Azure implementation and the local one be measured by the same
/// yardstick, and what makes a third implementation cost one derived class.
/// </para>
/// <para>
/// Every test here is a promise both implementations have to keep, so each one
/// says in a comment which sentence of the interface or of
/// <c>docs/Datenmodell.md</c> it holds the store to, or - for the cases beyond
/// the assurances - where the two storages would otherwise drift apart.
/// </para>
/// </remarks>
public abstract class ObjectStoreContractTests
{
    /// <summary>The entry of the model that carries a version stamp worth guarding.</summary>
    private const string DocumentPath = "documents/d7Kq2fR/document.json";

    /// <summary>The append-only entry of the model.</summary>
    private const string AuditPath = "documents/d7Kq2fR/audit.log";

    /// <summary>The prefix under which the section texts of one document live.</summary>
    private const string SectionPrefix = "documents/d7Kq2fR/sections/";

    /// <summary>
    /// The token handed to every call. Cancellation is not part of this
    /// contract: what a store does with a token that is already cancelled is
    /// decided by the runtime and not by <c>docs/Datenmodell.md</c>.
    /// </summary>
    private static CancellationToken Token => CancellationToken.None;

    /// <summary>
    /// Creates the store under test over storage that holds nothing yet.
    /// </summary>
    /// <returns>The store the calling test works against.</returns>
    /// <remarks>
    /// The derived class is the only place that knows what the store is built
    /// on, which is what keeps this class free of any directory or blob. It is
    /// called once per test, and xUnit builds a fresh instance of the test class
    /// for every test, so no test sees what another one wrote.
    /// </remarks>
    protected abstract IObjectStore CreateStore();

    // Assurance 1 of docs/Datenmodell.md in its simplest form: a read hands out
    // content and version together, because a later write has to give the
    // version back.
    [Fact]
    public async Task Creating_an_entry_makes_it_readable_with_its_content_and_a_version()
    {
        var store = CreateStore();

        var version = await CreateAsync(store, DocumentPath, "erste Fassung");

        var entry = await ExpectFoundAsync(store, DocumentPath);
        Assert.Equal("erste Fassung", entry.Content);
        Assert.Equal(version, entry.ETag);
    }

    // Assurance 1 needs a version that moves: if the stamp stayed the same, a
    // stale write would pass the condition and overwrite a change silently.
    // Checked with new content on purpose - whether writing the identical
    // content also changes the stamp is left open, because Azure hands out a
    // fresh ETag while the local store derives it from the content
    // (docs/Datenmodell.md, section Lokal und in Azure), and the application
    // never depends on the difference.
    [Fact]
    public async Task Writing_new_content_changes_the_version()
    {
        var store = CreateStore();
        var firstVersion = await CreateAsync(store, DocumentPath, "erste Fassung");

        var secondVersion = await ExpectWrittenAsync(
            store,
            DocumentPath,
            "zweite Fassung",
            WriteCondition.MustMatch(firstVersion));

        Assert.NotEqual(firstVersion, secondVersion);
        var entry = await ExpectFoundAsync(store, DocumentPath);
        Assert.Equal("zweite Fassung", entry.Content);
        Assert.Equal(secondVersion, entry.ETag);
    }

    // The heart of assurance 1: a write carrying a version that is no longer
    // current is a conflict, never a silent overwrite. The read afterwards is
    // the actual point of the test - a store that reports the conflict but has
    // already written would lose the other change all the same.
    [Fact]
    public async Task Writing_with_a_version_that_is_no_longer_current_is_a_conflict_and_changes_nothing()
    {
        var store = CreateStore();
        var firstVersion = await CreateAsync(store, DocumentPath, "erste Fassung");
        var secondVersion = await ExpectWrittenAsync(
            store,
            DocumentPath,
            "zweite Fassung",
            WriteCondition.MustMatch(firstVersion));

        var result = await store.WriteAsync(
            DocumentPath,
            "dritte Fassung",
            WriteCondition.MustMatch(firstVersion),
            Token);

        Assert.IsType<ObjectWriteResult.Conflict>(result);
        var entry = await ExpectFoundAsync(store, DocumentPath);
        Assert.Equal("zweite Fassung", entry.Content);
        Assert.Equal(secondVersion, entry.ETag);
    }

    // Assurance 2: versions/{n}.json is written once and never again. The
    // condition has to be checked where the write happens, so this is the test
    // that keeps a frozen version unchangeable.
    [Fact]
    public async Task Creating_an_entry_that_is_already_there_is_a_conflict_and_changes_nothing()
    {
        var store = CreateStore();
        const string versionPath = "documents/d7Kq2fR/versions/4.json";
        var version = await CreateAsync(store, versionPath, "eingefrorener Stand");

        var result = await store.WriteAsync(
            versionPath,
            "geänderter Stand",
            WriteCondition.MustNotExist,
            Token);

        Assert.IsType<ObjectWriteResult.Conflict>(result);
        var entry = await ExpectFoundAsync(store, versionPath);
        Assert.Equal("eingefrorener Stand", entry.Content);
        Assert.Equal(version, entry.ETag);
    }

    // WriteCondition.MustMatch demands that the entry exists and still carries
    // the version. Without this test a store could treat a deleted entry as
    // "nothing to conflict with" and resurrect it, which would undo the delete
    // a second writer had just made.
    [Fact]
    public async Task Writing_with_a_version_against_a_missing_entry_is_a_conflict()
    {
        var store = CreateStore();
        var version = await CreateAsync(store, DocumentPath, "erste Fassung");
        Assert.Equal(ObjectDeleteResult.Deleted, await store.DeleteAsync(DocumentPath, Token));

        var result = await store.WriteAsync(
            DocumentPath,
            "zweite Fassung",
            WriteCondition.MustMatch(version),
            Token);

        Assert.IsType<ObjectWriteResult.Conflict>(result);
        Assert.IsType<ObjectReadResult.NotFound>(await store.ReadAsync(DocumentPath, Token));
    }

    // WriteCondition.Unconditional is the third of the three intents and would
    // otherwise go unchecked. It has to work in both directions, so this test
    // and the next one pin it on an existing and on a missing entry.
    [Fact]
    public async Task Writing_unconditionally_replaces_what_is_there()
    {
        var store = CreateStore();
        await CreateAsync(store, DocumentPath, "erste Fassung");

        await ExpectWrittenAsync(store, DocumentPath, "zweite Fassung", WriteCondition.Unconditional);

        var entry = await ExpectFoundAsync(store, DocumentPath);
        Assert.Equal("zweite Fassung", entry.Content);
    }

    [Fact]
    public async Task Writing_unconditionally_creates_an_entry_that_is_not_there()
    {
        var store = CreateStore();

        var version = await ExpectWrittenAsync(
            store,
            DocumentPath,
            "erste Fassung",
            WriteCondition.Unconditional);

        var entry = await ExpectFoundAsync(store, DocumentPath);
        Assert.Equal("erste Fassung", entry.Content);
        Assert.Equal(version, entry.ETag);
    }

    // A missing entry is an ordinary answer and not a failure: a document
    // without feedback simply has no feedback entries. A store that threw here
    // would force every caller into a try block.
    [Fact]
    public async Task Reading_an_entry_that_is_not_there_reports_not_found()
    {
        var store = CreateStore();

        var result = await store.ReadAsync(DocumentPath, Token);

        Assert.IsType<ObjectReadResult.NotFound>(result);
    }

    // Listing is the only question the application asks across several entries,
    // and the interface promises ascending ordinal order so that neither the
    // callers nor the second implementation have to sort. The names are chosen
    // so that ordinal and culture-aware order differ: ordinally the capital S
    // comes first, under a culture-aware comparison it would come last.
    [Fact]
    public async Task Listing_a_prefix_returns_the_matching_paths_in_ascending_ordinal_order()
    {
        var store = CreateStore();
        await CreateAsync(store, SectionPrefix + "s_3c4d.md", "dritter Abschnitt");
        await CreateAsync(store, SectionPrefix + "S_0zzz.md", "erster Abschnitt");
        await CreateAsync(store, SectionPrefix + "s_1a2b.md", "zweiter Abschnitt");

        var paths = await store.ListAsync(SectionPrefix, Token);

        Assert.Equal(
            new[]
            {
                SectionPrefix + "S_0zzz.md",
                SectionPrefix + "s_1a2b.md",
                SectionPrefix + "s_3c4d.md",
            },
            paths);
    }

    // The listing must not reach past its prefix. The trap is the second
    // document whose identifier begins with the characters of the first: a
    // store that compared segment by segment, or that listed a whole directory
    // level, would hand out entries of a document the caller never asked about.
    [Fact]
    public async Task Listing_a_prefix_leaves_out_entries_that_do_not_begin_with_it()
    {
        var store = CreateStore();
        await CreateAsync(store, SectionPrefix + "s_1a2b.md", "Abschnitt");
        await CreateAsync(store, DocumentPath, "Metadaten");
        await CreateAsync(store, "documents/d7Kq2fRZZ/sections/s_1a2b.md", "fremder Abschnitt");

        var paths = await store.ListAsync(SectionPrefix, Token);

        Assert.Equal(new[] { SectionPrefix + "s_1a2b.md" }, paths);
    }

    // The interface says the match is by characters and not by path segments,
    // and that deeper entries are included. Both halves are traps for a caller
    // who forgets the trailing slash, so both are nailed down here rather than
    // left to whichever implementation is written first.
    [Fact]
    public async Task Listing_a_prefix_without_a_trailing_slash_also_matches_longer_names()
    {
        var store = CreateStore();
        await CreateAsync(store, DocumentPath, "Metadaten");
        await CreateAsync(store, SectionPrefix + "s_1a2b.md", "Abschnitt");
        await CreateAsync(store, "documents/d7Kq2fRZZ/document.json", "fremde Metadaten");

        var paths = await store.ListAsync("documents/d7Kq2fR", Token);

        Assert.Equal(
            new[]
            {
                DocumentPath,
                SectionPrefix + "s_1a2b.md",
                "documents/d7Kq2fRZZ/document.json",
            },
            paths);
    }

    // An empty result is not an error: a document without feedback is the
    // normal state, and the overview asks for prefixes that hold nothing yet.
    [Fact]
    public async Task Listing_a_prefix_that_matches_nothing_returns_an_empty_list()
    {
        var store = CreateStore();
        await CreateAsync(store, DocumentPath, "Metadaten");

        var paths = await store.ListAsync("documents/d7Kq2fR/feedback/", Token);

        Assert.Empty(paths);
    }

    // The audit log is written before anybody knows whether it exists. If the
    // first append did not create the entry, every caller would need a check
    // that is a race against the next event.
    [Fact]
    public async Task Appending_creates_the_entry_on_the_first_call()
    {
        var store = CreateStore();

        await store.AppendLineAsync(AuditPath, "{\"event\":\"DocumentCreated\"}", Token);

        var entry = await ExpectFoundAsync(store, AuditPath);
        Assert.Equal(new[] { "{\"event\":\"DocumentCreated\"}" }, Lines(entry.Content));
    }

    // Appending is its own operation exactly because read, extend and write
    // back would lose an event. A store that replaced the entry would pass
    // every other test in this class and still throw away the audit trail.
    [Fact]
    public async Task Appending_keeps_the_lines_that_are_already_there()
    {
        var store = CreateStore();
        await store.AppendLineAsync(AuditPath, "{\"event\":\"DocumentCreated\"}", Token);

        await store.AppendLineAsync(AuditPath, "{\"event\":\"ReviewSent\"}", Token);
        await store.AppendLineAsync(AuditPath, "{\"event\":\"FeedbackReceived\"}", Token);

        var entry = await ExpectFoundAsync(store, AuditPath);
        Assert.Equal(
            new[]
            {
                "{\"event\":\"DocumentCreated\"}",
                "{\"event\":\"ReviewSent\"}",
                "{\"event\":\"FeedbackReceived\"}",
            },
            Lines(entry.Content));
    }

    // The caller hands over a line without a break and the store adds the
    // separator, so the separator is the store's business alone - and the two
    // storages would part ways here without a word: a local store that writes
    // through the platform default would end the line with a carriage return
    // and a line feed on Windows, an append blob would not. The log would then
    // depend on where the application happens to run. Whether the last line
    // carries a trailing separator stays open on purpose; both forms read as
    // JSON Lines and neither implementation is forced into extra bookkeeping.
    [Fact]
    public async Task Appending_separates_the_lines_with_a_single_line_feed()
    {
        var store = CreateStore();

        await store.AppendLineAsync(AuditPath, "{\"event\":\"DocumentCreated\"}", Token);
        await store.AppendLineAsync(AuditPath, "{\"event\":\"ReviewSent\"}", Token);

        var entry = await ExpectFoundAsync(store, AuditPath);
        Assert.DoesNotContain("\r", entry.Content);
        Assert.StartsWith("{\"event\":\"DocumentCreated\"}\n{\"event\":\"ReviewSent\"}", entry.Content);
    }

    // A line that carries a break of its own would silently become two events
    // and make the log unreadable, which the interface therefore rejects. The
    // entry is checked afterwards because rejecting after writing would be no
    // rejection at all.
    [Fact]
    public async Task Appending_a_line_that_carries_a_line_break_is_rejected()
    {
        var store = CreateStore();

        await Assert.ThrowsAnyAsync<ArgumentException>(
            () => store.AppendLineAsync(AuditPath, "{\"event\":\"A\"}\n{\"event\":\"B\"}", Token));

        Assert.IsType<ObjectReadResult.NotFound>(await store.ReadAsync(AuditPath, Token));
    }

    [Fact]
    public async Task Deleting_an_entry_makes_it_unreadable()
    {
        var store = CreateStore();
        await CreateAsync(store, DocumentPath, "erste Fassung");

        var result = await store.DeleteAsync(DocumentPath, Token);

        Assert.Equal(ObjectDeleteResult.Deleted, result);
        Assert.IsType<ObjectReadResult.NotFound>(await store.ReadAsync(DocumentPath, Token));
    }

    // Assurance 1 says the delete carries no condition and that deleting twice
    // stays without effect: after a cancelled operation a caller cleans up
    // again, and the second attempt has to report NotFound instead of failing.
    [Fact]
    public async Task Deleting_twice_stays_harmless_and_reports_not_found()
    {
        var store = CreateStore();
        await CreateAsync(store, DocumentPath, "erste Fassung");
        Assert.Equal(ObjectDeleteResult.Deleted, await store.DeleteAsync(DocumentPath, Token));

        var result = await store.DeleteAsync(DocumentPath, Token);

        Assert.Equal(ObjectDeleteResult.NotFound, result);
    }

    // Assurance 6: section identifiers do not die. A delete removes the one
    // named entry and never everything under a prefix, which is what keeps the
    // feedback of a deleted section readable so it can be marked as orphaned.
    [Fact]
    public async Task Deleting_one_entry_leaves_its_neighbours_alone()
    {
        var store = CreateStore();
        const string feedbackPath = "documents/d7Kq2fR/feedback/f_4tZ.json";
        await CreateAsync(store, SectionPrefix + "s_1a2b.md", "erster Abschnitt");
        await CreateAsync(store, SectionPrefix + "s_3c4d.md", "zweiter Abschnitt");
        await CreateAsync(store, feedbackPath, "Rückmeldung");

        Assert.Equal(
            ObjectDeleteResult.Deleted,
            await store.DeleteAsync(SectionPrefix + "s_1a2b.md", Token));

        Assert.Equal(new[] { SectionPrefix + "s_3c4d.md" }, await store.ListAsync(SectionPrefix, Token));
        var feedback = await ExpectFoundAsync(store, feedbackPath);
        Assert.Equal("Rückmeldung", feedback.Content);
    }

    // Content travels as text and is stored as UTF-8 without a byte order mark.
    // This is where two storages drift apart without anybody noticing until a
    // document is read back: a local store that writes through the platform
    // code page mangles the umlauts, one that adds a byte order mark prepends
    // an invisible character that breaks the JSON reader on the first read, and
    // a store that normalises line endings silently rewrites a section text.
    [Fact]
    public async Task Content_with_umlauts_and_special_characters_comes_back_unchanged()
    {
        var store = CreateStore();
        const string content =
            "Gutachten Musterstraße\nÄnderung: 50 % der Fläche, „Zitat“\r\nEmoji 🏛 & <tag>";

        await CreateAsync(store, SectionPrefix + "s_1a2b.md", content);

        var entry = await ExpectFoundAsync(store, SectionPrefix + "s_1a2b.md");
        Assert.Equal(content, entry.Content);
    }

    // An empty section text is a real state - the owner writes a heading before
    // he writes the text. Empty is not the same as missing, so the entry has to
    // exist, has to read back as empty and has to carry a version; a local
    // store that derives the version from the content must not end up with an
    // empty stamp, which the constructor of ETag refuses.
    [Fact]
    public async Task An_entry_may_carry_empty_content()
    {
        var store = CreateStore();

        var version = await CreateAsync(store, SectionPrefix + "s_1a2b.md", string.Empty);

        var entry = await ExpectFoundAsync(store, SectionPrefix + "s_1a2b.md");
        Assert.Equal(string.Empty, entry.Content);
        Assert.Equal(version, entry.ETag);
        Assert.Equal(new[] { SectionPrefix + "s_1a2b.md" }, await store.ListAsync(SectionPrefix, Token));
    }

    // A path longer than any the application draws today, because the limits of
    // the two storages are not the same: a blob name may run to a thousand
    // characters, while a path on Windows stops at far less unless the
    // implementation asks for more. The length is chosen to stay well inside
    // both, so the test states a contract that can be kept instead of a limit
    // of one machine.
    [Fact]
    public async Task A_long_path_is_stored_and_read_back()
    {
        var store = CreateStore();
        const string longPath =
            "documents/d7Kq2fR8mN3pL5vT/sections/"
            + "a-section-identifier-that-is-far-longer-than-any-this-application-draws"
            + ".md";

        await CreateAsync(store, longPath, "Abschnitt mit langem Pfad");

        var entry = await ExpectFoundAsync(store, longPath);
        Assert.Equal("Abschnitt mit langem Pfad", entry.Content);
        Assert.Equal(new[] { longPath }, await store.ListAsync("documents/d7Kq2fR8mN3pL5vT/", Token));
    }

    // The interface compares paths as ordinal strings, so two paths that differ
    // only in case are two entries. This is the sharpest difference between the
    // two storages: blob names are case sensitive, a Windows file system is
    // not. Left untested, identifiers that differ only in case - and they are
    // drawn at random - would overwrite each other in the local store while
    // being kept apart in Azure.
    [Fact]
    public async Task Paths_that_differ_only_in_case_are_different_entries()
    {
        var store = CreateStore();
        const string lowerCasePath = "documents/d7kq2fr/document.json";
        const string upperCasePath = "documents/D7Kq2fR/document.json";

        await CreateAsync(store, lowerCasePath, "Dokument des einen");
        await CreateAsync(store, upperCasePath, "Dokument des anderen");

        Assert.Equal("Dokument des einen", (await ExpectFoundAsync(store, lowerCasePath)).Content);
        Assert.Equal("Dokument des anderen", (await ExpectFoundAsync(store, upperCasePath)).Content);
        Assert.Equal(ObjectDeleteResult.Deleted, await store.DeleteAsync(lowerCasePath, Token));
        Assert.IsType<ObjectReadResult.Found>(await store.ReadAsync(upperCasePath, Token));
    }

    /// <summary>Writes an entry that has to be new and hands back its version.</summary>
    private static async Task<ETag> CreateAsync(IObjectStore store, string path, string content) =>
        await ExpectWrittenAsync(store, path, content, WriteCondition.MustNotExist);

    /// <summary>Writes an entry, insisting that the write took effect.</summary>
    private static async Task<ETag> ExpectWrittenAsync(
        IObjectStore store,
        string path,
        string content,
        WriteCondition condition)
    {
        var result = await store.WriteAsync(path, content, condition, Token);

        return Assert.IsType<ObjectWriteResult.Written>(result).ETag;
    }

    /// <summary>Reads an entry, insisting that it is there.</summary>
    private static async Task<ObjectReadResult.Found> ExpectFoundAsync(IObjectStore store, string path) =>
        Assert.IsType<ObjectReadResult.Found>(await store.ReadAsync(path, Token));

    /// <summary>
    /// Splits appended content into its lines and drops the empty piece a
    /// trailing separator leaves behind, so a test can state the order of the
    /// events without deciding whether the last line ends with a separator.
    /// </summary>
    private static string[] Lines(string content)
    {
        var lines = content.Split('\n');

        return lines.Length > 0 && lines[^1].Length == 0
            ? lines[..^1]
            : lines;
    }
}
