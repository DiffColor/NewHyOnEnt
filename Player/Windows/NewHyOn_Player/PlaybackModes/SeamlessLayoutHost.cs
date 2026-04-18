using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace NewHyOnPlayer.PlaybackModes
{
    internal sealed class SeamlessLayoutHost : Canvas
    {
        public SeamlessLayoutHost()
        {
            Background = Brushes.Black;
            ClipToBounds = true;
            Visibility = System.Windows.Visibility.Hidden;
            Opacity = 0.0;
            IsHitTestVisible = false;
        }

        public void SetCanvasSize(double width, double height)
        {
            Width = width;
            Height = height;
        }

        public void ConfigurePresentation(double canvasWidth, double canvasHeight, double viewportWidth, double viewportHeight, bool preserveAspectRatio)
        {
            SetCanvasSize(canvasWidth, canvasHeight);

            double safeCanvasWidth = canvasWidth > 0 ? canvasWidth : 1;
            double safeCanvasHeight = canvasHeight > 0 ? canvasHeight : 1;
            double safeViewportWidth = viewportWidth > 0 ? viewportWidth : safeCanvasWidth;
            double safeViewportHeight = viewportHeight > 0 ? viewportHeight : safeCanvasHeight;
            var transformGroup = new TransformGroup();

            if (preserveAspectRatio)
            {
                double scale = System.Math.Min(safeViewportWidth / safeCanvasWidth, safeViewportHeight / safeCanvasHeight);
                if (double.IsNaN(scale) || double.IsInfinity(scale) || scale <= 0)
                {
                    scale = 1.0;
                }

                double offsetX = (safeViewportWidth - (safeCanvasWidth * scale)) / 2.0;
                double offsetY = (safeViewportHeight - (safeCanvasHeight * scale)) / 2.0;
                transformGroup.Children.Add(new ScaleTransform(scale, scale));
                transformGroup.Children.Add(new TranslateTransform(offsetX, offsetY));
            }
            else
            {
                double scaleX = safeViewportWidth / safeCanvasWidth;
                double scaleY = safeViewportHeight / safeCanvasHeight;
                if (double.IsNaN(scaleX) || double.IsInfinity(scaleX) || scaleX <= 0)
                {
                    scaleX = 1.0;
                }

                if (double.IsNaN(scaleY) || double.IsInfinity(scaleY) || scaleY <= 0)
                {
                    scaleY = 1.0;
                }

                transformGroup.Children.Add(new ScaleTransform(scaleX, scaleY));
            }

            RenderTransformOrigin = new Point(0, 0);
            RenderTransform = transformGroup;
        }

        public void ResetPresentation()
        {
            RenderTransformOrigin = new Point(0, 0);
            RenderTransform = Transform.Identity;
        }

        public void AttachSurfaces(IEnumerable<SeamlessContentSlot> slots)
        {
            Children.Clear();
            if (slots == null)
            {
                return;
            }

            foreach (SeamlessContentSlot slot in slots)
            {
                if (slot == null)
                {
                    continue;
                }

                Children.Add(slot.View);
            }
        }
    }
}
