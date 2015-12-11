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
using UniFiler10.Data.Model;
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
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Navigation;

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=234238

namespace UniFiler10.Views
{
	public sealed partial class SettingsPage : OpenableObservablePage
	{
		#region properties
		private SettingsVM _vm = null;
		public SettingsVM VM { get { return _vm; } set { _vm = value; RaisePropertyChanged_UI(); } }

		private AnimationStarter _animationStarter = null;
		#endregion properties


		#region construct dispose open close
		public SettingsPage()
		{
			InitializeComponent();
			MBView.DataContext = null; // otherwise, it will try something and run into binding errors. I am going to set its binding later.
			_animationStarter = new AnimationStarter(new Storyboard[] { SuccessStoryboard, FailureStoryboard });
		}
		protected override async Task OpenMayOverrideAsync()
		{
			var briefcase = Briefcase.GetCreateInstance();
			await briefcase.OpenAsync();

			_vm = new SettingsVM(briefcase.MetaBriefcase, _animationStarter);
			await _vm.OpenAsync();
			RaisePropertyChanged_UI(nameof(VM));

			//LayoutRoot.DataContext = VM;
			MBView.DataContext = VM.MetaBriefcase;
		}
		protected override async Task CloseMayOverrideAsync()
		{
			var vm = _vm;
			if (vm != null)
			{
				await vm.CloseAsync().ConfigureAwait(false);
				vm.Dispose();
			}
			_vm = null;

			_animationStarter.EndAllAnimations();
		}
		#endregion construct dispose open close


		#region user actions
		private void OnGoToBinderCover_Tapped(object sender, TappedRoutedEventArgs e)
		{
			Frame.Navigate(typeof(BriefcaseContentPage));
		}

		private void OnGoToBriefcase_Tapped(object sender, TappedRoutedEventArgs e)
		{
			Frame.Navigate(typeof(BriefcasePage));
		}

		private void OnToggleElevated_Tapped(object sender, TappedRoutedEventArgs e)
		{
			MBView.DataContext = null;
			MBView.DataContext = VM.MetaBriefcase;
		}

		private async void OnExport_Tapped(object sender, TappedRoutedEventArgs e)
		{
			var vm = _vm;
			if (vm != null)
			{
				await vm.ExportAsync().ConfigureAwait(false);
			}
		}

		private async void OnImport_Tapped(object sender, TappedRoutedEventArgs e)
		{
			var vm = _vm;
			if (vm != null)
			{
				await vm.ImportAsync(); // .ConfigureAwait(false);
				MBView.DataContext = null;
				MBView.DataContext = VM.MetaBriefcase;
			}
		}

		private void OnAbout_Tapped(object sender, TappedRoutedEventArgs e)
		{
			AboutFlyout.ShowAt(this);
		}
		#endregion user actions
	}
}
