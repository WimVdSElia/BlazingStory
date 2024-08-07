﻿using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using BlazingStory.Internals.Services.XmlDocComment;
using BlazingStory.Internals.Utils;
using BlazingStory.Types;
using Microsoft.AspNetCore.Components;

namespace BlazingStory.Internals.Models;

public class ComponentParameter
{
    #region Internal Properties

    internal MarkupString Summary { get; private set; } = default;

    #endregion Internal Properties

    #region Internal Fields

    internal readonly string Name;
    internal readonly Type Type;
    internal readonly TypeStructure TypeStructure;
    internal readonly bool Required;
    internal ControlType Control = ControlType.Default;
    internal object? DefaultValue = null;
    internal string[]? Options = null;

    #endregion Internal Fields

    #region Private Fields

    private readonly Type _ComponentType;

    private readonly PropertyInfo _PropertyInfo;

    private readonly IXmlDocComment _XmlDocComment;

    #endregion Private Fields

    #region Internal Constructors

    internal ComponentParameter(Type componentType, PropertyInfo propertyInfo, IXmlDocComment xmlDocComment)
    {
        this._ComponentType = componentType;
        this._PropertyInfo = propertyInfo;
        this._XmlDocComment = xmlDocComment;
        this.Name = propertyInfo.Name;
        this.Type = propertyInfo.PropertyType;
        this.TypeStructure = TypeUtility.ExtractTypeStructure(propertyInfo.PropertyType);
        this.Required = propertyInfo.GetCustomAttribute<EditorRequiredAttribute>() != null;
    }

    #endregion Internal Constructors

    #region Internal Methods

    /// <summary>
    /// Update summary property text of this parameter by reading a XML document comment file.
    /// </summary>
    internal async ValueTask UpdateSummaryFromXmlDocCommentAsync()
    {
        this.Summary = await this._XmlDocComment.GetSummaryOfPropertyAsync(this._PropertyInfo.DeclaringType ?? this._ComponentType, this.Name);
    }

    internal IEnumerable<string> GetParameterTypeStrings() => TypeUtility.GetTypeDisplayText(this.Type);

    #endregion Internal Methods
}

internal static class ComponentParameterExtensions
{
    #region Public Methods

    public static bool TryGetByName(this IEnumerable<ComponentParameter> componentParameters, string name, [NotNullWhen(true)] out ComponentParameter? parameter)
    {
        parameter = componentParameters.FirstOrDefault(p => p.Name == name);

        return parameter != null;
    }

    #endregion Public Methods
}
