using Microsoft.CSharp.RuntimeBinder;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using UniFiler10.Controlz;
using UniFiler10.Utilz;
using UniFiler10.ViewModels;
using Utilz;
using Windows.Devices.Sensors;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Foundation.Metadata;
using Windows.Graphics.Display;
using Windows.Phone.UI.Input;
using Windows.UI.Core;
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
	public sealed partial class BinderCoverView : BackableOpenableObservableControl, IAnimationStarter
	{
		#region properties
		private BinderCoverVM _vm = null;
		public BinderCoverVM VM { get { return _vm; } set { _vm = value; RaisePropertyChanged_UI(); } }
		#endregion properties

		#region construct, open, close
		public BinderCoverView()
		{
			//TriggerOpenCloseWhenLoadedUnloaded = false;
			InitializeComponent();
		}

		protected override async Task<bool> TryOpenMayOverrideAsync()
		{
			var binder = DataContext as Data.Model.Binder;
			if (await base.TryOpenMayOverrideAsync() && binder != null && !binder.IsDisposed)
			{
				if (_vm == null || _vm.Binder != binder)
				{
					_vm = new BinderCoverVM(binder, this);
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
				await _vmSemaphore.WaitAsync(); //.ConfigureAwait(false);

				var newBinder = DataContext as Data.Model.Binder;
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

		protected override void GoBackMustOverride()
		{
			_vm?.GoBack();
		}
		private void OnFolderPreviews_ItemClick(object sender, ItemClickEventArgs e)
		{
			Task all = _vm?.SelectFolderAsync((e?.ClickedItem as Data.Model.Binder.FolderPreview)?.FolderId);
		}

		private void OnDeleteFolder_Tapped(object sender, TappedRoutedEventArgs e)
		{
			var fp = (sender as FrameworkElement).DataContext as Data.Model.Binder.FolderPreview;
			Task del = _vm?.DeleteFolderAsync(fp);
		}
		private void OnAddFolder_Tapped(object sender, TappedRoutedEventArgs e)
		{
			Task del = _vm?.AddFolderAsync();
		}

		private void OnAddOpenFolder_Tapped(object sender, TappedRoutedEventArgs e)
		{
			Task del = _vm?.AddOpenFolderAsync();
		}

		private void OnSettingsButton_Tapped(object sender, TappedRoutedEventArgs e)
		{
			_vm?.GoToSettings();
		}
		#endregion event handlers

		public void StartAnimation()
		{
			RunInUiThread(delegate
			{
				UpdatingStoryboard.Begin();
			});
		}
		public void EndAnimation()
		{
			RunInUiThread(delegate
			{
				UpdatingStoryboard.SkipToFill();
				UpdatingStoryboard.Stop();
			});
		}
	}
}
