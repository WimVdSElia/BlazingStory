﻿using System.Collections.Specialized;
using System.Linq.Expressions;
using System.Reflection;
using System.Text.Json;
using System.Web;
using BlazingStory.Components;
using BlazingStory.Internals.Extensions;
using BlazingStory.Internals.Models;
using BlazingStory.Internals.Services;
using BlazingStory.Internals.Utils;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace BlazingStory.Internals.Pages.Canvas;

public partial class CanvasFrame : ComponentBase, IAsyncDisposable
{
    #region Protected Properties

    [CascadingParameter]
    protected StoriesStore StoriesStore { get; init; } = default!;

    [CascadingParameter]
    protected QueryRouteData RouteData { get; init; } = default!;

    [CascadingParameter]
    protected BlazingStoryApp BlazingStoryApp { get; init; } = default!;

    #endregion Protected Properties

    #region Private Properties

    [Inject] private IJSRuntime? JSRuntime { get; set; }
    [Inject] private ILogger<CanvasFrame>? Logger { get; set; }
    [Inject] private NavigationManager? NavigationManager { get; set; }

    #endregion Private Properties

    #region Private Fields

    // Lazy initialization for retrieving MethodInfo of EventTArgMonitorHandler
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Major Code Smell", "S3011:Reflection should not be used to increase accessibility of classes, methods, or fields", Justification = "<Pending>")]
    private static readonly Lazy<MethodInfo> _EventTArgMonitorHandlerMethod = new(() => typeof(CanvasFrame).GetMethod(nameof(EventTArgMonitorHandler), BindingFlags.NonPublic | BindingFlags.Instance) ?? throw new InvalidOperationException());

    // Lazy initialization for retrieving MethodInfo of EventCallbackFactory.Create
    private static readonly Lazy<MethodInfo> _EventCallbackCreateMethod = new(() =>
        typeof(EventCallbackFactory).GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => m.Name == "Create" && m.IsGenericMethod)
            .Select(m => (MethodInfo: m, ParameterTypes: m.GetParameters().Select(p => p.ParameterType).ToArray()))
            .Where(m => m.ParameterTypes.Length == 2)
            .Where(m => m.ParameterTypes[0] == typeof(object))
            .Where(m => m.ParameterTypes[1].IsGenericType && m.ParameterTypes[1].GetGenericTypeDefinition() == typeof(Func<,>))
            .Select(m => m.MethodInfo)
            .First());

    private readonly JSModule? _JSModule;

    private Story? _Story;

    private bool _EnableMeasure = false;

    private bool _Rendered = false;

    private string _ComponentArgsString = string.Empty;

    private string _GlobalEffectsString = string.Empty;

    #endregion Private Fields

    #region Public Constructors

    /// <summary>
    /// Constructor
    /// </summary>
    public CanvasFrame()
    {
        this._JSModule = new(() => this.JSRuntime, "Internals/Pages/Canvas/CanvasFrame.razor.js");
    }

    #endregion Public Constructors

    #region Public Methods

    public async ValueTask DisposeAsync()
    {
        if (this._Story != null)
        {
            this._Story.Context.ShouldRender -= this.StoryContext_ShouldRender;
        }

        if (this.NavigationManager != null)
        {
            this.NavigationManager.LocationChanged -= this.NavigationManager_LocationChanged;
        }

        if (this._JSModule != null)
        {
            await this._JSModule.DisposeAsync();
        }
    }

    #endregion Public Methods

    #region Protected Methods

    protected override void OnInitialized()
    {
        if (this.NavigationManager != null)
        {
            this.NavigationManager.LocationChanged += this.NavigationManager_LocationChanged;
        }
    }

    protected override async Task OnParametersSetAsync()
    {
        if (!this.StoriesStore.TryGetStoryByPath(this.RouteData.Parameter, out var story))
        {
            return;
        }

        if (Object.ReferenceEquals(this._Story, story))
        {
            return;
        }

        if (this._Story != null)
        {
            this._Story.Context.ShouldRender -= this.StoryContext_ShouldRender;
        }

        this._Story = story;
        this._Story.Context.ShouldRender += this.StoryContext_ShouldRender;

        await this.UpdateComponentStatesAsync();
        this.StateHasChanged();
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender)
        {
            return;
        }

        this._Rendered = true;
        await this.UpdateComponentStatesAsync();
        this.StateHasChanged();
    }

    #endregion Protected Methods

    #region Private Methods

    /// <summary>
    /// When the state of the story has been changed, this canvas frame should be re-rendered.
    /// </summary>
    private void StoryContext_ShouldRender(object? sender, EventArgs e)
    {
        this.StateHasChanged();
    }

    private void NavigationManager_LocationChanged(object? sender, LocationChangedEventArgs args)
    {
        if (this.Logger == null)
        {
            return;
        }

        this.UpdateComponentStatesAsync().AndLogException(this.Logger);
    }

    private async ValueTask UpdateComponentStatesAsync()
    {
        if (this.NavigationManager == null)
        {
            return;
        }

        var queryStrings = HttpUtility.ParseQueryString(new Uri(this.NavigationManager.Uri).Query);

        await this.UpdateComponentArgsFromUrlAsync(queryStrings);
        await this.UpdateGlobalEffectsFromUrlAsync(queryStrings);
        await this.WireUpEventMonitorsAsync();
    }

    private async ValueTask UpdateComponentArgsFromUrlAsync(NameValueCollection queryStrings)
    {
        if (this._Story == null)
        {
            return;
        }

        var componentArgsString = queryStrings["args"] ?? string.Empty;

        componentArgsString = UrlParameterShortener.DecodeAndDecompress(componentArgsString);

        if (this._ComponentArgsString == componentArgsString)
        {
            return;
        }

        this._ComponentArgsString = componentArgsString;

        await this._Story.Context.ResetArgumentsAsync();

        var componentArgs = this._ComponentArgsString.DecodeKeyValues();
        var parameters = this._Story.Context.Parameters;

        foreach (var arg in componentArgs)
        {
            var parameter = parameters.FirstOrDefault(p => p.Name == arg.Key);

            if (parameter == null)
            {
                continue;
            }

            if (!parameter.Type.TryConvertType(parameter.TypeStructure, arg.Value, out var value))
            {
                continue;
            }

            await this._Story.Context.AddOrUpdateArgumentAsync(arg.Key, value);
        }

        this.StateHasChanged();
    }

    private async ValueTask UpdateGlobalEffectsFromUrlAsync(NameValueCollection queryStrings)
    {
        // If it is during the server-side pre-rendering, nothing to do.
        if (!this._Rendered)
        {
            return;
        }

        var globalEffectsString = queryStrings["globals"] ?? "";

        if (this._GlobalEffectsString == globalEffectsString)
        {
            return;
        }

        this._GlobalEffectsString = globalEffectsString;

        var globalEffects = UriParameterKit.DecodeKeyValues(globalEffectsString);
        var background = globalEffects.TryGetValue("backgrounds.value", out var backgroundStr) ? backgroundStr : "transparent";
        var enableGrid = globalEffects.TryGetValue("backgrounds.grid", out var gridStr) ? bool.TryParse(gridStr, out var grid) ? grid : false : false;
        var enableOutline = globalEffects.TryGetValue("outline", out var outlineStr) ? bool.TryParse(outlineStr, out var outline) ? outline : false : false;

        var theme = globalEffects.TryGetValue("theme.value", out var themeStr) ? themeStr : "None";

        this._EnableMeasure = globalEffects.TryGetValue("measureEnabled", out var measureStr) ? bool.TryParse(measureStr, out var measure) ? measure : false : false;

        var basePath = "./_content/BlazingStory/css/preview/";
        var styleDescripters = new StyleDescriptor[] {
            new() { Id = "addon-background-grid", Enable = enableGrid, Href = basePath + "background-grid.min.css" },
            new() { Id = "addon-outline", Enable = enableOutline, Href = basePath + "outline.min.css" }};

        if (this._JSModule != null)
        {
            await this._JSModule.InvokeVoidAsync("ensurePreviewStyle", new object[] { background, styleDescripters, theme });
        }

        this.StateHasChanged();
    }

    // Method to wire up event monitors
    private async ValueTask WireUpEventMonitorsAsync()
    {
        if (this._Story?.Context?.Parameters == null)
        {
            return;
        }

        foreach (var parameter in this._Story.Context.Parameters)
        {
            // Check if parameter is an EventCallback
            if (parameter.Type.IsAssignableTo(typeof(EventCallback)))
            {
                var eventCallback = EventCallback.Factory.Create(this, (Func<Task>)(() => this.EventVoidMonitorHandler(parameter.Name)));

                await this._Story.Context.AddOrUpdateArgumentAsync(parameter.Name, eventCallback);
            }
            // Check if parameter is a generic EventCallback<>
            else if (parameter.Type.IsGenericType && parameter.Type.GetGenericTypeDefinition() == typeof(EventCallback<>))
            {
                var typeOfArgs = parameter.Type.GenericTypeArguments.First();

                if (typeOfArgs == null)
                {
                    continue;
                }

                var eventArgsParam = Expression.Parameter(typeOfArgs, "eventArgs");

                // Get the generic method for EventTArgMonitorHandler
                var monitorHandler = _EventTArgMonitorHandlerMethod.Value.MakeGenericMethod(typeOfArgs);
                var callBody = Expression.Call(Expression.Constant(this), monitorHandler, Expression.Constant(parameter.Name), eventArgsParam);

                // Create lambda expression
                var monitorHandlerLambdaType = typeof(Func<,>).MakeGenericType(typeOfArgs, typeof(Task));
                var monitorHandlerLambda = Expression.Lambda(monitorHandlerLambdaType, callBody, eventArgsParam);
                var monitorHandlerDelegate = monitorHandlerLambda.Compile();

                // Create event callback
                var eventCallbackCreate = _EventCallbackCreateMethod.Value.MakeGenericMethod(typeOfArgs) ?? throw new InvalidOperationException();

                var eventCallback = eventCallbackCreate.Invoke(EventCallback.Factory, new object[] { this, monitorHandlerDelegate });

                await this._Story.Context.AddOrUpdateArgumentAsync(parameter.Name, eventCallback);
            }
        }
    }

    private async Task EventVoidMonitorHandler(string name)
    {
        if (this._JSModule != null)
        {
            await this._JSModule.InvokeVoidAsync("dispatchComponentActionEvent", name, "void");
        }
    }

    private async Task EventTArgMonitorHandler<TArgs>(string name, TArgs eventArgs)
    {
        var json = JsonSerializer.Serialize(eventArgs, new JsonSerializerOptions { WriteIndented = true });

        if (this._JSModule != null)
        {
            await this._JSModule.InvokeVoidAsync("dispatchComponentActionEvent", name, json);
        }
    }

    #endregion Private Methods
}

internal class StyleDescriptor
{
    #region Public Properties

    public string? Id { get; init; }
    public bool Enable { get; init; }
    public string? Href { get; init; }

    #endregion Public Properties
}
