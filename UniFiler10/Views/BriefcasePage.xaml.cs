using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using UniFiler10.Controlz;
using UniFiler10.Data.Model;
using UniFiler10.ViewModels;
using Windows.ApplicationModel.Resources;
using Windows.UI.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
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
        public BriefcaseVM VM { get { return _vm; } set { _vm = value; RaisePropertyChanged(); } }
        #endregion properties

        #region construct dispose open close
        public BriefcasePage()
        {
            InitializeComponent();
        }

		protected override async Task OpenMayOverrideAsync()
		{
			if (_vm == null) _vm = new BriefcaseVM();
			await _vm.OpenAsync().ConfigureAwait(true);
			RaisePropertyChanged(nameof(VM));

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
		}
		#endregion construct dispose open close


		#region user actions
		private void OnBriefcaseCoverView_GoToBinderContentRequested(object sender, EventArgs e)
		{
			Frame.Navigate(typeof(BriefcaseContentPage));
		}

		private async void OnBriefcaseCoverView_GoToSettingsRequested(object sender, EventArgs e)
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