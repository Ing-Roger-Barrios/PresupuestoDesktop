using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows;

namespace PresupuestoPro.Behaviors
{
    public class InsertionLineAdorner : Adorner
    {
        private readonly Pen _pen;
        private double _lineY;

        public InsertionLineAdorner(UIElement adornedElement) : base(adornedElement)
        {
            _pen = new Pen(new SolidColorBrush(Colors.Blue), 2)
            {
                DashStyle = new DashStyle(new double[] { 2, 2 }, 0), // Fixed: Define a custom DashStyle
                DashCap = PenLineCap.Round
            };
        }

        public void SetLinePosition(double y)
        {
            _lineY = y;
            InvalidateVisual();
        }

        protected override void OnRender(DrawingContext drawingContext)
        {
            var adornerLayerSize = new Point(ActualWidth, ActualHeight);
            if (adornerLayerSize.X <= 0 || adornerLayerSize.Y <= 0)
                return;

            drawingContext.DrawLine(_pen, new Point(0, _lineY), new Point(adornerLayerSize.X, _lineY));
        }
    }
}
