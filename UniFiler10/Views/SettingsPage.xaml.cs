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
using Windows.UI.Xaml.Navigation;

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=234238

namespace UniFiler10.Views
{
	public sealed partial class SettingsPage : OpenableObservablePage
	{
		#region properties
		private SettingsVM _vm = null;
		public SettingsVM VM { get { return _vm; } set { _vm = value; RaisePropertyChanged_UI(); } }
		#endregion properties


		#region construct dispose open close
		public SettingsPage()
		{
			InitializeComponent();
			MBView.DataContext = null;
		}
		protected override async Task OpenMayOverrideAsync()
		{
			var briefcase = Briefcase.GetOrCreateInstance();
			await briefcase.OpenAsync();

			_vm = new SettingsVM(briefcase.MetaBriefcase);
			RaisePropertyChanged_UI(nameof(VM));

			//LayoutRoot.DataContext = VM;
			MBView.DataContext = VM.MetaBriefcase;
		}
		protected override Task CloseMayOverrideAsync()
		{
			_vm?.Dispose();
			VM = null;

			return Task.CompletedTask;
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
		#endregion user actions
	}
}
