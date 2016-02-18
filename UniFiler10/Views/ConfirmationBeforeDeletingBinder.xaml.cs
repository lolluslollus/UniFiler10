using System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

// The User Control item template is documented at http://go.microsoft.com/fwlink/?LinkId=234236

namespace UniFiler10.Views
{
	public sealed partial class ConfirmationBeforeDeletingBinder : UserControl
	{
		public event EventHandler<bool> UserAnswered;

		public bool YesNo
		{
			get { return (bool)GetValue(YesNoProperty); }
			set { SetValue(YesNoProperty, value); }
		}
		public static readonly DependencyProperty YesNoProperty =
			DependencyProperty.Register("YesNo", typeof(bool), typeof(ConfirmationBeforeDeletingBinder), new PropertyMetadata(false));

		private volatile bool _isHasUserInteracted = false;
		public bool IsHasUserInteracted { get { return _isHasUserInteracted; } private set { _isHasUserInteracted = value; } }


		public ConfirmationBeforeDeletingBinder()
		{
			InitializeComponent();
		}


		private void OnYes_Click(object sender, RoutedEventArgs e)
		{
			YesNo = true;
			IsHasUserInteracted = true;
			UserAnswered?.Invoke(this, YesNo);
		}

		private void OnNo_Click(object sender, RoutedEventArgs e)
		{
			YesNo = false;
			IsHasUserInteracted = true;
			UserAnswered?.Invoke(this, YesNo);
		}
	}
}
