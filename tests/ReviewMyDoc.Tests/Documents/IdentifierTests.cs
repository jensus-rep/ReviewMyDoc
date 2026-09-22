// The rules an identifier of the model keeps. They are checked on the two
// identifier types rather than through a document, because everything else in
// the application trusts that a value which exists as one of these types can be
// put into a path without the object store refusing it.

using ReviewMyDoc.Core.Documents;

namespace ReviewMyDoc.Tests.Documents;

/// <summary>Holds the identifier types to what docs/Datenmodell.md allows.</summary>
public sealed class IdentifierTests
{
    // The alphabet of docs/Datenmodell.md, section Ablage. The underscore is in
    // it, the hyphen and the dot are not, and neither is anything that would
    // have to be escaped in a URL.
    [Theory]
    [InlineData("d7kq2fr")]
    [InlineData("s_1a2b")]
    [InlineData("a")]
    [InlineData("0123456789")]
    [InlineData("_")]
    public void An_identifier_of_the_permitted_alphabet_is_accepted(string value)
    {
        Assert.Equal(value, new DocumentIdentifier(value).Value);
        Assert.Equal(value, new SectionIdentifier(value).Value);
    }

    // The upper case letter is the one that matters: the object store refuses a
    // path that carries one, because blob names tell case apart and a Windows
    // file system does not. Catching it here means the refusal happens where the
    // identifier is made and not when a document is saved.
    [Theory]
    [InlineData("")]
    [InlineData("D7kq2fr")]
    [InlineData("d7kq2fR")]
    [InlineData("s-1a2b")]
    [InlineData("s.1a2b")]
    [InlineData("s/1a2b")]
    [InlineData("s 1a2b")]
    [InlineData("äöü")]
    public void An_identifier_outside_the_permitted_alphabet_is_refused(string value)
    {
        Assert.ThrowsAny<ArgumentException>(() => new DocumentIdentifier(value));
        Assert.ThrowsAny<ArgumentException>(() => new SectionIdentifier(value));
    }

    // Drawn and not counted up, so that nothing about the number of documents or
    // the order they were made in can be read out of one, and so that one
    // identifier says nothing about the next.
    [Fact]
    public void Drawn_identifiers_differ()
    {
        var drawn = new HashSet<string>(StringComparer.Ordinal);

        for (var attempt = 0; attempt < 500; attempt++)
        {
            Assert.True(drawn.Add(DocumentIdentifier.Draw().Value), "A drawn identifier appeared twice.");
            Assert.True(drawn.Add(SectionIdentifier.Draw().Value), "A drawn identifier appeared twice.");
        }
    }

    [Fact]
    public void A_drawn_identifier_keeps_to_the_alphabet()
    {
        for (var attempt = 0; attempt < 200; attempt++)
        {
            var drawn = SectionIdentifier.Draw().Value;

            Assert.NotEmpty(drawn);
            Assert.All(drawn, character =>
                Assert.True(character is >= 'a' and <= 'z' or >= '0' and <= '9', $"'{drawn}' is not drawn from the alphabet."));
        }
    }

    // Two identifiers with the same text name the same thing; the types compare
    // by value, because the whole model looks a section up by its identifier.
    [Fact]
    public void Identifiers_compare_by_their_value()
    {
        Assert.Equal(new SectionIdentifier("s_1a2b"), new SectionIdentifier("s_1a2b"));
        Assert.NotEqual(new SectionIdentifier("s_1a2b"), new SectionIdentifier("s_3c4d"));
        Assert.Equal("s_1a2b", new SectionIdentifier("s_1a2b").ToString());
    }
}
