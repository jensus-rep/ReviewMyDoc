// Checks the three partials of the building blocks: that they produce exactly
// the classes the building blocks under components/ define, and that the states
// of the markup, which the building blocks leave to the page, really arrive.
// The last test is the one that matters in the long run: a class name invented
// here would look right and be styled by nothing.

using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using ReviewMyDoc.Web.Pages.Shared.Ui;

namespace ReviewMyDoc.Tests.Web;

/// <summary>Tests of the partials under Pages/Shared/Ui.</summary>
public sealed class UiPartialTests : IClassFixture<WebApplicationFactory<Program>>
{
    private const string ButtonView = "/Pages/Shared/Ui/_Button.cshtml";
    private const string FieldView = "/Pages/Shared/Ui/_Field.cshtml";
    private const string RowsView = "/Pages/Shared/Ui/_Rows.cshtml";

    private readonly WebApplicationFactory<Program> _factory;

    /// <summary>Takes the application the test class shares.</summary>
    /// <param name="factory">The application under test.</param>
    public UiPartialTests(WebApplicationFactory<Program> factory) => _factory = factory;

    [Fact]
    public async Task A_button_carries_the_classes_of_the_building_block()
    {
        var html = await RenderAsync(ButtonView, new ButtonModel("Speichern"));

        Assert.Contains("class=\"button button--primary\"", html, StringComparison.Ordinal);
        Assert.Contains("type=\"submit\"", html, StringComparison.Ordinal);
        Assert.Contains(">Speichern</button>", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_button_with_a_target_becomes_a_link()
    {
        var html = await RenderAsync(
            ButtonView,
            new ButtonModel("Zur Übersicht") { Variant = ButtonVariant.Secondary, Href = "/" });

        Assert.Contains("<a class=\"button button--secondary\" href=\"/\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain("<button", html, StringComparison.Ordinal);
    }

    // The building block knows two ways of being off, one per element, and the
    // partial has to pick the right one: a link without href, a button with the
    // real attribute. A link with href and aria-disabled would still be
    // clickable.
    [Fact]
    public async Task A_disabled_link_keeps_no_target()
    {
        var html = await RenderAsync(
            ButtonView,
            new ButtonModel("Noch nicht verfügbar") { Href = "/", Disabled = true });

        Assert.Contains("aria-disabled=\"true\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain("href", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_disabled_button_carries_the_attribute()
    {
        var html = await RenderAsync(ButtonView, new ButtonModel("Speichern") { Disabled = true });

        Assert.Contains("disabled=\"disabled\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain("aria-disabled", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_field_connects_label_control_and_hint()
    {
        var html = await RenderAsync(
            FieldView,
            new FieldModel("Input.Mail", "E-Mail")
            {
                Control = FieldControl.Email,
                Hint = "Nur für die Antwort auf Ihre Anfrage.",
                Autocomplete = "email",
                Required = true,
            });

        Assert.Contains("<div class=\"field\">", html, StringComparison.Ordinal);
        Assert.Contains("class=\"field__label\" for=\"Input_Mail\"", html, StringComparison.Ordinal);
        Assert.Contains("id=\"Input_Mail\"", html, StringComparison.Ordinal);
        Assert.Contains("name=\"Input.Mail\"", html, StringComparison.Ordinal);
        Assert.Contains("type=\"email\"", html, StringComparison.Ordinal);
        Assert.Contains("autocomplete=\"email\"", html, StringComparison.Ordinal);
        Assert.Contains("required=\"required\"", html, StringComparison.Ordinal);
        Assert.Contains("aria-describedby=\"Input_Mail-hint\"", html, StringComparison.Ordinal);
        Assert.Contains("class=\"field__hint\" id=\"Input_Mail-hint\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain("aria-invalid", html, StringComparison.Ordinal);
    }

    // With an error the frame turns red, the control says it was refused, and
    // the reader is pointed at hint and error in that order, as
    // components/field/README.md asks.
    [Fact]
    public async Task A_refused_field_names_hint_and_error_in_this_order()
    {
        var html = await RenderAsync(
            FieldView,
            new FieldModel("Titel", "Titel")
            {
                Hint = "Höchstens 120 Zeichen.",
                Error = "Der Titel fehlt.",
            });

        Assert.Contains("<div class=\"field field--invalid\">", html, StringComparison.Ordinal);
        Assert.Contains("aria-describedby=\"Titel-hint Titel-error\"", html, StringComparison.Ordinal);
        Assert.Contains("aria-invalid=\"true\"", html, StringComparison.Ordinal);
        Assert.Contains("class=\"field__error\" id=\"Titel-error\"", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_field_renders_every_kind_of_control()
    {
        var multiLine = await RenderAsync(
            FieldView,
            new FieldModel("Text", "Text") { Control = FieldControl.MultiLine, Rows = 8, Value = "Hallo" });

        var choice = await RenderAsync(
            FieldView,
            new FieldModel("Art", "Art")
            {
                Control = FieldControl.Choice,
                Value = "bericht",
                Options = [new ChoiceOption("angebot", "Angebot"), new ChoiceOption("bericht", "Bericht")],
            });

        var file = await RenderAsync(
            FieldView,
            new FieldModel("Anhang", "Anhang") { Control = FieldControl.File, Accept = ".pdf" });

        Assert.Contains("<textarea class=\"field__input\"", multiLine, StringComparison.Ordinal);
        Assert.Contains("rows=\"8\"", multiLine, StringComparison.Ordinal);
        Assert.Contains(">Hallo</textarea>", multiLine, StringComparison.Ordinal);

        Assert.Contains("<select class=\"field__input\"", choice, StringComparison.Ordinal);
        Assert.Contains("<option value=\"bericht\" selected", choice, StringComparison.Ordinal);
        Assert.Contains("<option value=\"angebot\">Angebot</option>", choice, StringComparison.Ordinal);

        Assert.Contains("type=\"file\"", file, StringComparison.Ordinal);
        Assert.Contains("accept=\".pdf\"", file, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_list_renders_every_part_of_an_item()
    {
        var html = await RenderAsync(
            RowsView,
            new RowsModel(
            [
                new RowModel("Einleitung")
                {
                    Number = "01",
                    Text = "Worum es geht.",
                    Action = new RowActionModel("Ansehen", "/dokumente/1"),
                },
            ])
            { Ordered = true });

        Assert.Contains("<ol class=\"rows\">", html, StringComparison.Ordinal);
        Assert.Contains("<li class=\"rows__item\">", html, StringComparison.Ordinal);
        Assert.Contains("<span class=\"rows__number\">01</span>", html, StringComparison.Ordinal);
        Assert.Contains("<span class=\"rows__title\">Einleitung</span>", html, StringComparison.Ordinal);
        Assert.Contains("<p class=\"rows__text\">Worum es geht.</p>", html, StringComparison.Ordinal);
        Assert.Contains("<a class=\"rows__action\" href=\"/dokumente/1\">Ansehen</a>", html, StringComparison.Ordinal);
    }

    // Every part except the title may be missing, and a missing part leaves no
    // empty element behind that the building block would still measure.
    [Fact]
    public async Task A_list_without_an_order_leaves_out_what_it_does_not_have()
    {
        var html = await RenderAsync(RowsView, new RowsModel([new RowModel("Einleitung")]));

        Assert.Contains("<ul class=\"rows\">", html, StringComparison.Ordinal);
        Assert.Contains("<span class=\"rows__title\">Einleitung</span>", html, StringComparison.Ordinal);
        Assert.DoesNotContain("rows__number", html, StringComparison.Ordinal);
        Assert.DoesNotContain("rows__text", html, StringComparison.Ordinal);
        Assert.DoesNotContain("rows__action", html, StringComparison.Ordinal);
    }

    // The assurance the whole frame rests on: a partial invents no class of its
    // own. Every class it writes has to stand in the css of its building block,
    // otherwise it is styled by nothing and nobody notices until it is seen.
    [Theory]
    [InlineData(ButtonView, "button")]
    [InlineData(FieldView, "field")]
    [InlineData(RowsView, "rows")]
    public async Task A_partial_writes_only_classes_its_building_block_defines(string viewPath, string component)
    {
        var css = await File.ReadAllTextAsync(
            Path.Combine(RepositoryRoot(), "components", component, $"{component}.css"),
            TestContext.Current.CancellationToken);

        foreach (var model in ModelsOf(viewPath))
        {
            var html = await RenderAsync(viewPath, model);

            foreach (var name in ClassNamesOf(html))
            {
                Assert.Contains($".{name}", css, StringComparison.Ordinal);
            }
        }
    }

    /// <summary>Every shape of a partial that has to be checked for its classes.</summary>
    private static IEnumerable<object> ModelsOf(string viewPath) => viewPath switch
    {
        ButtonView =>
        [
            new ButtonModel("Speichern"),
            new ButtonModel("Zurück") { Variant = ButtonVariant.Secondary, Href = "/" },
        ],
        FieldView =>
        [
            new FieldModel("Titel", "Titel") { Hint = "Hinweis", Error = "Fehler" },
            new FieldModel("Text", "Text") { Control = FieldControl.MultiLine },
            new FieldModel("Art", "Art") { Control = FieldControl.Choice, Options = [new ChoiceOption("a", "A")] },
        ],
        _ =>
        [
            new RowsModel(
            [
                new RowModel("Einleitung")
                {
                    Number = "01",
                    Text = "Worum es geht.",
                    Action = new RowActionModel("Ansehen", "/"),
                },
            ]),
        ],
    };

    private static IEnumerable<string> ClassNamesOf(string html) =>
        Regex.Matches(html, "class=\"([^\"]*)\"")
            .SelectMany(match => match.Groups[1].Value.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            .Distinct(StringComparer.Ordinal);

    /// <summary>Walks up from the test output until the solution file appears.</summary>
    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "ReviewMyDoc.sln")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);
        return directory.FullName;
    }

    private async Task<string> RenderAsync(string viewPath, object model)
    {
        using var scope = _factory.Services.CreateScope();

        return await RazorPartial.RenderAsync(scope.ServiceProvider, viewPath, model);
    }
}
