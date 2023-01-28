﻿using Microsoft.JSInterop;

namespace BlazingStory.Internals.Services;

internal class JSModule : IAsyncDisposable
{
    private IJSObjectReference? _Module;

    private readonly Func<IJSRuntime> _GetJSRuntime;

    private readonly string _ModulePath;

    internal JSModule(Func<IJSRuntime> jSRuntime, string modulePath)
    {
        this._GetJSRuntime = jSRuntime;
        this._ModulePath = modulePath;
    }

    private async ValueTask<IJSObjectReference> GetModuleAsync()
    {
        if (this._Module == null)
        {
            var jsRuntime = this._GetJSRuntime();
            this._Module = await jsRuntime.InvokeAsync<IJSObjectReference>("import", this._ModulePath);
        }
        return this._Module;
    }

    public async ValueTask InvokeVoidAsync(string identifier, params object?[]? args)
    {
        var module = await this.GetModuleAsync();
        await module.InvokeVoidAsync(identifier, args);
    }

    public async ValueTask<T> InvokeAsync<T>(string identifier, params object?[]? args)
    {
        var module = await this.GetModuleAsync();
        return await module.InvokeAsync<T>(identifier, args);
    }

    public async ValueTask DisposeAsync()
    {
        if (this._Module == null) return;
        try { await this._Module.DisposeAsync(); } catch (JSDisconnectedException) { }
    }
}