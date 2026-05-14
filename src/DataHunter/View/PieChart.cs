using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using DataHunter.ViewModel;

namespace DataHunter.View
{
	public class PieChart : FrameworkElement
	{
		private readonly List<RenderedSlice> renderedSlices = new List<RenderedSlice>();

		public PieChart()
		{
			Focusable = true;
		}

		public static readonly DependencyProperty SlicesProperty = DependencyProperty.Register(
			nameof(Slices),
			typeof(IEnumerable<PieSlice>),
			typeof(PieChart),
			new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

		public static readonly DependencyProperty HighlightedSliceProperty = DependencyProperty.Register(
			nameof(HighlightedSlice),
			typeof(PieSlice),
			typeof(PieChart),
			new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender | FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, HighlightedSliceChanged));

		private static readonly DependencyProperty PopoutProgressProperty = DependencyProperty.Register(
			nameof(PopoutProgress),
			typeof(double),
			typeof(PieChart),
			new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender));

		public event EventHandler<PieSliceClickedEventArgs> SliceClicked;

		public IEnumerable<PieSlice> Slices
		{
			get => (IEnumerable<PieSlice>)GetValue(SlicesProperty);
			set => SetValue(SlicesProperty, value);
		}

		public PieSlice HighlightedSlice
		{
			get => (PieSlice)GetValue(HighlightedSliceProperty);
			set => SetValue(HighlightedSliceProperty, value);
		}

		private double PopoutProgress
		{
			get => (double)GetValue(PopoutProgressProperty);
			set => SetValue(PopoutProgressProperty, value);
		}

		protected override void OnRender(DrawingContext drawingContext)
		{
			base.OnRender(drawingContext);
			renderedSlices.Clear();

			var slices = Slices?.Where(s => s.Bytes > 0).ToList();
			if(slices == null || slices.Count == 0)
			{
				DrawEmptyState(drawingContext);
				return;
			}

			var total = slices.Sum(s => s.Bytes);
			var radius = Math.Max(0, Math.Min(ActualWidth, ActualHeight) / 2 - 4);
			if(radius <= 0)
				return;

			var center = new Point(ActualWidth / 2, ActualHeight / 2);
			var rect = new Rect(center.X - radius, center.Y - radius, radius * 2, radius * 2);
			var startAngle = -90.0;

			foreach(var slice in slices)
			{
				var sweepAngle = 360.0 * slice.Bytes / total;
				var highlighted = IsHighlighted(slice);
				var offset = highlighted ? PointOnCircle(new Point(), 8 * PopoutProgress, startAngle + sweepAngle / 2) : new Point();
				var sliceCenter = new Point(center.X + offset.X, center.Y + offset.Y);
				var sliceRect = new Rect(rect.X + offset.X, rect.Y + offset.Y, rect.Width, rect.Height);
				DrawSlice(drawingContext, sliceCenter, sliceRect, startAngle, sweepAngle, slice.Brush, highlighted);
				renderedSlices.Add(new RenderedSlice(slice, startAngle, sweepAngle, center, radius));
				startAngle += sweepAngle;
			}
		}

		protected override void OnMouseMove(MouseEventArgs e)
		{
			base.OnMouseMove(e);
			var slice = HitTestSlice(e.GetPosition(this));
			if(!IsSameSlice(slice, HighlightedSlice))
				HighlightedSlice = slice;
			Cursor = slice?.Source == null ? Cursors.Arrow : Cursors.Hand;
		}

		protected override void OnMouseLeave(MouseEventArgs e)
		{
			base.OnMouseLeave(e);
			HighlightedSlice = null;
			Cursor = Cursors.Arrow;
		}

		protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
		{
			base.OnMouseLeftButtonUp(e);
			Focus();
			var slice = HitTestSlice(e.GetPosition(this));
			if(slice?.Source == null)
				return;

			SliceClicked?.Invoke(this, new PieSliceClickedEventArgs(slice));
			e.Handled = true;
		}

		protected override void OnGotKeyboardFocus(KeyboardFocusChangedEventArgs e)
		{
			base.OnGotKeyboardFocus(e);
			if(HighlightedSlice == null)
				MoveHighlight(1);
		}

		protected override void OnKeyDown(KeyEventArgs e)
		{
			base.OnKeyDown(e);
			if(e.Key == Key.Right || e.Key == Key.Down)
			{
				MoveHighlight(1);
				e.Handled = true;
				return;
			}

			if(e.Key == Key.Left || e.Key == Key.Up)
			{
				MoveHighlight(-1);
				e.Handled = true;
				return;
			}

			if(e.Key != Key.Enter && e.Key != Key.Space)
				return;

			if(HighlightedSlice?.Source == null)
				return;

			SliceClicked?.Invoke(this, new PieSliceClickedEventArgs(HighlightedSlice));
			e.Handled = true;
		}

		private void MoveHighlight(int direction)
		{
			var slices = Slices?.Where(s => s.Bytes > 0).ToList();
			if(slices == null || slices.Count == 0)
				return;

			var currentIndex = HighlightedSlice == null ? -1 : slices.FindIndex(s => IsSameSlice(s, HighlightedSlice));
			var nextIndex = (currentIndex + direction + slices.Count) % slices.Count;
			HighlightedSlice = slices[nextIndex];
		}

		protected override AutomationPeer OnCreateAutomationPeer()
		{
			return new PieChartAutomationPeer(this);
		}

		private static void DrawEmptyState(DrawingContext drawingContext)
		{
		}

		private static void HighlightedSliceChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
		{
			var chart = (PieChart)dependencyObject;
			var animation = new DoubleAnimation(e.NewValue == null ? 0.0 : 1.0, TimeSpan.FromMilliseconds(120))
			{
				EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
			};
			chart.BeginAnimation(PopoutProgressProperty, animation);
		}

		private static void DrawSlice(DrawingContext drawingContext, Point center, Rect rect, double startAngle, double sweepAngle, Brush brush, bool highlighted)
		{
			var pen = highlighted ? new Pen(Brushes.White, 1.5) : null;
			if(sweepAngle >= 359.99)
			{
				drawingContext.DrawEllipse(brush, pen, center, rect.Width / 2, rect.Height / 2);
				return;
			}

			var start = PointOnCircle(center, rect.Width / 2, startAngle);
			var end = PointOnCircle(center, rect.Width / 2, startAngle + sweepAngle);
			var geometry = new StreamGeometry();
			using(var context = geometry.Open())
			{
				context.BeginFigure(center, true, true);
				context.LineTo(start, true, false);
				context.ArcTo(end, new Size(rect.Width / 2, rect.Height / 2), 0, sweepAngle > 180, SweepDirection.Clockwise, true, false);
			}

			geometry.Freeze();
			drawingContext.DrawGeometry(brush, pen, geometry);
		}

		private PieSlice HitTestSlice(Point point)
		{
			foreach(var slice in renderedSlices)
			{
				var dx = point.X - slice.Center.X;
				var dy = point.Y - slice.Center.Y;
				if(Math.Sqrt(dx * dx + dy * dy) > slice.Radius)
					continue;

				var angle = Math.Atan2(dy, dx) * 180.0 / Math.PI;
				if(angle < -90)
					angle += 360;

				if(angle >= slice.StartAngle && angle <= slice.StartAngle + slice.SweepAngle)
					return slice.Slice;
			}

			return null;
		}

		private bool IsHighlighted(PieSlice slice)
		{
			return IsSameSlice(slice, HighlightedSlice);
		}

		private static bool IsSameSlice(PieSlice left, PieSlice right)
		{
			if(left == null || right == null)
				return false;

			if(left.Source != null || right.Source != null)
				return ReferenceEquals(left.Source, right.Source);

			return string.Equals(left.Name, right.Name, StringComparison.Ordinal);
		}

		private static Point PointOnCircle(Point center, double radius, double angle)
		{
			var radians = Math.PI * angle / 180.0;
			return new Point(center.X + radius * Math.Cos(radians), center.Y + radius * Math.Sin(radians));
		}

		private class RenderedSlice
		{
			public RenderedSlice(PieSlice slice, double startAngle, double sweepAngle, Point center, double radius)
			{
				Slice = slice;
				StartAngle = startAngle;
				SweepAngle = sweepAngle;
				Center = center;
				Radius = radius;
			}

			public PieSlice Slice { get; }

			public double StartAngle { get; }

			public double SweepAngle { get; }

			public Point Center { get; }

			public double Radius { get; }
		}

		private class PieChartAutomationPeer : FrameworkElementAutomationPeer
		{
			public PieChartAutomationPeer(PieChart owner) : base(owner)
			{
			}

			protected override string GetClassNameCore()
			{
				return nameof(PieChart);
			}

			protected override AutomationControlType GetAutomationControlTypeCore()
			{
				return AutomationControlType.Custom;
			}
		}
	}

	public class PieSliceClickedEventArgs : EventArgs
	{
		public PieSliceClickedEventArgs(PieSlice slice)
		{
			Slice = slice;
		}

		public PieSlice Slice { get; }
	}
}
