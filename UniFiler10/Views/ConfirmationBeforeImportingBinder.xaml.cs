﻿using System;
using UniFiler10.ViewModels;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

// The User Control item template is documented at http://go.microsoft.com/fwlink/?LinkId=234236

namespace UniFiler10.Views
{
	public sealed partial class ConfirmationBeforeImportingBinder : UserControl
	{
		public event EventHandler<BriefcaseVM.ImportBinderOperations> UserAnswered;

		public BriefcaseVM.ImportBinderOperations Operation
		{
			get { return (BriefcaseVM.ImportBinderOperations)GetValue(OperationProperty); }
			set { SetValue(OperationProperty, value); }
		}
		public static readonly DependencyProperty OperationProperty =
			DependencyProperty.Register("Operation", typeof(BriefcaseVM.ImportBinderOperations), typeof(ConfirmationBeforeImportingBinder), new PropertyMetadata(BriefcaseVM.ImportBinderOperations.Cancel));

		private volatile bool _isHasUserInteracted = false;
		public bool IsHasUserInteracted { get { return _isHasUserInteracted; } private set { _isHasUserInteracted = value; } }


		public ConfirmationBeforeImportingBinder()
		{
			InitializeComponent();
		}

		private void OnMerge_Click(object sender, RoutedEventArgs e)
		{
			Operation = BriefcaseVM.ImportBinderOperations.Merge;
			IsHasUserInteracted = true;
			UserAnswered?.Invoke(this, Operation);
		}

		private void OnOverwrite_Click(object sender, RoutedEventArgs e)
		{
			Operation = BriefcaseVM.ImportBinderOperations.Import;
			IsHasUserInteracted = true;
			UserAnswered?.Invoke(this, Operation);
		}
		private void OnCancel_Click(object sender, RoutedEventArgs e)
		{
			Operation = BriefcaseVM.ImportBinderOperations.Cancel;
			IsHasUserInteracted = true;
			UserAnswered?.Invoke(this, Operation);
		}
	}
}
