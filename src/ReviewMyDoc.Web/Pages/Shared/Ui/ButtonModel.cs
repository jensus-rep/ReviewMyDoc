// Model of the partial Ui/_Button: everything the building block button can
// show, as a typed record. It exists so that no page writes the class names of
// components/button by hand and every button of the application is built the
// same way.

namespace ReviewMyDoc.Web.Pages.Shared.Ui;

/// <summary>The two variants the building block button knows.</summary>
public enum ButtonVariant
{
    /// <summary>The one filled surface of an area, in the accent colour.</summary>
    Primary,

    /// <summary>A text link carrying a hairline frame, without a surface.</summary>
    Secondary,
}

/// <summary>What a button does when it sits inside a form.</summary>
public enum ButtonBehaviour
{
    /// <summary>Submits the surrounding form.</summary>
    Submit,

    /// <summary>Submits nothing; the page gives the button its meaning.</summary>
    Plain,
}

/// <summary>
/// A button of the application, rendered as a &lt;button&gt; or, as soon as
/// <see cref="Href"/> is set, as an &lt;a&gt;. Both carry the same classes, as
/// components/button/README.md describes.
/// </summary>
/// <param name="Label">The caption, in German and already final.</param>
public sealed record ButtonModel(string Label)
{
    /// <summary>Which of the two variants to render. Primary by default.</summary>
    public ButtonVariant Variant { get; init; } = ButtonVariant.Primary;

    /// <summary>The target of the link. Set means: render an anchor.</summary>
    public string? Href { get; init; }

    /// <summary>What the button does in a form. Only read for a button.</summary>
    public ButtonBehaviour Behaviour { get; init; } = ButtonBehaviour.Submit;

    /// <summary>Whether the control is off.</summary>
    public bool Disabled { get; init; }

    /// <summary>The classes of the building block for the chosen variant.</summary>
    public string CssClass => Variant switch
    {
        ButtonVariant.Secondary => "button button--secondary",
        _ => "button button--primary",
    };

    /// <summary>Whether this button is a link and not a form control.</summary>
    public bool IsLink => Href is not null;

    /// <summary>
    /// The target, or nothing while the link is off: a disabled link keeps no
    /// href, so it is neither clickable nor reachable with the keyboard, which
    /// is what the README of the building block asks for.
    /// </summary>
    public string? LinkHref => Disabled ? null : Href;

    /// <summary>
    /// The state of a disabled link, which has no disabled attribute of its
    /// own. Null everywhere else, so Razor leaves the attribute out.
    /// </summary>
    public string? AriaDisabled => Disabled && IsLink ? "true" : null;

    /// <summary>The type attribute of a rendered button.</summary>
    public string TypeAttribute => Behaviour == ButtonBehaviour.Submit ? "submit" : "button";
}
