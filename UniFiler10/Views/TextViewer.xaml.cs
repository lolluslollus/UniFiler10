using System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

// The User Control item template is documented at http://go.microsoft.com/fwlink/?LinkId=234236

namespace UniFiler10.Views
{
	public sealed partial class TextViewer : UserControl
	{
		public event EventHandler<bool> UserAnswered;

		private volatile bool _isHasUserInteracted = false;
		public bool IsHasUserInteracted { get { return _isHasUserInteracted; } private set { _isHasUserInteracted = value; } }


		public TextViewer(string text)
		{
			InitializeComponent();
			One.Text = text;
		}

		private void OnBack_Click(object sender, RoutedEventArgs e)
		{
			IsHasUserInteracted = true;
			UserAnswered?.Invoke(this, true);
		}
	}
}
