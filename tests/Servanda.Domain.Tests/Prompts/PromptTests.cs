using Servanda.Domain.Prompts;

namespace Servanda.Domain.Tests.Prompts;

public sealed class PromptTests
{
    private static readonly DateTimeOffset Timestamp = new(2026, 8, 14, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void CreateBuildsDenseVariantAndVariableOrder()
    {
        var prompt = Create();

        Assert.NotNull(prompt);
        Assert.Equal([0, 1], prompt.Variants.Select(variant => variant.SortOrder));
        Assert.Equal([0], prompt.Variables.Select(variable => variable.SortOrder));
        Assert.Equal(1, prompt.Revision);
    }

    [Fact]
    public void UpdateContentKeepsIdentityOfEditedVariant()
    {
        var prompt = Create()!;
        var variantId = prompt.Variants[0].Id;

        var errors = prompt.UpdateContent(
            "Nowy tytuł",
            "Nowy opis",
            [],
            [
                new PromptVariantDraft(variantId, "Krótki", null, "Treść {{temat}} po zmianie"),
                new PromptVariantDraft(null, "Nowy", null, "Inna treść {{temat}}"),
            ],
            [new PromptVariableDraft(null, "temat", "Temat", "", true, false)],
            allowUnusedVariables: false,
            Timestamp.AddMinutes(1),
            () => Guid.NewGuid().ToString("N"));

        Assert.Empty(errors);
        Assert.Equal(variantId, prompt.Variants[0].Id);
        Assert.Equal(2, prompt.Revision);
    }

    [Fact]
    public void UpdateContentRejectsUndefinedPlaceholder()
    {
        var prompt = Create()!;

        var errors = prompt.UpdateContent(
            "Tytuł",
            "Opis",
            [],
            [new PromptVariantDraft(null, "Wariant", null, "Treść {{brakujaca}}")],
            [new PromptVariableDraft(null, "temat", "Temat", "", false, false)],
            allowUnusedVariables: true,
            Timestamp,
            () => Guid.NewGuid().ToString("N"));

        Assert.Contains(nameof(Prompt.Variants), errors.Keys);
        Assert.Equal(1, prompt.Revision);
    }

    [Fact]
    public void UpdateContentRequiresConfirmationForUnusedVariable()
    {
        var prompt = Create()!;
        var variants = new[] { new PromptVariantDraft(null, "Wariant", null, "Treść bez znaczników") };
        var variables = new[] { new PromptVariableDraft(null, "temat", "Temat", "", false, false) };

        var rejected = prompt.UpdateContent(
            "Tytuł",
            "Opis",
            [],
            variants,
            variables,
            allowUnusedVariables: false,
            Timestamp,
            () => Guid.NewGuid().ToString("N"));
        var confirmed = prompt.UpdateContent(
            "Tytuł",
            "Opis",
            [],
            variants,
            variables,
            allowUnusedVariables: true,
            Timestamp,
            () => Guid.NewGuid().ToString("N"));

        Assert.Contains(nameof(Prompt.Variables), rejected.Keys);
        Assert.Empty(confirmed);
        Assert.Equal(2, prompt.Revision);
    }

    [Fact]
    public void ValidateRejectsPromptWithoutVariant()
    {
        var errors = Prompt.Validate("Tytuł", "Opis", [], [], [], allowUnusedVariables: true);

        Assert.Contains(nameof(Prompt.Variants), errors.Keys);
    }

    [Fact]
    public void SetFavoriteIncrementsRevision()
    {
        var prompt = Create()!;

        prompt.SetFavorite(true, Timestamp.AddMinutes(1));

        Assert.True(prompt.IsFavorite);
        Assert.Equal(2, prompt.Revision);
    }

    [Fact]
    public void BuildSnapshotDescribesCurrentVariantsAndVariables()
    {
        var snapshot = Create()!.BuildSnapshot();

        Assert.Equal(PromptSnapshot.CurrentSchemaVersion, snapshot.SchemaVersion);
        Assert.True(snapshot.IsSupported);
        Assert.Equal(["Krótki", "Długi"], snapshot.Variants.Select(variant => variant.Name));
        Assert.Equal(["temat"], snapshot.Variables.Select(variable => variable.Name));
    }

    private static Prompt? Create() => Prompt.Create(
        "01PROMPT",
        "01AREA",
        "01CATEGORY",
        "Prompt",
        "Opis promptu",
        [],
        [
            new PromptVariantDraft(null, "Krótki", "czat", "Treść {{temat}}"),
            new PromptVariantDraft(null, "Długi", null, "Dłuższa treść {{temat}}"),
        ],
        [new PromptVariableDraft(null, "temat", "Temat", "", true, false)],
        allowUnusedVariables: false,
        0,
        Timestamp,
        () => Guid.NewGuid().ToString("N"),
        out _);
}
