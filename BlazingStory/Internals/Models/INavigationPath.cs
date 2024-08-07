﻿namespace BlazingStory.Internals.Models;

internal interface INavigationPath
{
    #region Public Properties

    /// <summary>
    /// Gets a navigation path string for the item. <br /> (ex. "/story/example-button--primary", "/docs/example-button--docs")
    /// </summary>
    string? NavigationPath { get; }

    #endregion Public Properties
}
