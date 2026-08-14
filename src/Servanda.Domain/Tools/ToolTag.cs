namespace Servanda.Domain.Tools;

public sealed class ToolTag(string toolId, string tagId)
{
    public string ToolId { get; private set; } = toolId;

    public string TagId { get; private set; } = tagId;
}
