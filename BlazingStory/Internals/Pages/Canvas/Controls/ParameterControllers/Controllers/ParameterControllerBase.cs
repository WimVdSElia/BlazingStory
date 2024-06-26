﻿using BlazingStory.Internals.Models;
using Microsoft.AspNetCore.Components;

namespace BlazingStory.Internals.Pages.Canvas.Controls.ParameterControllers.Controllers;

public class ParameterControllerBase : ComponentBase
{
    #region Public Properties

    [Parameter, EditorRequired]
    public string Key { get; set; }

    [Parameter, EditorRequired]
    public ComponentParameter Parameter { get; set; }

    [Parameter, EditorRequired]
    public object? Value { get; set; }

    [Parameter]
    public string[] Options { get; set; }

    [Parameter]
    public EventCallback<ParameterInputEventArgs> OnInput { get; set; }

    #endregion Public Properties

    #region Protected Methods

    protected async Task OnInputAsync(object? value)
    {
        if (this.Parameter == null) throw new NullReferenceException("Parameter is null.");
        await this.OnInput.InvokeAsync(new ParameterInputEventArgs(value, this.Parameter));
    }

    #endregion Protected Methods
}
