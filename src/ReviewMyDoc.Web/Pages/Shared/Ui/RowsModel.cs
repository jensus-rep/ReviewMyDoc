// Model of the partial Ui/_Rows: a list of items of the same kind, separated by
// hairlines instead of cards. The list only knows its items and whether their
// order carries meaning; everything an item shows stands in RowModel.

namespace ReviewMyDoc.Web.Pages.Shared.Ui;

/// <summary>
/// A list of items of the same kind, rendered with the building block
/// components/rows.
/// </summary>
/// <param name="Items">The items, already in the order they are shown in.</param>
public sealed record RowsModel(IReadOnlyList<RowModel> Items)
{
    /// <summary>
    /// Whether the order carries meaning. True renders an ordered list, false a
    /// plain one, as components/rows/README.md asks: ol for steps with a
    /// number, ul for items without an order.
    /// </summary>
    public bool Ordered { get; init; }
}
