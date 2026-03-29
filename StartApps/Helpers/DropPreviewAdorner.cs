using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using MediaBrush = System.Windows.Media.Brush;
using MediaColor = System.Windows.Media.Color;
using MediaPen = System.Windows.Media.Pen;

namespace StartApps.Helpers;

public class DropPreviewAdorner : Adorner
{
    private static readonly MediaPen BorderPen = new(new SolidColorBrush(MediaColor.FromArgb(180, 140, 180, 255)), 2)
    {
        LineJoin = PenLineJoin.Round
    };

    private static readonly MediaBrush FillBrush = new SolidColorBrush(MediaColor.FromArgb(60, 140, 180, 255));

    static DropPreviewAdorner()
    {
        BorderPen.Brush.Freeze();
        FillBrush.Freeze();
    }

    private Rect _targetRect;

    public DropPreviewAdorner(UIElement adornedElement)
        : base(adornedElement)
    {
        IsHitTestVisible = false;
        _targetRect = Rect.Empty;
    }

    public void Update(Rect rect)
    {
        _targetRect = rect;
        InvalidateVisual();
    }

    public void Clear()
    {
        if (_targetRect.IsEmpty)
        {
            return;
        }

        _targetRect = Rect.Empty;
        InvalidateVisual();
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        if (_targetRect.IsEmpty)
        {
            return;
        }

        drawingContext.DrawRoundedRectangle(FillBrush, BorderPen, _targetRect, 18, 18);
    }
}
