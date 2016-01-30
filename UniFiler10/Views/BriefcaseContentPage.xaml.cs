﻿using System;
using System.ComponentModel;
using System.Threading.Tasks;
using UniFiler10.Controlz;
using UniFiler10.Data.Model;
using UniFiler10.ViewModels;
using Utilz;
using Utilz.Controlz;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

// The User Control item template is documented at http://go.microsoft.com/fwlink/?LinkId=234236

namespace UniFiler10.Views
{
	public sealed partial class BriefcaseContentPage : OpenableObservablePage
	{
		private BriefcaseContentVM _vm = null;
		public BriefcaseContentVM VM { get { return _vm; } private set { _vm = value; RaisePropertyChanged_UI(); } }

		#region construct, open, close
		public BriefcaseContentPage()
		{
			InitializeComponent();
		}

		protected override async Task OpenMayOverrideAsync()
		{
			_vm = new BriefcaseContentVM();
			await _vm.OpenAsync();
			RaisePropertyChanged_UI(nameof(VM));

			await BinderCoverView.OpenAsync();
		}
		protected override async Task CloseMayOverrideAsync()
		{
			await BinderCoverView.CloseAsync();

			var vm = _vm;
			if (vm != null)
			{
				await vm.CloseAsync();
				vm.Dispose();
				VM = null;
			}
		}
		#endregion construct, open, close


		#region user actions
		private void OnOpenBriefcaseCover_Click(object sender, RoutedEventArgs e)
		{
			Frame.Navigate(typeof(BriefcasePage));
		}
		protected override bool GoBackMayOverride()
		{
			Frame.Navigate(typeof(BriefcasePage));
			return true;
		}
		private void OnBinderPreview_Click(object sender, ItemClickEventArgs e)
		{
			Task open = _vm?.OpenBinderAsync(e?.ClickedItem?.ToString());
		}

		private void OnBinderCoverView_GoToBinderContentRequested(object sender, EventArgs e)
		{
			Frame.Navigate(typeof(BinderContentPage));
		}

		private async void OnBinderCoverView_GoToSettingsRequested(object sender, EventArgs e)
		{
			var vm = _vm;
			if (vm != null)
			{
				await vm.CloseBinderAsync();
			}
			Frame.Navigate(typeof(SettingsPage));
		}
		#endregion user actions
	}
}