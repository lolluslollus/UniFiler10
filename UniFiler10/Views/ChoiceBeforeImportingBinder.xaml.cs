using System;
using System.Linq;
using UniFiler10.Data.Model;
using UniFiler10.ViewModels;
using Utilz;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

// The User Control item template is documented at http://go.microsoft.com/fwlink/?LinkId=234236

namespace UniFiler10.Views
{
	public sealed partial class ChoiceBeforeImportingBinder : UserControl
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

		private string _dbName = string.Empty;
		public string DBName => _dbName;

		private readonly Briefcase _briefcase = null;
		public Briefcase Briefcase => _briefcase;

		private readonly SwitchableObservableCollection<string> _dbNames = new SwitchableObservableCollection<string>();
		public SwitchableObservableCollection<string> DbNames { get { return _dbNames; } }

		public ChoiceBeforeImportingBinder(string targetBinderName)
		{
			_briefcase = Briefcase.GetCurrentInstance();
			_dbNames.ReplaceAll(_briefcase.DbNames.Where(dbn => dbn != targetBinderName));
			InitializeComponent();
		}

		private void OnCancel_Click(object sender, RoutedEventArgs e)
		{
			_dbName = string.Empty;
			Operation = BriefcaseVM.ImportBinderOperations.Cancel;
			IsHasUserInteracted = true;
			UserAnswered?.Invoke(this, Operation);
		}

		private void OnPickDirectory_Click(object sender, RoutedEventArgs e)
		{
			_dbName = string.Empty;
			Operation = BriefcaseVM.ImportBinderOperations.Import;
			IsHasUserInteracted = true;
			UserAnswered?.Invoke(this, Operation);
		}

		private void OnPickBinder_Click(object sender, RoutedEventArgs e)
		{
			_dbName = (sender as FrameworkElement).DataContext.ToString();
			Operation = BriefcaseVM.ImportBinderOperations.Import;
			IsHasUserInteracted = true;
			UserAnswered?.Invoke(this, Operation);
		}
	}
}