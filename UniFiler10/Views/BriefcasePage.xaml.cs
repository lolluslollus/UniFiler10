using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
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

        #region construct dispose open close
        public BriefcasePage()
        {
            Application.Current.Resuming += OnResuming;
            Application.Current.Suspending += OnSuspending;
            Loading += OnLoading;
            InitializeComponent();
        }

        private async void OnSuspending(object sender, Windows.ApplicationModel.SuspendingEventArgs e)
        {
            var deferral = e.SuspendingOperation.GetDeferral();

            await _vm?.CloseAsync();
            _vm.Dispose();
            VM = null;

            deferral.Complete();
        }

        private async void OnResuming(object sender, object e)
        {
            await ActivateAsync().ConfigureAwait(false);
        }
        //protected override async void OnNavigatedTo(NavigationEventArgs e)
        //{
        //    await ActivateAsync();
        //    base.OnNavigatedTo(e);
        //}
        private async void OnLoading(FrameworkElement sender, object args) // fires after OnNavigatedTo and before OnLoaded
        {
            await ActivateAsync().ConfigureAwait(false);
        }

        // LOLLO OnUnloaded and OnNavigatingFrom do not fire

        private async Task ActivateAsync()
        {
            if (_vm == null) _vm = new BriefcaseVM();
            await _vm.OpenAsync().ConfigureAwait(true);
            RaisePropertyChanged(nameof(VM));
            LayoutRoot.DataContext = VM;
            // LOLLO do not set the datacontext of the whole control or it will alter the dependency properties, if any. 
            // Instead, set LayoutRoot.DataContext, where LayoutRoot is the main child of the Page or UserControl.
            // For example:
            // LayoutRoot.DataContext = VM;
        }
        #endregion construct dispose open close

        private async void OnAddDbName_Click(object sender, RoutedEventArgs e)
        {
            if (VM != null && VM.Briefcase != null) VM.Briefcase.IsShowingSettings = false;
            NewDbNameTB.Visibility = Visibility.Visible;
            await UpdateAddDbFieldsAsync().ConfigureAwait(false);
        }
        private async Task UpdateAddDbFieldsAsync()
        {
            if (!string.IsNullOrWhiteSpace(NewDbNameTB.Text))
            {
                if (_vm != null)
                {
                    if (await _vm.CheckDbNameAsync(NewDbNameTB.Text))
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
        private async void OnNewDbNameTB_TextChanged(object sender, TextChangedEventArgs e)
        {
            await UpdateAddDbFieldsAsync().ConfigureAwait(false);
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

        private async void OnDbItemClicked(object sender, SelectionChangedEventArgs e)
        {
            if (_vm != null && e?.AddedItems?.Count > 0)
            {
                await _vm.OpenBinderAsync(((sender as ListView).SelectedItem.ToString())).ConfigureAwait(false);
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
            var fe = sender as FrameworkElement;
            if (VM != null && fe != null)
            {
                //raise confirmation popup
                var rl = new ResourceLoader(); // localisation globalisation localization globalization
                string strQuestion = rl.GetString("DeleteBinderConfirmationRequest");
                string strYes = rl.GetString("Yes");
                string strNo = rl.GetString("No");

                var dialog = new MessageDialog(strQuestion);
                UICommand yesCommand = new UICommand(strYes, (command) => { });
                UICommand noCommand = new UICommand(strNo, (command) => { });
                dialog.Commands.Add(yesCommand);
                dialog.Commands.Add(noCommand);
                dialog.DefaultCommandIndex = 1; // Set the command that will be invoked by default
                IUICommand reply = await dialog.ShowAsync().AsTask(); // Show the message dialog
                // proceed
                if (reply == yesCommand)
                {
                    await VM.DeleteDbAsync(fe.DataContext as string).ConfigureAwait(false);
                }
            }
        }
    }
}
