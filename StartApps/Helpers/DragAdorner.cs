using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;

namespace StartApps.Helpers;

public class DragAdorner : Adorner
{
    private readonly ImageSource _imageSource;
    private readonly System.Windows.Size _adornerSize;
    private double _leftOffset;
    private double _topOffset;

    public DragAdorner(UIElement adornedElement, ImageSource imageSource, System.Windows.Size adornerSize)
        : base(adornedElement)
    {
        IsHitTestVisible = false;
        _imageSource = imageSource;
        _adornerSize = adornerSize;
    }

    public void SetPosition(System.Windows.Point cursorPosition)
    {
        _leftOffset = cursorPosition.X - (_adornerSize.Width / 2);
        _topOffset = cursorPosition.Y - (_adornerSize.Height / 2);
        InvalidateVisual();
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        if (_imageSource == null)
        {
            return;
        }

        drawingContext.DrawImage(_imageSource,
            new Rect(new System.Windows.Point(_leftOffset, _topOffset), _adornerSize));
    }
}
