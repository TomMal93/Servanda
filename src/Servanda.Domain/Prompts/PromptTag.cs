namespace Servanda.Domain.Prompts;

public sealed class PromptTag(string promptId, string tagId)
{
    public string PromptId { get; private set; } = promptId;

    public string TagId { get; private set; } = tagId;
}
