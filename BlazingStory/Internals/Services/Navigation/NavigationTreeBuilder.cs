﻿using BlazingStory.Internals.Models;
using BlazingStory.Types;

namespace BlazingStory.Internals.Services.Navigation;

/// <summary>
/// NavigationTreeBuilder builds a tree of <see cref="NavigationTreeItem"/> from a collection of <see cref="StoryContainer"/>.
/// </summary>
internal class NavigationTreeBuilder
{
    /// <summary>
    /// Build a tree of <see cref="NavigationTreeItem"/> from a collection of <see cref="StoryContainer"/>.
    /// </summary>
    /// <param name="components">A collection of <see cref="StoryContainer"/> that is the source of the navigation item tree.</param>
    /// <param name="customPages">A collection of <see cref="CustomPageContainer"/> that is the source of the navigation item tree.</param>
    /// <param name="customOrderings">A collection of <see cref="NavigationTreeOrdering"/> that reprents specifications of ordering navigation tree items.</param>
    /// <param name="expandedNavigationPath">A navigation path string to specify the tree item node that should be expanded (ex."/story/examples-button--primary")</param>
    /// <returns></returns>
    internal NavigationTreeItem Build(IEnumerable<StoryContainer> components, IEnumerable<CustomPageContainer> customPages, IList<NavigationTreeOrdering>? customOrderings, string? expandedNavigationPath)
    {
        var root = new NavigationTreeItem { Type = NavigationItemType.Container };

        this.BuildStories(components, root);
        this.BuildCustomPages(customPages, root);
#if true
        var sortedSubItems = Sort(root.SubItems, customOrderings ?? []);
        root.SubItems.Clear();
        root.SubItems.AddRange(sortedSubItems);
#else
        root.SortSubItemsRecurse();
#endif

        if (!string.IsNullOrEmpty(expandedNavigationPath))
        {
            var expansionPath = new Stack<NavigationTreeItem>();
            if (FindExpansionPathTo(expansionPath, root, expandedNavigationPath))
            {
                foreach (var expansion in expansionPath) { expansion.Expanded = true; }
            }
        }

        root.SubItems.ForEach(story => story.Expanded = true);

        return root;
    }

    private void BuildStories(IEnumerable<StoryContainer> components, NavigationTreeItem root)
    {
        foreach (var component in components)
        {
            var segments = component.Title.Split('/');
            var componentNode = this.CreateOrGetNavigationTreeItem(root, pathSegments: Enumerable.Empty<string>(), segments);
            componentNode.Type = NavigationItemType.Component;

            var pathSegments = componentNode.PathSegments.Append(componentNode.Caption).ToArray();

            // Add a "Docs" node for the component
            var docsNode = new NavigationTreeItem
            {
                Type = NavigationItemType.Docs,
                NavigationPath = "/docs/" + NavigationPath.Create(component.Title, "Docs"),
                PathSegments = pathSegments,
                Caption = "Docs"
            };
            componentNode.SubItems.Add(docsNode);

            // Add "Story" nodes that live in the component
            var storyNodes = component.Stories
                .Select(story => new NavigationTreeItem
                {
                    Type = NavigationItemType.Story,
                    NavigationPath = "/story/" + story.NavigationPath,
                    PathSegments = pathSegments,
                    Caption = story.Name
                });
            componentNode.SubItems.AddRange(storyNodes);
        }
    }

    private void BuildCustomPages(IEnumerable<CustomPageContainer> customPages, NavigationTreeItem root)
    {
        foreach (var page in customPages)
        {
            var segments = page.Title.Split('/');
            var customPageNode = this.CreateOrGetNavigationTreeItem(root, pathSegments: Enumerable.Empty<string>(), segments);
            customPageNode.Type = NavigationItemType.CustomPage;
            customPageNode.Caption = segments.LastOrDefault("Custom");
            customPageNode.NavigationPath = "/custom/" + page.NavigationPath;
        }
    }

    private NavigationTreeItem CreateOrGetNavigationTreeItem(NavigationTreeItem item, IEnumerable<string> pathSegments, IEnumerable<string> segments)
    {
        var head = segments.First();
        var tails = segments.Skip(1);

        var subItem = item.SubItems.Find(sub => sub.Caption == head);
        if (subItem == null)
        {
            subItem = new NavigationTreeItem
            {
                Type = NavigationItemType.Container,
                PathSegments = pathSegments,
                Caption = head
            };
            item.SubItems.Add(subItem);
        }

        if (!tails.Any()) return subItem;

        return this.CreateOrGetNavigationTreeItem(subItem, pathSegments.Append(head).ToArray(), tails);
    }

    private static IEnumerable<NavigationTreeItem> Sort(IEnumerable<NavigationTreeItem> items, IList<NavigationTreeOrdering> customOrderings)
    {
        if (items.FirstOrDefault()?.Type is not NavigationItemType.Container and not NavigationItemType.Component) return items.ToArray();

        var itemSourceSet = items.ToDictionary(item => item.Caption, item => item);
        var sortedItems = new List<NavigationTreeItem>();

        for (var i = 0; i < customOrderings.Count; i++)
        {
            var request = customOrderings[i];
            if (request.Type != NavigationTreeOrdering.NodeType.Item) continue;
            if (itemSourceSet.TryGetValue(request.Title, out var item))
            {
                sortedItems.Add(item);
                itemSourceSet.Remove(request.Title);

                var nextIsSubItems = i + 1 < customOrderings.Count && customOrderings[i + 1].Type == NavigationTreeOrdering.NodeType.SubItems;
                var subCustomOrderings = nextIsSubItems ? customOrderings[i + 1].SubItems : [];

                var sortedSubItems = Sort(item.SubItems, subCustomOrderings);
                item.SubItems.Clear();
                item.SubItems.AddRange(sortedSubItems);

                if (nextIsSubItems) i++;
            }
        }

        // Sort remains recursively
        var sortedRemains = itemSourceSet.Values.Order(comparer: NavigationTreeItemComparer.Instance).ToArray();
        foreach (var item in sortedRemains)
        {
            var sortedSubItems = Sort(item.SubItems, []);
            item.SubItems.Clear();
            item.SubItems.AddRange(sortedSubItems);
        }
        sortedItems.AddRange(sortedRemains);

        return sortedItems;
    }

    internal class NavigationTreeItemComparer : IComparer<NavigationTreeItem>
    {
        internal static readonly NavigationTreeItemComparer Instance = new();

        int IComparer<NavigationTreeItem>.Compare(NavigationTreeItem? a, NavigationTreeItem? b)
        {
            if (a is null || b is null) return 0;
            if (a.Type == NavigationItemType.CustomPage && b.Type != NavigationItemType.CustomPage) return -1;
            if (a.Type != NavigationItemType.CustomPage && b.Type == NavigationItemType.CustomPage) return 1;
            return a.Caption.CompareTo(b.Caption);
        }
    }


    private static bool FindExpansionPathTo(Stack<NavigationTreeItem> expansionPath, NavigationTreeItem item, string expandedNavigationPath)
    {
        expansionPath.Push(item);
        if (item.NavigationPath == expandedNavigationPath) return true;
        foreach (var subItem in item.SubItems)
        {
            if (FindExpansionPathTo(expansionPath, subItem, expandedNavigationPath)) return true;
        }
        expansionPath.Pop();
        return false;
    }

}
