﻿using BlazingStory.Internals.Extensions;
using BlazingStory.Internals.Models;
using BlazingStory.Internals.Services;
using BlazingStory.Internals.Services.Command;
using BlazingStory.Internals.Services.Navigation;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace BlazingStory.Internals.Components.SideBar.NavigationTree;

public partial class NavigationTree : ComponentBase, IDisposable
{
    #region Public Properties

    [Parameter, EditorRequired]
    public NavigationTreeItem NavigationRoot { get; set; } = new();

    #endregion Public Properties

    #region Protected Properties

    [CascadingParameter]
    protected StoriesStore StoriesStore { get; init; } = default!;

    [CascadingParameter]
    protected QueryRouteData RouteData { get; init; } = default!;

    [CascadingParameter]
    protected IServiceProvider Services { get; init; } = default!;

    #endregion Protected Properties

    #region Private Properties

    [Inject] private NavigationManager? NavigationManager { get; set; }
    [Inject] private ILogger<NavigationTree>? Logger { get; set; }

    #endregion Private Properties

    #region Private Fields

    private readonly Subscriptions _Subscriptions = new();
    private NavigationService? _NavigationService;

    #endregion Private Fields

    #region Public Methods

    public void Dispose()
    {
        if (this.NavigationManager != null)
        {
            this.NavigationManager.LocationChanged -= this.NavigationManager_LocationChanged;
        }

        this._Subscriptions.Dispose();
    }

    #endregion Public Methods

    #region Protected Methods

    protected override void OnInitialized()
    {
        this._NavigationService = this.Services.GetRequiredService<NavigationService>();

        if (this.NavigationManager != null)
        {
            this.NavigationManager.LocationChanged += this.NavigationManager_LocationChanged;
        }

        var commandService = this.Services.GetRequiredService<CommandService>();
        this._Subscriptions.Add(
            commandService.Subscribe(CommandType.PreviousComponent, this.OnPreviousComponent),
            commandService.Subscribe(CommandType.NextComponent, this.OnNextComponent),
            commandService.Subscribe(CommandType.PreviousStory, this.OnPreviousStory),
            commandService.Subscribe(CommandType.NextStory, this.OnNextStory),
            commandService.Subscribe(CommandType.CollapseAll, this.OnCollapseAll)
        );
    }

    protected override void OnAfterRender(bool firstRender)
    {
        if (firstRender && this.NavigationManager != null)
        {
            this.NavigationManager_LocationChanged(this, new(this.NavigationManager.Uri, isNavigationIntercepted: true));
        }
    }

    #endregion Protected Methods

    #region Private Methods

    private ValueTask OnPreviousComponent() => this.MoveComponent(navigateToNext: false);

    private ValueTask OnNextComponent() => this.MoveComponent(navigateToNext: true);

    private ValueTask MoveComponent(bool navigateToNext)
    {
        this._NavigationService?.NavigateToNextComponentItem(this.RouteData, navigateToNext);
        return ValueTask.CompletedTask;
    }

    private ValueTask OnPreviousStory() => this.MoveStory(navigateToNext: false);

    private ValueTask OnNextStory() => this.MoveStory(navigateToNext: true);

    private ValueTask MoveStory(bool navigateToNext)
    {
        this._NavigationService?.NavigateToNextDocsOrStory(this.RouteData, navigateToNext);
        return ValueTask.CompletedTask;
    }

    private ValueTask OnCollapseAll()
    {
        foreach (var item in this.NavigationRoot.SubItems.SelectMany(headerItem => headerItem.SubItems))
        {
            item.ApplyExpansionRecursively(false);
        }

        return ValueTask.CompletedTask;
    }

    private void NavigationManager_LocationChanged(object? sender, LocationChangedEventArgs args)
    {
        if (this._NavigationService == null)
        {
            return;
        }

        if (!this._NavigationService.TryGetActiveNavigationItem(this.RouteData, out var activeItem, out var _))
        {
            if (this.RouteData.View == "story")
            {
                this._NavigationService.NotifyLastVisitedWasUnknown();
            }

            return;
        }

        this.NavigationRoot.EnsureExpandedTo(activeItem);
        this.StateHasChanged();

        if (this.Logger != null)
        {
            this._NavigationService.AddHistoryAsync(activeItem).AndLogException(this.Logger);
        }
    }

    #endregion Private Methods
}
