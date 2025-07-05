using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Shapes;

namespace OverlayScope 
{
    public partial class AreaSelectorWindow : Window
    {
        private Point _startPoint;
        public Rect SelectedArea { get; private set; }

        public AreaSelectorWindow()
        {
            InitializeComponent();
        }

        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            _startPoint = e.GetPosition(this);
            SelectionRectangle.SetValue(Canvas.LeftProperty, _startPoint.X);
            SelectionRectangle.SetValue(Canvas.TopProperty, _startPoint.Y);
            SelectionRectangle.Width = 0;
            SelectionRectangle.Height = 0;
            SelectionRectangle.Visibility = Visibility.Visible;
        }

        private void Window_MouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                Point currentPoint = e.GetPosition(this);
                double x = Math.Min(_startPoint.X, currentPoint.X);
                double y = Math.Min(_startPoint.Y, currentPoint.Y);
                double width = Math.Abs(_startPoint.X - currentPoint.X);
                double height = Math.Abs(_startPoint.Y - currentPoint.Y);
                SelectionRectangle.SetValue(Canvas.LeftProperty, x);
                SelectionRectangle.SetValue(Canvas.TopProperty, y);
                SelectionRectangle.Width = width;
                SelectionRectangle.Height = height;
            }
        }

        private void Window_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (SelectionRectangle.Width > 0 && SelectionRectangle.Height > 0)
            {
                double x = (double)SelectionRectangle.GetValue(Canvas.LeftProperty);
                double y = (double)SelectionRectangle.GetValue(Canvas.TopProperty);
                SelectedArea = new Rect(x, y, SelectionRectangle.Width, SelectionRectangle.Height);
            }
            this.DialogResult = true;
            this.Close();
        }
    }
}