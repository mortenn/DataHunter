using System;
using System.Windows;
using System.Windows.Controls;

namespace DataHunter.View
{
	public class ErrorDialog : Window
	{
		public ErrorDialog(Exception exception)
		{
			Title = "DataHunter error";
			Width = 700;
			Height = 420;
			MinWidth = 500;
			MinHeight = 300;
			WindowStartupLocation = WindowStartupLocation.CenterOwner;

			var details = exception.ToString();
			var summary = $"{exception.GetType().Name}: {exception.Message}";

			var root = new Grid { Margin = new Thickness(12) };
			root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
			root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
			root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

			var summaryText = new TextBlock
			{
				Text = summary,
				TextWrapping = TextWrapping.Wrap,
				Margin = new Thickness(0, 0, 0, 10),
				FontWeight = FontWeights.SemiBold,
			};
			Grid.SetRow(summaryText, 0);
			root.Children.Add(summaryText);

			var detailsText = new TextBox
			{
				Text = details,
				IsReadOnly = true,
				AcceptsReturn = true,
				AcceptsTab = true,
				TextWrapping = TextWrapping.NoWrap,
				VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
				HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
			};
			Grid.SetRow(detailsText, 1);
			root.Children.Add(detailsText);

			var buttons = new StackPanel
			{
				Orientation = Orientation.Horizontal,
				HorizontalAlignment = HorizontalAlignment.Right,
				Margin = new Thickness(0, 10, 0, 0),
			};

			var copyButton = new Button
			{
				Content = "Copy details",
				MinWidth = 100,
				Margin = new Thickness(0, 0, 8, 0),
			};
			copyButton.Click += (sender, args) => Clipboard.SetText(details);
			buttons.Children.Add(copyButton);

			var closeButton = new Button
			{
				Content = "Close",
				IsDefault = true,
				MinWidth = 80,
			};
			closeButton.Click += (sender, args) => Close();
			buttons.Children.Add(closeButton);

			Grid.SetRow(buttons, 2);
			root.Children.Add(buttons);

			Content = root;
		}
	}
}
