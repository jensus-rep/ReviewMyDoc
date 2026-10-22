// Exercises real document operations and storage, including failed appends and
// rejected writes. History must describe committed changes without their content.
using System.Text.Json;
using Microsoft.Extensions.Logging;
using ReviewMyDoc.Core.Audit;
using ReviewMyDoc.Core.Documents;
using ReviewMyDoc.Core.Storage;
using ReviewMyDoc.Infrastructure.Audit;
using ReviewMyDoc.Tests.Documents;

namespace ReviewMyDoc.Tests.Audit;

/// <summary>Contracts for append-only, content-free document auditing.</summary>
public sealed class AuditLogTests : DocumentServiceTestBase
{
    private AuditLog Log => new(_objects, _clock, new RecordingAuditFailures());

    [Theory]
    [InlineData(AuditEventKind.DocumentCreated)]
    [InlineData(AuditEventKind.DocumentRenamed)]
    [InlineData(AuditEventKind.SectionAdded)]
    [InlineData(AuditEventKind.SectionRenamed)]
    [InlineData(AuditEventKind.SectionsReordered)]
    [InlineData(AuditEventKind.SectionDeleted)]
    [InlineData(AuditEventKind.SectionSplit)]
    [InlineData(AuditEventKind.TextSaved)]
    [InlineData(AuditEventKind.VersionFrozen)]
    public async Task Each_document_change_appends_one_event_after_commit(AuditEventKind kind)
    {
        var doc = await CreateDocumentAsync("Private title");
        var id = doc.Document.Id;
        if (kind == AuditEventKind.DocumentCreated)
        {
            Assert.Equal(kind, Assert.Single(await Log.ReadAsync(id, Token)).Kind);
            return;
        }
        doc = await SucceedsAsync(_service.AddSectionAsync(id, "Private heading", doc.ETag, Token));
        doc = await SucceedsAsync(_service.AddSectionAsync(id, "Other heading", doc.ETag, Token));
        var section = doc.Document.Sections[0].Id;
        var store = new DocumentStore(_objects);
        var editing = new DocumentEditingService(store, _clock, Log);
        var text = (await store.ReadSectionTextAsync(id, section, Token))!;
        var saved = Assert.IsType<ObjectWriteResult.Written>(await editing.SaveAsync(id, section, "PRIVATE TEXT", text.ETag, Token));
        doc = await SucceedsAsync(_service.LoadDocumentAsync(id, Token));
        var before = await RawAsync(id);
        var count = (await Log.ReadAsync(id, Token)).Count;
        _clock.UtcNow = Changed;

        switch (kind)
        {
            case AuditEventKind.DocumentRenamed:
                await SucceedsAsync(_service.RenameDocumentAsync(id, "Renamed private title", doc.ETag, Token));
                break;
            case AuditEventKind.SectionAdded:
                await SucceedsAsync(_service.AddSectionAsync(id, "Added heading", doc.ETag, Token));
                break;
            case AuditEventKind.SectionRenamed:
                await SucceedsAsync(_service.RenameSectionAsync(id, section, "New heading", doc.ETag, Token));
                break;
            case AuditEventKind.SectionsReordered:
                await SucceedsAsync(_service.ReorderSectionsAsync(id, doc.Document.Sections.Reverse().Select(s => s.Id).ToArray(), doc.ETag, Token));
                break;
            case AuditEventKind.SectionDeleted:
                await SucceedsAsync(_service.DeleteSectionAsync(id, section, doc.ETag, Token));
                break;
            case AuditEventKind.SectionSplit:
                await SucceedsAsync(_service.SplitSectionAsync(id, section, 1, 4, "Selected heading", doc.ETag, Token));
                break;
            case AuditEventKind.TextSaved:
                Assert.IsType<ObjectWriteResult.Written>(await editing.SaveAsync(id, section, "REVISED PRIVATE TEXT", saved.ETag, Token));
                break;
            case AuditEventKind.VersionFrozen:
                await SucceedsAsync(_service.FreezeVersionAsync(id, doc.ETag, Token));
                break;
        }

        var events = await Log.ReadAsync(id, Token);
        Assert.Equal(count + 1, events.Count);
        var entry = events[0];
        Assert.Equal(kind, entry.Kind);
        Assert.Equal(Changed, entry.OccurredAt);
        Assert.Equal(id.Value, entry.DocumentId);
        Assert.Equal(AuditActor.Owner, entry.Actor);
        Assert.Equal("owner", entry.ActorId);
        var raw = await RawAsync(id);
        Assert.StartsWith(before, raw);
        Assert.DoesNotContain("PRIVATE", raw);
        Assert.DoesNotContain("heading", raw);
        foreach (var line in raw.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            using var json = JsonDocument.Parse(line);
            Assert.Equal(JsonValueKind.String, json.RootElement.GetProperty("kind").ValueKind);
            Assert.Equal(id.Value, json.RootElement.GetProperty("documentId").GetString());
        }
    }

    [Fact]
    public async Task Stale_and_compensated_changes_leave_the_log_unchanged()
    {
        var doc = await WithThreeSectionsAsync();
        var id = doc.Document.Id;
        var before = await RawAsync(id);
        Assert.IsType<DocumentResult.Conflict>(await _service.RenameDocumentAsync(id, "Refused", new ETag("stale"), Token));
        var probe = new AuditStoreProbe(_objects) { RefuseDocumentWrites = true };
        var service = new DocumentService(new DocumentStore(probe), _clock, new AuditLog(probe, _clock, new RecordingAuditFailures()));
        Assert.IsType<DocumentResult.Conflict>(await service.AddSectionAsync(id, "Compensated", doc.ETag, Token));
        Assert.IsType<DocumentResult.Conflict>(await service.FreezeVersionAsync(id, doc.ETag, Token));
        var editing = new DocumentEditingService(new DocumentStore(_objects), _clock, Log);
        Assert.IsType<ObjectWriteResult.Conflict>(await editing.SaveAsync(id, doc.Document.Sections[0].Id, "Refused", new ETag("stale"), Token));
        Assert.Equal(before, await RawAsync(id));
        Assert.Equal(0, probe.Appends);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task A_failed_or_cancelled_append_preserves_the_change_and_logs_no_secret(bool cancelled)
    {
        var probe = new AuditStoreProbe(_objects) { FailAppends = true, CancelAppends = cancelled };
        var logger = new CapturingLogger();
        var service = new DocumentService(new DocumentStore(probe), _clock,
            new AuditLog(probe, _clock, new AuditFailureReporter(logger)));
        var created = await SucceedsAsync(service.CreateDocumentAsync("owner", "Still committed", Token));
        Assert.NotNull(await new DocumentStore(_objects).ReadAsync(created.Document.Id, Token));
        var message = Assert.Single(logger.Messages);
        Assert.Contains("DocumentCreated", message);
        Assert.Contains(created.Document.Id.Value, message);
        Assert.DoesNotContain(AuditStoreProbe.Secret, message);
        Assert.Empty(await Log.ReadAsync(created.Document.Id, Token));
        Assert.Equal(1, probe.Appends);
    }

    [Fact]
    public async Task Concurrent_events_only_append_and_equal_timestamps_keep_reverse_append_order()
    {
        var id = DocumentIdentifier.Draw();
        var probe = new AuditStoreProbe(_objects);
        var audit = new AuditLog(probe, _clock, new RecordingAuditFailures());
        await Task.WhenAll(Enumerable.Range(1, 20).Select(version =>
            audit.RecordAsync(id, AuditEventKind.VersionFrozen, Token, version: version)));
        Assert.Equal(20, (await audit.ReadAsync(id, Token)).Count);
        Assert.Equal(0, probe.AuditRewrites);
        await audit.RecordAsync(id, AuditEventKind.DocumentApproved, Token);
        await audit.RecordAsync(id, AuditEventKind.DocumentReopened, Token);
        var entries = await audit.ReadAsync(id, Token);
        Assert.Equal(AuditEventKind.DocumentReopened, entries[0].Kind);
        Assert.Equal(AuditEventKind.DocumentApproved, entries[1].Kind);
    }

    private async Task<string> RawAsync(DocumentIdentifier id) =>
        Assert.IsType<ObjectReadResult.Found>(await _objects.ReadAsync($"documents/{id}/audit.log", Token)).Content;

    private sealed class CapturingLogger : ILogger<AuditFailureReporter>
    {
        public List<string> Messages { get; } = [];
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel level, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            Messages.Add($"{formatter(state, exception)} {state} {exception}");
    }
}
