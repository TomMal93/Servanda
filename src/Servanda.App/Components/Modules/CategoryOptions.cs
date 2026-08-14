using Servanda.Application.Catalog;

namespace Servanda.App.Components.Modules;

/// <summary>
/// Spłaszcza drzewo kategorii do listy wyboru z czytelnym wcięciem poziomu.
/// </summary>
public static class CategoryOptions
{
    public static IEnumerable<(string Id, string Label)> Flatten(
        IReadOnlyList<CategoryNode> nodes,
        int depth = 0)
    {
        foreach (var node in nodes)
        {
            var prefix = depth == 0 ? string.Empty : string.Concat(new string('—', depth), " ");
            yield return (node.Category.Id, prefix + node.Category.Name);
            foreach (var child in Flatten(node.Children, depth + 1))
            {
                yield return child;
            }
        }
    }
}
