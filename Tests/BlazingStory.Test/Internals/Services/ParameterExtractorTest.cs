﻿using System.Linq.Expressions;
using BlazingStory.Internals.Services;
using BlazingStory.Internals.Services.XmlDocComment;
using BlazingStory.Test._Fixtures;
using Microsoft.Extensions.Logging.Abstractions;
using RazorClassLib1.Components.Button;
using RazorClassLib1.Components.Rating;
using RazorClassLib1.Components.TextInput;

namespace BlazingStory.Test.Internals.Services;

internal class ParameterExtractorTest
{
    [Test]
    public void GetParameterName_From_Expression_Test()
    {
        Expression<Func<Button, ButtonColor>> expression = (Button _) => _.Color;
        var parameterName = ParameterExtractor.GetParameterName(expression);
        parameterName.Is("Color");
    }

    [Test]
    public async Task GetParametersFromComponentType_Test()
    {
        var xmlDocComment = new XmlDocCommentForWasm(XmlDocCommentLoaderFromOutDir.CreateHttpClient(), NullLogger<XmlDocCommentForWasm>.Instance);
        var parameters = ParameterExtractor.GetParametersFromComponentType(typeof(Button), xmlDocComment);
        foreach (var item in parameters)
        {
            await item.UpdateSummaryFromXmlDocCommentAsync();
        }

        parameters
            .Select(p => $"{p.Name}, {p.Type.Name}, {p.Required}, {p.Summary}")
            .Is("Bold, Boolean, False, ",
                "Text, String, True, Set a text that is button caption.",
                "Color, ButtonColor, False, Set a color of the button. \"ButtonColor.Default\" is default.",
                "OnClick, EventCallback`1, False, Set a callback method that will be invoked when users click the button.",
                "Size, ComponentSize, False, Set a size of the component. \"ComponentSize.Medium\" is default.");
    }

    [Test]
    public async Task GetParametersForTheComponent_Inherited_NuGetPackage_Test()
    {
        var xmlDocComment = new XmlDocCommentForWasm(XmlDocCommentLoaderFromOutDir.CreateHttpClient(), NullLogger<XmlDocCommentForWasm>.Instance);
        var parameters = ParameterExtractor.GetParametersFromComponentType(typeof(Rating), xmlDocComment);
        foreach (var item in parameters)
        {
            await item.UpdateSummaryFromXmlDocCommentAsync();
        }

        parameters
            .Select(p => $"{p.Name}, {p.Type.Name}, {p.Required}, {p.Summary}")
            .Is("Rate, Int32, False, Gets or sets the score of rating.",
                "Color, String, False, Gets or sets the color of the rating mark.",
                "Id, String, False, Gets or sets the identifier for the component. See also the MDN document about the <a href=\"https://developer.mozilla.org/en-US/docs/Web/HTML/Global_attributes/id\" target=\"_blank\">global id attribute</a>.");
    }

    [Test]
    public async Task GetParametersForTheComponent_Inherited_GenericType_Test()
    {
        var xmlDocComment = new XmlDocCommentForWasm(XmlDocCommentLoaderFromOutDir.CreateHttpClient(), NullLogger<XmlDocCommentForWasm>.Instance);
        var parameters = ParameterExtractor.GetParametersFromComponentType(typeof(TextInput), xmlDocComment);
        foreach (var item in parameters)
        {
            await item.UpdateSummaryFromXmlDocCommentAsync();
        }

        parameters
            .Select(p => $"{p.Name}, {p.Type.Name}, {p.Required}, {p.Summary}")
            .Is("Value, String, False, Gets or sets the value of the input.",
                "ValueChanged, EventCallback`1, False, Gets or sets the callback that will be invoked when the value changes.");
    }
}
