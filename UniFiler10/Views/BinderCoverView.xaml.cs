using System;
using System.Threading.Tasks;
using UniFiler10.Controlz;
using UniFiler10.Data.Model;
using Utilz;
using UniFiler10.ViewModels;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media.Animation;

// The User Control item template is documented at http://go.microsoft.com/fwlink/?LinkId=234236

namespace UniFiler10.Views
{
	public sealed partial class BinderCoverView : OpenableObservableControl, IAnimationStarter
	{
		#region events
		public event EventHandler GoToSettingsRequested;
		public event EventHandler GoToBinderContentRequested;
		#endregion events


		#region properties
		private BinderCoverVM _vm = null;
		public BinderCoverVM VM { get { return _vm; } set { _vm = value; RaisePropertyChanged_UI(); } }
		#endregion properties


		#region construct, open, close
		public BinderCoverView()
		{
			DataContextChanged += OnDataContextChanged;
			InitializeComponent();
		}


		protected override Task OpenMayOverrideAsync()
		{
			return UpdateVMAsync();
		}

		private async Task UpdateVMAsync()
		{
			var binder = DataContext as Binder;
			if (binder != null && !binder.IsDisposed)
			{
				if (_vm == null)
				{
					_vm = new BinderCoverVM(binder, this);
					await _vm.OpenAsync().ConfigureAwait(false);
					RaisePropertyChanged_UI(nameof(VM));
				}
				else if (_vm.Binder != binder)
				{
					await DisposeVMAsync().ConfigureAwait(false);

					_vm = new BinderCoverVM(binder, this);
					await _vm.OpenAsync().ConfigureAwait(false);
					RaisePropertyChanged_UI(nameof(VM));
				}
			}
			else
			{
				await DisposeVMAsync().ConfigureAwait(false);
			}
		}

		protected override async Task CloseMayOverrideAsync()
		{
			await DisposeVMAsync().ConfigureAwait(false);
			EndAnimation(0);
		}

		private async Task DisposeVMAsync()
		{
			var vm = _vm;
			if (vm != null)
			{
				await vm.CloseAsync();
				vm.Dispose();
				VM = null;
			}
		}
		#endregion construct, open, close


		#region event handlers
		private void OnDataContextChanged(FrameworkElement sender, DataContextChangedEventArgs args)
		{
			Task upd = RunFunctionWhileOpenAsyncT(delegate
			{
				return UpdateVMAsync();
			});
		}

		private void OnFolderPreviews_ItemClick(object sender, ItemClickEventArgs e)
		{
			Task upd = RunFunctionWhileOpenAsyncT(async delegate
			{
				var vm = _vm;
				if (vm != null)
				{
					// LOLLO NOTE await instance?.method crashes if instance is null; await is not that clever yet.
					if (await vm.SetCurrentFolderAsync((e?.ClickedItem as Binder.FolderPreview)?.FolderId))
					{
						GoToBinderContentRequested?.Invoke(this, EventArgs.Empty);
					}
				}
			});
		}

		private void OnDeleteFolder_Tapped(object sender, TappedRoutedEventArgs e)
		{
			Task del = _vm?.DeleteFolderAsync((sender as FrameworkElement).DataContext as Binder.FolderPreview);
		}
		private void OnAddFolder_Tapped(object sender, TappedRoutedEventArgs e)
		{
			Task del = _vm?.AddFolderAsync();
		}

		private void OnAddAndOpenFolder_Tapped(object sender, TappedRoutedEventArgs e)
		{
			Task upd = RunFunctionWhileOpenAsyncT(async delegate
			{
				var vm = _vm;
				if (vm != null)
				{
					if (await vm.AddAndOpenFolderAsync()) //.ConfigureAwait(false);
					{
						GoToBinderContentRequested?.Invoke(this, EventArgs.Empty);
					}
				}
			});
		}

		private void OnImportFoldersFromBinder_Tapped(object sender, TappedRoutedEventArgs e)
		{
			Task imp = _vm?.ImportFoldersFromBinderAsync();
		}

		private void OnSettingsButton_Tapped(object sender, TappedRoutedEventArgs e)
		{
			GoToSettingsRequested?.Invoke(this, EventArgs.Empty);
		}
		#endregion event handlers


		#region animations
		public void StartAnimation(int whichAnimation = 0)
		{
			Task start = RunInUiThreadAsync(delegate
			{
				UpdatingStoryboard.Begin();
			});
		}
		public void EndAnimation(int whichAnimation = 0)
		{
			Task end = RunInUiThreadAsync(delegate
			{
				UpdatingStoryboard.SkipToFill();
				UpdatingStoryboard.Stop();
			});
		}
		#endregion animations
	}
}