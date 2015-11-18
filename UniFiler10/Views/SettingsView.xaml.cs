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
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class SettingsView : BackableOpenableObservableControl
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
			DependencyProperty.Register("BriefcaseVM", typeof(BriefcaseVM), typeof(SettingsView), new PropertyMetadata(null));
		#endregion properties


		#region construct dispose open close
		public SettingsView()
        {
			OpenCloseWhenLoadedUnloaded = false;
            InitializeComponent();
			Task open = TryOpenAsync();
        }
		protected override async Task<bool> OpenMayOverrideAsync()
		{
			RegisterBackEventHandlers();
			UpdateVm(DataContext as MetaBriefcase);
			await Task.CompletedTask;
			return true;
		}
		private void UpdateVm(MetaBriefcase metaBriefcase)
        {
			RunInUiThread(delegate 
			{
				if (_vm == null || _vm.MetaBriefcase != metaBriefcase)
				{
					VM = new SettingsVM(metaBriefcase);
				}
			});
        }

		protected override void CloseMe()
		{
			BriefcaseVM?.ShowCover();
		}
		#endregion construct dispose open close
	}
}
