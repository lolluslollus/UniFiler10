using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using UniFiler10.Controlz;
using UniFiler10.Data.Metadata;
using UniFiler10.ViewModels;
using Utilz;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Foundation.Metadata;
using Windows.Phone.UI.Input;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=234238

namespace UniFiler10.Views
{
	public sealed partial class SettingsView : OpenableObservableControl
	{
		#region properties
		private SettingsVM _vm = null;
		public SettingsVM VM { get { return _vm; } set { _vm = value; RaisePropertyChanged_UI(); } }

		public BriefcaseVM BriefcaseVM
		{
			get { return (BriefcaseVM)GetValue(BriefcaseVMProperty); }
			set { SetValue(BriefcaseVMProperty, value); }
		}
		public static readonly DependencyProperty BriefcaseVMProperty =
			DependencyProperty.Register("BriefcaseVM", typeof(BriefcaseVM), typeof(SettingsView), new PropertyMetadata(null, OnBriefcaseVMChanged));
		private static void OnBriefcaseVMChanged(DependencyObject obj, DependencyPropertyChangedEventArgs args)
		{
			var instance = obj as SettingsView;
			if (instance != null && args != null && args.NewValue != args.OldValue)
			{
				instance.UpdateDataContext();
				Task upd = instance.UpdateOpenCloseAsync();
			}
		}
		#endregion properties


		#region construct dispose open close
		public SettingsView()
		{
			//TriggerOpenCloseWhenLoadedUnloaded = false;
			UpdateDataContext();
			InitializeComponent();
			//Task upd = UpdateOpenCloseAsync();
		}
		protected override async Task<bool> TryOpenMayOverrideAsync()
		{
			var mb = DataContext as MetaBriefcase;
			if (mb != null && !mb.IsDisposed && await base.TryOpenMayOverrideAsync())
			{
				if (_vm == null || _vm.MetaBriefcase != mb)
				{
					_vm = new SettingsVM(mb);
					RaisePropertyChanged_UI(nameof(VM));

					MBView.DataContext = DataContext;
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

			_vm?.Dispose();
			VM = null;
		}

		private static SemaphoreSlimSafeRelease _vmSemaphore = new SemaphoreSlimSafeRelease(1, 1);
		//private void OnDataContextChanged(FrameworkElement sender, DataContextChangedEventArgs args)
		//{
		//	Task upd = UpdateOpenCloseAsync();
		//}

		private void UpdateDataContext()
		{
			DataContext = BriefcaseVM?.Briefcase?.MetaBriefcase;
		}
		private async Task UpdateOpenCloseAsync()
		{
			try
			{
				await _vmSemaphore.WaitAsync().ConfigureAwait(false);

				var mb = DataContext as MetaBriefcase;
				if (mb == null)
				{
					await CloseAsync().ConfigureAwait(false);
				}
				else if (_vm == null || _vm.MetaBriefcase != mb)
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
		//protected override void GoBackMustOverride()
		//{
		//	BriefcaseVM?.ShowCover();
		//}
		#endregion construct dispose open close


		#region user actions
		private void OnGoToBinder_Tapped(object sender, TappedRoutedEventArgs e)
		{
			BriefcaseVM?.ShowBinder();
		}

		private void OnGoToBriefcase_Tapped(object sender, TappedRoutedEventArgs e)
		{
			BriefcaseVM?.ShowCover();
		}

		private void OnToggleElevated_Tapped(object sender, TappedRoutedEventArgs e)
		{
			MBView.DataContext = null;
			MBView.DataContext = DataContext;
		}
		#endregion user actions
	}
}
