﻿using BlazingStory.Internals.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace BlazingStory.Internals.Components.Inputs;

public partial class ColorInput : ComponentBase, IAsyncDisposable
{
    #region Public Properties

    [Parameter]
    public string? Value { get; set; }

    [Parameter]
    public string? PlaceHolder { get; set; }

    [Parameter]
    public EventCallback<ChangeEventArgs> OnInput { get; set; }

    #endregion Public Properties

    #region Private Properties

    [Inject] private IJSRuntime? JSRuntime { get; set; }

    private string _HexColorText => this._CurrentColor?.NormalizedHexText ?? "#000000";

    #endregion Private Properties

    #region Private Fields

    private readonly JSModule _JSModule;
    private WebColor? _CurrentColor = null;

    private WebColor.Type _CurrentColorType = WebColor.Type.Unknown;

    #endregion Private Fields

    #region Public Constructors

    public ColorInput()
    {
        this._JSModule = new(() => this.JSRuntime, "Internals/Components/Inputs/ColorInput.razor.js");
    }

    #endregion Public Constructors

    #region Public Methods

    public override async Task SetParametersAsync(ParameterView parameters)
    {
        var valueParameter = parameters.GetValueOrDefault(nameof(this.Value), default(string));
        var valueHasChanged = this.Value != valueParameter;
        await base.SetParametersAsync(parameters);

        if (valueHasChanged)
        {
            await this.Parse(valueParameter);
            this.StateHasChanged();
        }
    }

    public async ValueTask DisposeAsync()
    {
        await this._JSModule.DisposeAsync();
    }

    #endregion Public Methods

    #region Private Methods

    private async ValueTask Parse(string? colorText)
    {
        var (color, type) = WebColor.Parse(colorText);

        if (color == null)
        {
            var computedColorText = await this.GetComputedColorAsync(colorText);
            (color, _) = WebColor.Parse(computedColorText, colorText);
            type = WebColor.Type.Hex;
        }

        this._CurrentColor = color;
        this._CurrentColorType = type;
    }

    private ValueTask<string?> GetComputedColorAsync(string? colorText)
    {
        return this._JSModule.InvokeAsync<string?>("getComputedColor", colorText);
    }

    private async Task OnInputInternal(ChangeEventArgs args)
    {
        this.Value = args.Value as string;
        await this.Parse(this.Value);
        await this.OnInput.InvokeAsync(args);
    }

    private async Task OnInputColorPicker(ChangeEventArgs args)
    {
        var currentType = this._CurrentColorType;
        await this.Parse(args.Value as string);

        if (this._CurrentColor != null)
        {
            this._CurrentColorType = currentType;
            args.Value = currentType switch
            {
                WebColor.Type.RGBA => this._CurrentColor.RGBAText,
                WebColor.Type.HSLA => this._CurrentColor.HSLAText,
                _ => this._CurrentColor.HexOrNameText
            };
        }

        this.Value = args.Value as string;
        await this.OnInput.InvokeAsync(args);
    }

    private async Task OnClickExchangeIcon()
    {
        if (this._CurrentColor != null)
        {
            switch (this._CurrentColorType)
            {
                case WebColor.Type.RGBA:
                    this._CurrentColorType = WebColor.Type.HSLA;
                    this.Value = this._CurrentColor.HSLAText;
                    break;

                case WebColor.Type.HSLA:
                    this._CurrentColorType = WebColor.Type.Hex;
                    this.Value = this._CurrentColor.HexOrNameText;
                    break;

                default:
                    this._CurrentColorType = WebColor.Type.RGBA;
                    this.Value = this._CurrentColor.RGBAText;
                    break;
            }
            await this.OnInput.InvokeAsync(new() { Value = this.Value });
        }
    }

    #endregion Private Methods
}
