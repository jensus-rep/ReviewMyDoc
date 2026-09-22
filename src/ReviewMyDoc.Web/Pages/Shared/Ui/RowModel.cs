// Model of the partial Ui/_RowsItem: one item of a list. Every part except the
// title may be left out, exactly as the building block components/rows allows,
// and a part that is missing leaves no gap behind.

namespace ReviewMyDoc.Web.Pages.Shared.Ui;

/// <summary>The one action of an item, rendered as a quiet link.</summary>
/// <param name="Label">The caption, in German.</param>
/// <param name="Href">Where the action leads.</param>
public sealed record RowActionModel(string Label, string Href);

/// <summary>One item of a list.</summary>
/// <param name="Title">The title of the item, the one part that is always there.</param>
public sealed record RowModel(string Title)
{
    /// <summary>
    /// The number in front of the title, as text: it is a caption and not a
    /// value to calculate with, and a leading zero has to survive.
    /// </summary>
    public string? Number { get; init; }

    /// <summary>One sentence below the title.</summary>
    public string? Text { get; init; }

    /// <summary>The one action of the item, on the right in a wide list.</summary>
    public RowActionModel? Action { get; init; }
}
