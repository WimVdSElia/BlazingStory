﻿using BlazingStory.Internals.Extensions;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using static System.Math;

namespace BlazingStory.Internals.Pages.Canvas;

public partial class MeasureLayer : ComponentBase, IAsyncDisposable
{
    #region Private Properties

    [Inject] private IJSRuntime? JSRuntime { get; set; }

    #endregion Private Properties

    #region Private Fields

    private static readonly string ResetCss =
        "border:none !important;" +
        "border-radius:0 !important;" +
        "outline:none !important;" +
        "margin:0 0 0 0 !important;" +
        "padding:0 0 0 0 !important;" +
        "line-height:none !important;" +
        "display:inline-block !important;" +
        "opacity:1 !important;" +
        "transition:none !important;";

    private DotNetObjectReference<MeasureLayer>? _This;

    private IJSObjectReference? _EventMonitorSubscriber;

    private Measurement? _Measurement;

    private string _ContentAreaStyle = "";

    private StyleSet _PaddingStyle = new();

    private StyleSet _PaddingNumStyle = new();

    private StyleSet _MarginStyle = new();

    private StyleSet _MarginNumStyle = new();

    private string _ContentSizeStyle = "";

    private string _ContentSizeText = "";

    #endregion Private Fields

    #region Public Methods

    [JSInvokable(nameof(TargetElementChanged))]
    public void TargetElementChanged(Measurement? measurement)
    {
        this._Measurement = measurement;

        if (measurement == null)
        {
            this.StateHasChanged();
            return;
        }

        var boundary = measurement.Boundary;
        var padding = measurement.Padding;
        var margin = measurement.Margin;

        var contentRect = new DOMRect
        {
            X = boundary.X + padding.Left,
            Y = boundary.Y + padding.Top,
            Width = boundary.Width - padding.Left - padding.Right,
            Height = boundary.Height - padding.Top - padding.Bottom
        };
        this._ContentAreaStyle = GetSpacingStyle(contentRect, BackColors.Content);

        this._ContentSizeText = $"{Round(contentRect.Width, 2)} x {Round(contentRect.Height, 2)}";
        this._ContentSizeStyle = GetContentSizeStyle(this._ContentSizeText, contentRect, boundary);

        this._PaddingStyle = GetSpacingStyleSet(padding, contentRect, BackColors.Padding);
        this._MarginStyle = GetSpacingStyleSet(margin, boundary, BackColors.Margin);

        var contentCenterX = contentRect.X + contentRect.Width / 2.0;
        var contentCenterY = contentRect.Y + contentRect.Height / 2.0;
        var paddingNumRect = ComputeNumTextRectSet(padding, contentCenterX, contentCenterY, contentRect, null);
        var marginNumRect = ComputeNumTextRectSet(margin, contentCenterX, contentCenterY, boundary, paddingNumRect);

        this._PaddingNumStyle = GetNumberStyleSet(paddingNumRect, NumTextColors.Padding);
        this._MarginNumStyle = GetNumberStyleSet(marginNumRect, NumTextColors.Margin);

        this.StateHasChanged();
    }

    public async ValueTask DisposeAsync()
    {
        await this._EventMonitorSubscriber.DisposeIfConnectedAsync("dispose");
        this._This?.Dispose();
    }

    #endregion Public Methods

    #region Protected Methods

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender || this.JSRuntime is null)
        {
            return;
        }

        await using var module = await this.JSRuntime.ImportAsync("Internals/Pages/Canvas/MeasureLayer.razor.js");
        this._This = DotNetObjectReference.Create(this);
        this._EventMonitorSubscriber = await module.InvokeAsync<IJSObjectReference>("subscribeTargetElementChanged", this._This);
    }

    #endregion Protected Methods

    #region Private Methods

    private static string GetContentSizeStyle(string contentSizeText, in DOMRect contentRect, in DOMRect boundary)
    {
        var rect = GetTextRect(contentSizeText);

        if (rect.Width >= contentRect.Width || rect.Height >= contentRect.Height)
        {
            rect.X = boundary.X + boundary.Width + NumTextMetric.Gap;
            rect.Y = boundary.Y + boundary.Height + NumTextMetric.Gap;
        }
        else
        {
            rect.X = contentRect.X + (contentRect.Width - rect.Width) / 2;
            rect.Y = contentRect.Y + (contentRect.Height - rect.Height) / 2;
        }

        return GetNumberStyle(rect, NumTextColors.Content);
    }

    private static StyleSet GetSpacingStyleSet(in SpacingSize spacing, in DOMRect boundary, string background)
    {
        var top = boundary;
        top.X = boundary.X - spacing.Left;
        top.Y = boundary.Y - spacing.Top;
        top.Width = spacing.Left + boundary.Width + spacing.Right;
        top.Height = spacing.Top;

        var left = boundary;
        left.X = boundary.X - spacing.Left;
        left.Width = spacing.Left;

        var bottom = top;
        bottom.Y = boundary.Y + boundary.Height;
        bottom.Height = spacing.Bottom;

        var right = boundary;
        right.X = boundary.X + boundary.Width;
        right.Width = spacing.Right;

        return new StyleSet
        {
            Top = GetSpacingStyle(top, background),
            Left = GetSpacingStyle(left, background),
            Bottom = GetSpacingStyle(bottom, background),
            Right = GetSpacingStyle(right, background),
        };
    }

    private static string GetSpacingStyle(in DOMRect rect, string background)
    {
        return ResetCss +
            $"position:absolute; top:{Round(rect.Y, 2)}px; left:{Round(rect.X, 2)}px; width:{Round(rect.Width, 2)}px; height:{Round(rect.Height, 2)}px; background:{background} !important;";
    }

    private static StyleSet GetNumberStyleSet(DOMRectSet rectSet, string background)
    {
        return new StyleSet
        {
            Top = GetNumberStyle(rectSet.Top, background),
            Left = GetNumberStyle(rectSet.Left, background),
            Bottom = GetNumberStyle(rectSet.Bottom, background),
            Right = GetNumberStyle(rectSet.Right, background),
        };
    }

    private static string GetNumberStyle(in DOMRect rect, string background)
    {
        if (rect.Equals(DOMRect.Empty))
        {
            return "display:none !important;";
        }

        return GetSpacingStyle(rect, background) +
            "font:600 12px monospace !important; border-radius:3px !important; color:#000 !important;" +
            $"text-align:center !important; line-height:{Round(rect.Height, 2)}px !important;";
    }

    private static DOMRectSet ComputeNumTextRectSet(SpacingSize spacing, double centerX, double centerY, in DOMRect boundary, DOMRectSet? coRectSet)
    {
        var rectSet = new DOMRectSet
        {
            Top = ComputeNumTextRect(spacing.Top, centerX, centerY: boundary.Y - spacing.Top / 2.0),
            Left = ComputeNumTextRect(spacing.Left, centerX: boundary.X - spacing.Left / 2.0, centerY),
            Bottom = ComputeNumTextRect(spacing.Bottom, centerX, centerY: boundary.Y + boundary.Height + spacing.Bottom / 2.0),
            Right = ComputeNumTextRect(spacing.Right, centerX: boundary.X + boundary.Width + spacing.Right / 2.0, centerY)
        };

        if (coRectSet == null)
        {
            return rectSet;
        }

        RelocateNumTextRect(ref rectSet.Top, coRectSet.Top, vertical: true, delta: -1, limit: coRectSet.Top.Y - rectSet.Top.Height);
        RelocateNumTextRect(ref rectSet.Bottom, coRectSet.Bottom, vertical: true, delta: +1, limit: coRectSet.Bottom.Y + coRectSet.Bottom.Height);
        RelocateNumTextRect(ref rectSet.Left, coRectSet.Left, vertical: false, delta: -1, limit: coRectSet.Left.X - rectSet.Left.Width);
        RelocateNumTextRect(ref rectSet.Right, coRectSet.Right, vertical: false, delta: +1, limit: coRectSet.Right.X + coRectSet.Right.Width);

        return rectSet;
    }

    private static void RelocateNumTextRect(ref DOMRect rect, in DOMRect coRect, bool vertical, int delta, double limit)
    {
        if (rect.Equals(DOMRect.Empty) || coRect.Equals(DOMRect.Empty))
        {
            return;
        }

        if (vertical)
        {
            if ((delta < 0 && rect.Y >= limit) || (delta > 0 && rect.Y <= limit))
            {
                rect.Y = limit + delta * NumTextMetric.Gap;
            }
        }
        else
        {
            if ((delta < 0 && rect.X >= limit) || (delta > 0 && rect.X <= limit))
            {
                rect.X = limit + delta * NumTextMetric.Gap;
            }
        }
    }

    private static DOMRect ComputeNumTextRect(double num, double centerX, double centerY)
    {
        var roundNum = Round(num);

        if (roundNum == 0.0)
        {
            return DOMRect.Empty;
        }

        var rect = GetTextRect(roundNum.ToString());
        rect.X = centerX - rect.Width / 2.0;
        rect.Y = centerY - rect.Height / 2.0;

        return rect;
    }

    private static DOMRect GetTextRect(string text)
    {
        var textLen = text.Length;

        return new DOMRect
        {
            Width = textLen * NumTextMetric.HorizontalSize + NumTextMetric.HorizontalPadding * 2,
            Height = NumTextMetric.VerticalSize + NumTextMetric.VerticalPadding * 2
        };
    }

    #endregion Private Methods

    #region Public Structs

    public struct DOMRect
    {
        #region Public Properties

        public double X { get; set; }
        public double Y { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }

        #endregion Public Properties

        #region Public Fields

        public static readonly DOMRect Empty = new();

        #endregion Public Fields
    }

    public struct SpacingSize
    {
        #region Public Properties

        public double Top { get; set; }
        public double Left { get; set; }
        public double Bottom { get; set; }
        public double Right { get; set; }

        #endregion Public Properties
    }

    #endregion Public Structs

    #region Public Classes

    public class Measurement
    {
        #region Public Properties

        public DOMRect Boundary { get; set; }
        public SpacingSize Padding { get; set; }
        public SpacingSize Margin { get; set; }

        #endregion Public Properties
    }

    #endregion Public Classes

    #region Private Classes

    private static class BackColors
    {
        #region Public Fields

        public const string Content = "#6fa8dca8";
        public const string Padding = "#93c47d8c";
        public const string Margin = "#f6b26ba8";

        #endregion Public Fields
    }

    private static class NumTextColors
    {
        #region Public Fields

        public const string Content = "#6fa8dc";
        public const string Padding = "#93c47d";
        public const string Margin = "#f6b26b";

        #endregion Public Fields
    }

    private static class NumTextMetric
    {
        #region Public Fields

        public const int Gap = 6;
        public const int HorizontalPadding = 6;
        public const int VerticalPadding = 3;
        public const int HorizontalSize = 7;
        public const int VerticalSize = 16;

        #endregion Public Fields
    }

    private class StyleSet
    {
        #region Public Fields

        public string Top = "";
        public string Left = "";
        public string Bottom = "";
        public string Right = "";

        #endregion Public Fields
    }

    private class DOMRectSet
    {
        #region Public Fields

        public DOMRect Top;
        public DOMRect Left;
        public DOMRect Bottom;
        public DOMRect Right;

        #endregion Public Fields
    }

    #endregion Private Classes
}
