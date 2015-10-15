using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using UniFiler10.Data.Metadata;
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
    public sealed partial class SettingsView : UserControl, INotifyPropertyChanged
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
        private SettingsVM _vm = null;
        public SettingsVM VM { get { return _vm; } set { _vm = value; RaisePropertyChanged(); } }
        #endregion properties

        #region construct and dispose
        public SettingsView()
        {
            InitializeComponent();
        }
        #endregion construct and dispose

        #region open and close
        private async void OnDataContextChanged(FrameworkElement sender, DataContextChangedEventArgs args)
        {
            if (args?.NewValue is MetaBriefcase)
                await ActivateAsync(args.NewValue as MetaBriefcase).ConfigureAwait(false);
        }

        private async Task ActivateAsync(MetaBriefcase metaBriefcase)
        {
            await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, delegate
            {
                if (_vm == null)
                {
                    VM = new SettingsVM(metaBriefcase);
                }
            }).AsTask().ConfigureAwait(false);
        }
        #endregion open and close
    }
}
