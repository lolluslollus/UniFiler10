using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using UniFiler10.Converters;
using UniFiler10.Data.Model;
using UniFiler10.ViewModels;
using Windows.Foundation;
using Windows.Foundation.Collections;
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
    public sealed partial class BriefcasePage : Page, INotifyPropertyChanged
    {
        #region INotifyPropertyChanged
        public event PropertyChangedEventHandler PropertyChanged;
        //private void ClearListeners() { PropertyChanged = null; }
        private void RaisePropertyChanged([CallerMemberName] String propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        #endregion INotifyPropertyChanged

        #region properties
        private BriefcaseVM _vm = null;
        public BriefcaseVM VM { get { return _vm; } set { _vm = value; RaisePropertyChanged(); } }
        #endregion properties

        #region construct and dispose
        public BriefcasePage()
        {
            InitializeComponent();
        }
        #endregion construct and dispose

        #region open and close
        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            if (e.NavigationMode == NavigationMode.New || e.NavigationMode == NavigationMode.Refresh)
            {
                await ActivateAsync().ConfigureAwait(false);
            }
        }

        // LOLLO OnUnloaded and OnNavigatingFrom do not fire

        private async Task ActivateAsync()
        {
            VM = new BriefcaseVM();
            await _vm.ActivateAsync().ConfigureAwait(true);
            // LOLLO do not set the datacontext of the whole control or it will alter the dependency properties, if any
            //LayoutRoot.DataContext = VM;
        }
        #endregion open and close

        private void OnAddDbName_Click(object sender, RoutedEventArgs e)
        {
            if (VM != null && VM.Briefcase != null) VM.Briefcase.IsShowingSettings = false;
            NewDbNameTB.Visibility = Visibility.Visible;
            UpdateAddDbFields();
        }
        private void UpdateAddDbFields()
        {
            if (!string.IsNullOrWhiteSpace(NewDbNameTB.Text))
            {
                if (_vm != null)
                {
                    if (_vm.CheckDbName(NewDbNameTB.Text))
                    {
                        AddDbButton.Visibility = Visibility.Visible;
                        NewDbNameErrorTB.Visibility = Visibility.Collapsed;
                    }
                    else
                    {
                        AddDbButton.Visibility = Visibility.Collapsed;
                        NewDbNameErrorTB.Visibility = Visibility.Visible;
                    }
                }
            }
            else
            {
                AddDbButton.Visibility = Visibility.Collapsed;
                NewDbNameErrorTB.Visibility = Visibility.Collapsed;
            }
        }
        private void OnNewDbNameTB_TextChanged(object sender, TextChangedEventArgs e)
        {
            UpdateAddDbFields();
        }
        private async void OnAddDb_Click(object sender, RoutedEventArgs e)
        {
            if (_vm != null)
            {
                NewDbNameTB.Visibility = Visibility.Collapsed;
                AddDbButton.Visibility = Visibility.Collapsed;
                await _vm.AddDbAsync(NewDbNameTB.Text).ConfigureAwait(false);
            }
        }

        private void OnTogglePaneOpen(object sender, RoutedEventArgs e)
        {
            if (_vm != null) VM.Briefcase.IsPaneOpen = !_vm.Briefcase.IsPaneOpen;
        }

        private void OnDbItemClicked(object sender, SelectionChangedEventArgs e)
        {
            if (_vm != null && e?.AddedItems?.Count > 0)
            {
                _vm.OpenBinder(((sender as ListView).SelectedItem.ToString()));
            }
        }

        private void OnDbNameClicked(object sender, ItemClickEventArgs e)
        {
            if (VM != null && VM.Briefcase != null) VM.Briefcase.IsShowingSettings = false;
        }

        private async void OnRestoreBinder_Click(object sender, RoutedEventArgs e)
        {
            if (VM != null) await VM.RestoreDbAsync().ConfigureAwait(false);
        }

        private async void OnBackupBinder_Click(object sender, RoutedEventArgs e)
        {
            var fe = sender as FrameworkElement;
            if (VM != null && fe != null) await VM.BackupDbAsync(fe.DataContext as string).ConfigureAwait(false);
        }

        private async void OnDeleteBinder_Click(object sender, RoutedEventArgs e)
        {
            // LOLLO TODO ask for confirmation before deleting
            var fe = sender as FrameworkElement;
            if (VM != null && fe != null) await VM.DeleteDbAsync(fe.DataContext as string).ConfigureAwait(false);
        }
    }
}
