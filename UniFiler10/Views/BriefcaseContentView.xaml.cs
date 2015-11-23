using System;
using System.Threading.Tasks;
using UniFiler10.Controlz;
using UniFiler10.Data.Model;
using UniFiler10.ViewModels;
using Utilz;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

// The User Control item template is documented at http://go.microsoft.com/fwlink/?LinkId=234236

namespace UniFiler10.Views
{
	public sealed partial class BriefcaseContentView : BackableOpenableObservableControl
	{
		private BinderVM _vm = null;
		public BinderVM VM { get { return _vm; } private set { _vm = value; RaisePropertyChanged_UI(); } }

		public BriefcaseVM BriefcaseVM
		{
			get { return (BriefcaseVM)GetValue(BriefcaseVMProperty); }
			set { SetValue(BriefcaseVMProperty, value); }
		}
		public static readonly DependencyProperty BriefcaseVMProperty =
			DependencyProperty.Register("BriefcaseVM", typeof(BriefcaseVM), typeof(BriefcaseContentView), new PropertyMetadata(null));


		#region construct, open, close
		public BriefcaseContentView()
		{
			//TriggerOpenCloseWhenLoadedUnloaded = false;
			InitializeComponent();
		}
		protected override async Task<bool> TryOpenMayOverrideAsync()
		{
			var binder = DataContext as Binder;
			if (binder != null && !binder.IsDisposed && await base.TryOpenMayOverrideAsync())
			{
				if (_vm == null || _vm.Binder != binder)
				{
					_vm = new BinderVM(binder);
					await _vm.OpenAsync().ConfigureAwait(false);
					RaisePropertyChanged_UI(nameof(VM));
				}
				return true;
			}
			else
			{
				return false;
			}
		}
		protected override async Task CloseMayOverrideAsync()
		{
			await base.CloseMayOverrideAsync();

			var vm = _vm;
			if (vm != null)
			{
				await vm.CloseAsync();
				vm.Dispose();
				VM = null;
			}
		}

		private static SemaphoreSlimSafeRelease _vmSemaphore = new SemaphoreSlimSafeRelease(1, 1);
		private async Task UpdateOpenCloseAsync()
		{
			try
			{
				await _vmSemaphore.WaitAsync().ConfigureAwait(false);

				var newBinder = DataContext as Binder;
				if (newBinder == null)
				{
					await CloseAsync().ConfigureAwait(false);
				}
				else if (_vm == null || _vm.Binder != newBinder)
				{
					await CloseAsync().ConfigureAwait(false);
					await TryOpenAsync().ConfigureAwait(false);
				}
			}
			finally
			{
				SemaphoreSlimSafeRelease.TryRelease(_vmSemaphore);
			}
		}
		#endregion construct, open, close


		#region event handlers
		private void OnDataContextChanged(FrameworkElement sender, DataContextChangedEventArgs args)
		{
			Task upd = UpdateOpenCloseAsync();
		}

		private void OnListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			Task sel = _vm?.SelectFolderAsync(((sender as ListView)?.SelectedItem as Folder)?.Id);
		}

		private void OnDeleteFolder_Click(object sender, RoutedEventArgs e)
		{
			Task del = _vm?.DeleteFolderAsync((sender as FrameworkElement)?.DataContext as Folder);
		}

		private void OnAddFolder_Click(object sender, RoutedEventArgs e)
		{
			Task add = _vm?.AddFolderAsync();
		}

		//private void OnTogglePaneOpen(object sender, RoutedEventArgs e)
		//{
		//    _vm?.TogglePaneOpen();
		//}

		private void OnOpenCover_Click(object sender, RoutedEventArgs e)
		{
			_vm?.OpenCover();
		}

		protected override void GoBackMustOverride()
		{
			_vm?.GoBack();
		}

		private void OnOpenBriefcaseCover_Click(object sender, RoutedEventArgs e)
		{
			_vm?.GoBack();
		}

		private void OnBinderListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			BriefcaseVM?.OpenBinder((sender as ListView)?.SelectedItem.ToString());
		}
		#endregion event handlers
	}
}