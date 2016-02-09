using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using UniFiler10.ViewModels;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

// The User Control item template is documented at http://go.microsoft.com/fwlink/?LinkId=234236

namespace UniFiler10.Views
{
	public sealed partial class ConfirmationBeforeExportingBinder : UserControl
	{
		public event EventHandler<bool> UserAnswered;

		public bool YesNo
		{
			get { return (bool)GetValue(YesNoProperty); }
			set { SetValue(YesNoProperty, value); }
		}
		public static readonly DependencyProperty YesNoProperty =
			DependencyProperty.Register("YesNo", typeof(bool), typeof(ConfirmationBeforeExportingBinder), new PropertyMetadata(false));

		private volatile bool _isHasUserInteracted = false;
		public bool IsHasUserInteracted { get { return _isHasUserInteracted; } private set { _isHasUserInteracted = value; } }


		public ConfirmationBeforeExportingBinder()
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
