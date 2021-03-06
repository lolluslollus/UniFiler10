﻿using System.Threading.Tasks;
using UniFiler10.Controlz;
using UniFiler10.Data.Model;
using UniFiler10.ViewModels;
using Utilz.Controlz;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Navigation;

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=234238

namespace UniFiler10.Views
{
	/// <summary>
	/// An empty page that can be used on its own or navigated to within a Frame.
	/// </summary>
	public sealed partial class BriefcasePage : OpenableObservablePage
	{
		#region properties
		private BriefcaseVM _vm = null;
        public BriefcaseVM VM { get { return _vm; } set { _vm = value; RaisePropertyChanged_UI(); } }

		private readonly AnimationStarter _animationStarter = null;
		#endregion properties


		#region construct dispose open close
		public BriefcasePage()
        {
			LastNavigatedPageRegKey = App.LAST_NAVIGATED_PAGE_REG_KEY;
			NavigationCacheMode = NavigationCacheMode.Enabled;
			InitializeComponent();
			_animationStarter = AnimationsControl.AnimationStarter;
			//_animationStarter = new AnimationStarter(new Storyboard[] { UpdatingStoryboard, SuccessStoryboard, FailureStoryboard });
		}

		protected override async Task OpenMayOverrideAsync(object args = null)
		{
			await AnimationsControl.OpenAsync();

			if (_vm == null) _vm = new BriefcaseVM(_animationStarter);
			await _vm.OpenAsync().ConfigureAwait(true);
			RaisePropertyChanged_UI(nameof(VM));

			//await BriefcaseCoverView.OpenAsync().ConfigureAwait(false);

			await AnimationsControl.OpenAsync().ConfigureAwait(true);

			//RegisterIsShowingBinderHandler();
			// LOLLO NOTE do not set the datacontext of the whole control or it will alter the dependency properties, if any. 
			// Instead, set LayoutRoot.DataContext, where LayoutRoot is the main child of the Page or UserControl.
			// For example:
			// LayoutRoot.DataContext = VM;
		}
		protected override async Task CloseMayOverrideAsync()
		{
			var vm = _vm;
			if (vm != null)
			{
				await vm.CloseAsync();
				vm.Dispose();
				VM = null;
			}

			//await BriefcaseCoverView.CloseAsync().ConfigureAwait(false);

			await AnimationsControl.CloseAsync().ConfigureAwait(false);
		}
		#endregion construct dispose open close


		#region event handlers
		private void OnAddBinderStep0_Tapped(object sender, TappedRoutedEventArgs e)
		{
			Task add = VM?.AddDbStep0Async();
		}
		private void OnAddBinderStep1_Tapped(object sender, TappedRoutedEventArgs e)
		{
			Task add = VM?.AddDbStep1Async();
		}

		//private void OnBackupButton_Tapped(object sender, TappedRoutedEventArgs e)
		//{
		//	VM?.StartExportBinder((sender as FrameworkElement)?.DataContext as string);
		//}

		private void OnImportButton_Tapped(object sender, TappedRoutedEventArgs e)
		{
			VM?.StartImportBinderIntoBriefcase();
		}

		//private void OnDeleteButton_Tapped(object sender, TappedRoutedEventArgs e)
		//{
		//	Task delete = VM?.DeleteDbAsync((sender as FrameworkElement)?.DataContext as string);
		//}
		private async void OnSettingsButton_Tapped(object sender, TappedRoutedEventArgs e)
		{
			var vm = _vm;
			if (vm != null)
			{
				await vm.CloseBinderAsync();
			}
			Frame.Navigate(typeof(SettingsPage));
		}
		private void OnDelete_Tapped(object sender, DocumentView.DocumentClickedArgs e)
		{
			Task delete = VM?.DeleteDbAsync((sender as FrameworkElement)?.DataContext as string);
		}
		private void OnImport_Tapped(object sender, DocumentView.DocumentClickedArgs e)
		{
			VM?.StartImportBinderIntoBinder((sender as FrameworkElement)?.DataContext as string);
		}
		private void OnSave_Tapped(object sender, DocumentView.DocumentClickedArgs e)
		{
			VM?.StartExportBinder((sender as FrameworkElement)?.DataContext as string);
		}
		private void OnBinderPreview_Tapped(object sender, DocumentView.DocumentClickedArgs e)
		{
			Task open = OpenBinder((sender as FrameworkElement)?.DataContext?.ToString());
		}
		private void OnBinderPreviews_ItemClick(object sender, ItemClickEventArgs e)
		{
			Task open = OpenBinder(e?.ClickedItem?.ToString());
		}
		private async Task OpenBinder(string dbName)
		{
			var vm = VM;
			if (vm != null && !string.IsNullOrWhiteSpace(dbName))
			{
				if (await vm.TryOpenCurrentBinderAsync(dbName))
				{
					Frame.Navigate(typeof(BriefcaseContentPage));
				}
			}
		}
		#endregion event handlers
	}
}