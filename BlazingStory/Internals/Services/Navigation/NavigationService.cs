﻿using System.Diagnostics.CodeAnalysis;
using BlazingStory.Internals.Models;
using Microsoft.AspNetCore.Components;

namespace BlazingStory.Internals.Services.Navigation;

internal class NavigationService
{
    private readonly NavigationManager _NavigationManager;

    private readonly NavigationHistory _NavigationHistory;

    private NavigationTreeItem _Root = new();

    private int _SearchResultSequence = 0;

    public NavigationService(NavigationManager navigationManager, HelperScript helperScript)
    {
        this._NavigationManager = navigationManager;
        this._NavigationHistory = new(helperScript);
    }

    internal NavigationTreeItem BuildNavigationTree(IEnumerable<StoryContainer> storyContainers, string? expandedNavigationPath)
    {
        this._Root = new NavigationTreeBuilder().Build(storyContainers, expandedNavigationPath);
        return this._Root;
    }

    internal string GetNavigationUrl(INavigationPath item) => "./?path=/story/" + item.NavigationPath;

    internal void NavigateTo(INavigationPath item)
    {
        this._NavigationManager.NavigateTo(this.GetNavigationUrl(item));
    }

    internal void NavigateToDefaultStory(QueryRouteData? routeData)
    {
        if (this.TryGetActiveNavigationItem(routeData, out var _, out var storyItems)) return;

        var firstStory = storyItems.FirstOrDefault();
        if (firstStory == null) return;

        this._Root.EnsureExpandedTo(firstStory);
        this.NavigateTo(firstStory);
    }

    internal bool TryGetActiveNavigationItem(QueryRouteData? routeData, [NotNullWhen(true)] out NavigationTreeItem? activeItem, out IEnumerable<NavigationTreeItem> storyItems)
    {
        activeItem = null;
        storyItems = this._Root.EnumAll()
            .Where(item => item.Type == NavigationItemType.Story)
            .ToArray();

        var navigationPath = routeData?.Parameter;
        if (string.IsNullOrEmpty(navigationPath)) return false;

        activeItem = storyItems.FirstOrDefault(item => item.NavigationPath == navigationPath);
        if (activeItem == null) return false;

        return true;
    }

    internal async ValueTask BackToLastNavigatedAsync()
    {
        var historyItems = await this.GetHistoryItemsAsync();
        var lastNavigated = historyItems.FirstOrDefault();
        if (lastNavigated == null)
        {
            this.NavigateToDefaultStory(null);
        }
        else
        {
            this.NavigateTo(lastNavigated);
        }
    }


    internal ValueTask<IEnumerable<NavigationListItem>> GetHistoryItemsAsync()
    {
        return this._NavigationHistory.GetItemsAsync();
    }

    internal async ValueTask AddHistoryAsync(NavigationTreeItem active)
    {
        await this._NavigationHistory.AddAsync(this._Root, active);
    }

    internal async ValueTask ClearHistoryAsync()
    {
        await this._NavigationHistory.ClearAsync();
    }

    internal IEnumerable<NavigationListItem> Search(IEnumerable<string>? keywords)
    {
        if (keywords == null || keywords.Where(word => !string.IsNullOrEmpty(word)).Any() == false) return Enumerable.Empty<NavigationListItem>();
        var results = new List<NavigationListItem>();
        this.SearchCore(this._Root, keywords, results);
        return results;
    }

    private void SearchCore(NavigationTreeItem item, IEnumerable<string> keywords, List<NavigationListItem> results)
    {
        if (item.Type is NavigationItemType.Component or NavigationItemType.Story)
        {
            if (keywords.Any(word => item.Caption.Contains(word, StringComparison.InvariantCultureIgnoreCase)))
            {
                results.Add(NavigationListItem.CreateFrom(this._SearchResultSequence++, item));
                return;
            }
        }

        foreach (var subItem in item.SubItems)
        {
            this.SearchCore(subItem, keywords, results);
        }
    }
}
