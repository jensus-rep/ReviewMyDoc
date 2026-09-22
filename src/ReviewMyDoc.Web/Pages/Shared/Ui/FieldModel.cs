// Model of the partial Ui/_Field: label, control, hint and error message of one
// form field, as a typed record. It also computes the connection between the
// three texts (for, id, aria-describedby), because the building block
// components/field only gives them their place and leaves that connection to
// the markup.

namespace ReviewMyDoc.Web.Pages.Shared.Ui;

/// <summary>The six kinds of control the building block field covers.</summary>
public enum FieldControl
{
    /// <summary>A single line of text.</summary>
    Text,

    /// <summary>An e-mail address.</summary>
    Email,

    /// <summary>A password, never prefilled.</summary>
    Password,

    /// <summary>Several lines of text, growing downwards only.</summary>
    MultiLine,

    /// <summary>A file to upload.</summary>
    File,

    /// <summary>A closed choice between a few named values.</summary>
    Choice,
}

/// <summary>One entry of a closed choice.</summary>
/// <param name="Value">The value that is submitted.</param>
/// <param name="Label">The caption the reader sees, in German.</param>
public sealed record ChoiceOption(string Value, string Label);

/// <summary>
/// One form field. <paramref name="Name"/> is the name of the form value and
/// therefore what the model binding of Razor Pages reads; the id is derived
/// from it, so label and control belong together without the page repeating
/// itself.
/// </summary>
/// <param name="Name">Name of the form value, for example <c>Input.Titel</c>.</param>
/// <param name="Label">Caption of the field, in German.</param>
public sealed record FieldModel(string Name, string Label)
{
    /// <summary>Which kind of control to render. A line of text by default.</summary>
    public FieldControl Control { get; init; } = FieldControl.Text;

    /// <summary>
    /// The id of the control. Derived from the name, with the separator of a
    /// bound name replaced by an underscore: the same id the tag helpers of
    /// Razor Pages generate, and one that needs no escaping in a css selector.
    /// </summary>
    public string Id { get; init; } = Name.Replace('.', '_');

    /// <summary>The current value. Never set for a password or a file.</summary>
    public string? Value { get; init; }

    /// <summary>Help below the field, for what the reader cannot guess.</summary>
    public string? Hint { get; init; }

    /// <summary>The error of the last attempt, in German and as a sentence.</summary>
    public string? Error { get; init; }

    /// <summary>What the browser may fill in, for example <c>email</c>.</summary>
    public string? Autocomplete { get; init; }

    /// <summary>Which files the file control offers, for example <c>.pdf</c>.</summary>
    public string? Accept { get; init; }

    /// <summary>Whether the form cannot be submitted without this field.</summary>
    public bool Required { get; init; }

    /// <summary>Whether the control is off.</summary>
    public bool Disabled { get; init; }

    /// <summary>
    /// Visible lines of a multi line field. The building block takes the height
    /// from this attribute and not from css.
    /// </summary>
    public int Rows { get; init; } = 5;

    /// <summary>The entries of a closed choice, empty for every other kind.</summary>
    public IReadOnlyList<ChoiceOption> Options { get; init; } = [];

    /// <summary>Whether a hint is to be rendered.</summary>
    public bool HasHint => !string.IsNullOrWhiteSpace(Hint);

    /// <summary>Whether an error is to be rendered.</summary>
    public bool HasError => !string.IsNullOrWhiteSpace(Error);

    /// <summary>Id of the hint, referenced by the control.</summary>
    public string HintId => $"{Id}-hint";

    /// <summary>Id of the error message, referenced by the control.</summary>
    public string ErrorId => $"{Id}-error";

    /// <summary>The classes of the frame, red framed as soon as an error stands.</summary>
    public string CssClass => HasError ? "field field--invalid" : "field";

    /// <summary>The state of a field that was refused. Null otherwise.</summary>
    public string? AriaInvalid => HasError ? "true" : null;

    /// <summary>
    /// The ids the control is described by, hint before error, so a screen
    /// reader reads the help first and the error second. Null while there is
    /// neither, so Razor leaves the attribute out.
    /// </summary>
    public string? DescribedBy => (HasHint, HasError) switch
    {
        (true, true) => $"{HintId} {ErrorId}",
        (true, false) => HintId,
        (false, true) => ErrorId,
        _ => null,
    };

    /// <summary>The type attribute of a single line control.</summary>
    public string TypeAttribute => Control switch
    {
        FieldControl.Email => "email",
        FieldControl.Password => "password",
        FieldControl.File => "file",
        _ => "text",
    };
}
