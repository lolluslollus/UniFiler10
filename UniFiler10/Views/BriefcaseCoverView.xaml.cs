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
using Windows.ApplicationModel.Resources;
using Windows.Devices.Sensors;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Foundation.Metadata;
using Windows.Graphics.Display;
using Windows.Phone.UI.Input;
using Windows.UI.Core;
using Windows.UI.Popups;
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
	public sealed partial class BriefcaseCoverView : OpenableObservableControl, IAnimationStarter
	{
		#region properties
		private BriefcaseVM _vm = null;
		public BriefcaseVM VM { get { return _vm; } set { _vm = value; RaisePropertyChanged_UI(); } }
		#endregion properties


		#region construct, dispose, open, close
		public BriefcaseCoverView()
		{
			OpenCloseWhenLoadedUnloaded = false;
			InitializeComponent();
		}

		protected override async Task<bool> OpenMayOverrideAsync()
		{
			RunInUiThread(delegate { RegisterEventHandlers(); });
			await Task.CompletedTask;
			return true;
		}
		protected override Task CloseMayOverrideAsync()
		{
			RunInUiThread(delegate { UnregisterEventHandlers(); });
			return Task.CompletedTask;
		}
		private void RegisterEventHandlers()
		{
			if (ApiInformation.IsTypePresent("Windows.Phone.UI.Input.HardwareButtons"))
			{
				HardwareButtons.BackPressed += OnHardwareButtons_BackPressed;
			}
			SystemNavigationManager.GetForCurrentView().BackRequested += OnTabletSoftwareButton_BackPressed;
		}
		private void UnregisterEventHandlers()
		{
			if (ApiInformation.IsTypePresent("Windows.Phone.UI.Input.HardwareButtons"))
			{
				HardwareButtons.BackPressed -= OnHardwareButtons_BackPressed;
			}
			SystemNavigationManager.GetForCurrentView().BackRequested -= OnTabletSoftwareButton_BackPressed;
		}
		#endregion construct, dispose, open, close


		#region event handlers
		private void OnBackButton_Tapped(object sender, TappedRoutedEventArgs e)
		{
			GoBack();
		}
		private void OnHardwareButtons_BackPressed(object sender, BackPressedEventArgs e)
		{
			GoBack();
		}
		private void OnTabletSoftwareButton_BackPressed(object sender, Windows.UI.Core.BackRequestedEventArgs e)
		{
			GoBack();
		}

		private void GoBack()
		{
			VM?.Briefcase?.SetIsCoverOpen(false);
		}

		private void OnFolderPreviews_ItemClick(object sender, ItemClickEventArgs e)
		{
			bool isOpen = _vm?.OpenBinder(e?.ClickedItem?.ToString()) == true;
			if (isOpen) _vm?.Briefcase?.SetIsCoverOpen(false);
		}

		private void OnAddBinderStep0_Tapped(object sender, TappedRoutedEventArgs e)
		{
			_vm?.AddDbStep0();
		}
		private void OnAddBinderStep1_Tapped(object sender, TappedRoutedEventArgs e)
		{
			_vm?.AddDbStep1();
		}

		private void OnBackupButton_Tapped(object sender, TappedRoutedEventArgs e)
		{
			Task backup = _vm?.BackupDbAsync((sender as FrameworkElement)?.DataContext as string);
		}

		private void OnRestoreButton_Tapped(object sender, TappedRoutedEventArgs e)
		{
			Task restore = _vm?.RestoreDbAsync();
		}

		private void OnDeleteButton_Tapped(object sender, TappedRoutedEventArgs e)
		{
			Task delete = _vm?.DeleteDbAsync((sender as FrameworkElement)?.DataContext as string);
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
