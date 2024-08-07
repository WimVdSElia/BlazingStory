﻿using System.ComponentModel;
using System.Reflection;
using BlazingStory.Configurations;
using BlazingStory.Internals.Extensions;
using BlazingStory.Internals.Services;
using BlazingStory.Internals.Services.Addons;
using BlazingStory.Internals.Services.Command;
using BlazingStory.Internals.Services.Navigation;
using BlazingStory.Internals.Services.XmlDocComment;
using BlazingStory.Types;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using Toolbelt.Blazor.Extensions.DependencyInjection;

namespace BlazingStory.Components;

/// <summary>
/// The Blazing Story app component. <br /> This component is the root component of the Blazing
/// Story app.
/// </summary>
/// <seealso cref="Microsoft.AspNetCore.Components.ComponentBase" />
/// <seealso cref="System.IAsyncDisposable" />
public partial class BlazingStoryApp : IAsyncDisposable
{
    #region Public Properties

    /// <summary>
    /// A collection of assemblies to search for stories.
    /// </summary>
    [Parameter, EditorRequired]
    public IEnumerable<Assembly>? Assemblies { get; set; }

    /// <summary>
    /// A title string of this Blazing Story app. (The default value is "Blazing Story") <br /> This
    /// is used for the title of every HTML document. And also, this is used for the brand logo
    /// unless you customize the logo contents using <see cref="BrandLogoArea" /> render fragment parameter.
    /// </summary>
    [Parameter]
    public string? Title { get; set; } = "Blazing Story";

    /// <summary>
    /// A type of the default layout component to use when displaying a story.
    /// </summary>
    [Parameter]
    public Type? DefaultLayout { get; set; }

    /// <summary>
    /// Content for the brand logo area at the top of the sidebar. <br /> You can refer to the
    /// instance of the <see cref="BlazingStoryApp" /> component via <c>context</c> argument in the
    /// rendered fragment.
    /// </summary>
    [Parameter]
    public RenderFragment<BlazingStoryApp>? BrandLogoArea { get; set; }

    /// <summary>
    /// The available color schemes for the Blazing Story. <br /> When the <see
    /// cref="AvailableColorSchemes.Light" /> is set, the Blazing Story app will be displayed in
    /// light mode only. <br /> When the <see cref="AvailableColorSchemes.Dark" /> is set, the
    /// Blazing Story app will be displayed in dark mode only. <br /> The default value is <see
    /// cref="AvailableColorSchemes.Both" />, and system preference will be respected.
    /// </summary>
    // TODO: The default value is
    // <see cref="AvailableColorSchemes.Both" />
    // , and user preference will be respected. The user preference page will be displayed only when the
    // <see cref="AvailableColorSchemes.Both" />
    // is set.
    [Parameter]
    public AvailableColorSchemes AvailableColorSchemes { get; set; } = AvailableColorSchemes.Both;

    /// <summary>
    /// [Preview feature] Gets or sets whether to enable hot reloading. (default: false)
    /// </summary>
    [Parameter]
    public bool EnableHotReloading { get; set; }

    /// <summary>
    /// A type of the theme component to use when displaying a story. <br /> The theme component is
    /// a component that provides a theme for the story.
    /// </summary>
    [Parameter]
    public Type? ThemeType { get; set; }

    #endregion Public Properties

    #region Private Properties

    [Inject] private IJSRuntime? JSRuntime { get; set; }

    [Inject] private ILoggerFactory? LoggerFactory { get; set; }

    [Inject] private NavigationManager? NavigationManager { get; set; }

    [Inject] private IServiceProvider? GlobalServices { get; set; }

    #endregion Private Properties

    #region Private Fields

    private readonly StoriesStore _StoriesStore = new();
    private readonly AddonsStore _AddonsStore = new();
    private readonly JSModule _JSModule;
    private readonly BlazingStoryOptions _Options = new();

    private IEnumerable<Type> _AddonsTypes = new Type[]
    {
        typeof(BlazingStory.Internals.Addons.Background.BackgroundAddon),
        typeof(BlazingStory.Internals.Addons.Grid.GridAddon),
        typeof(BlazingStory.Internals.Addons.ChangeSize.ChangeSizeAddon),
        typeof(BlazingStory.Internals.Addons.Measure.MeasureAddon),
        typeof(BlazingStory.Internals.Addons.Outlines.OutlinesAddon),
    };

    private AvailableColorSchemes _PrevAvailableColorSchemes = AvailableColorSchemes.Both;
    private bool _firstRendered = false;

    private int _InitLevel = 0;

    private AsyncServiceScope? _ServiceScope;
    private DotNetObjectReference<BlazingStoryApp> _RefThis;

    private string _PreferesColorScheme = "light";

    private IJSObjectReference? _PreferesColorSchemeChangeSubscriber;

    #endregion Private Fields

    #region Public Constructors

    public BlazingStoryApp()
    {
        this._JSModule = new(() => this.JSRuntime, "Components/BlazingStoryApp.razor.js");
        this._RefThis = DotNetObjectReference.Create(this);
    }

    #endregion Public Constructors

    #region Public Methods

    [JSInvokable, EditorBrowsable(EditorBrowsableState.Never)]
    public async Task OnPreferesColorSchemeChanged(string preferesColorScheme)
    {
        await this.UpdatePreferesColorSchemeAsync();
        this.StateHasChanged();
    }

    public async ValueTask DisposeAsync()
    {
        await this._PreferesColorSchemeChangeSubscriber.DisposeIfConnectedAsync("dispose");
        this._RefThis.Dispose();
        await this._JSModule.DisposeAsync();

        if (this._ServiceScope.HasValue)
        {
            await this._ServiceScope.Value.DisposeAsync();
        }
    }

    #endregion Public Methods

    #region Protected Methods

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Trimming", "IL2075:'this' argument does not satisfy 'DynamicallyAccessedMembersAttribute' in call to target method. The return value of the source method does not have matching annotations.", Justification = "<Pending>")]
    protected override void OnInitialized()
    {
        if (this.ThemeType != null)
        {
            var typeThemeAddon = typeof(BlazingStory.Internals.Addons.Theme.ThemeAddon<>).MakeGenericType(this.ThemeType);
            this._AddonsTypes = this._AddonsTypes.Append(typeThemeAddon);
        }

        this._ServiceScope = this.ConfigureServices();
    }

    protected override async Task OnParametersSetAsync()
    {
        await base.OnParametersSetAsync();

        this._Options.EnableHotReloading = this.EnableHotReloading;

        if (this.AvailableColorSchemes != this._PrevAvailableColorSchemes)
        {
            this._PrevAvailableColorSchemes = this.AvailableColorSchemes;

            if (this._firstRendered)
            {
                await this.UpdatePreferesColorSchemeAsync();
            }
        }
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender)
        {
            return;
        }

        this._firstRendered = true;

        await this.UpdatePreferesColorSchemeAsync();

        this._PreferesColorSchemeChangeSubscriber = await this._JSModule.InvokeAsync<IJSObjectReference>("subscribePreferesColorSchemeChanged", this._RefThis, nameof(OnPreferesColorSchemeChanged));

        // Init Level 0 -> Default Background is visible & Color Scheme Container is hidden and transparent.

        await this._JSModule.InvokeVoidAsync("ensureAllFontsAndStylesAreLoaded");

        // Init Level 1 -> The Default Background is still visible & Color Scheme Container is
        // becoming to be VISIBLE but still transparent.
        this._InitLevel++;
        this.StateHasChanged();

        await Task.Delay(50);

        // Init Level 2 -> The Default Background is still visible & Color Scheme Container
        // transitions to OPAQUE in 0.2sec.
        this._InitLevel++;
        this.StateHasChanged();

        await Task.Delay(200);

        // Init Level 3 -> After the Color Scheme Container transitioned to opaque, The Default
        // Background transitions to INVISIBLE.
        this._InitLevel++;
        this.StateHasChanged();
    }

    #endregion Protected Methods

    #region Private Methods

    private AsyncServiceScope ConfigureServices()
    {
        if (this.JSRuntime is null)
        {
            throw new InvalidOperationException("The JSRuntime is not initialized.");
        }

        if (this.LoggerFactory is null)
        {
            throw new InvalidOperationException("The LoggerFactory is not initialized.");
        }

        if (this.NavigationManager is null)
        {
            throw new InvalidOperationException("The NavigationManager is not initialized.");
        }

        if (this.GlobalServices is null)
        {
            throw new InvalidOperationException("The GlobalServices is not initialized.");
        }

        var services = new ServiceCollection()
            .AddSingleton<IJSRuntime>(_ => this.JSRuntime)
            .AddSingleton<ILoggerFactory>(_ => this.LoggerFactory)
            .AddSingleton(typeof(ILogger<>), typeof(Logger<>))
            .AddSingleton<HttpClient>(_ => this.GlobalServices.GetRequiredService<HttpClient>())
            .AddSingleton<NavigationManager>(_ => this.NavigationManager)
            .AddSingleton<GlobalServiceProvider>(_ => new(this.GlobalServices))
            .AddHotKeys2()
            .AddScoped<HelperScript>()
            .AddScoped<CommandService>()
            .AddScoped<NavigationService>()
            .AddScoped<AddonsStore>(_ => this._AddonsStore)
            .AddScoped<BlazingStoryOptions>(_ => this._Options)
            .AddScoped<WebAssets>()
            .AddTransient<ComponentActionLogs>();

        if (OperatingSystem.IsBrowser())
        {
            services.AddSingleton<IXmlDocComment, XmlDocCommentForWasm>();
        }
        else
        {
            services.AddSingleton<IXmlDocComment, XmlDocCommentForServer>();
        }

        var serviceProvider = services.BuildServiceProvider();

        return serviceProvider.CreateAsyncScope();
    }

    private async ValueTask UpdatePreferesColorSchemeAsync()
    {
        if (!this._ServiceScope.HasValue)
        {
            throw new InvalidOperationException("The service provider is not initialized.");
        }

        if (this.AvailableColorSchemes == AvailableColorSchemes.Both)
        {
            var helperScript = this._ServiceScope.Value.ServiceProvider.GetRequiredService<HelperScript>();
            var colorScheme = await helperScript.GetLocalStorageItemAsync("ColorScheme", defaultValue: "system");

            if (colorScheme != "dark" && colorScheme != "light")
            {
                colorScheme = await this._JSModule.InvokeAsync<string>("getPrefersColorScheme");
            }

            this._PreferesColorScheme = colorScheme;
        }
        else
        {
            this._PreferesColorScheme = this.AvailableColorSchemes switch
            {
                AvailableColorSchemes.Light => "light",
                AvailableColorSchemes.Dark => "dark",
                _ => throw new InvalidOperationException($"The {nameof(this.AvailableColorSchemes)} is invalid."),
            };
        }
    }

    #endregion Private Methods
}
