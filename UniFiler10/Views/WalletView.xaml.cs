using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using UniFiler10.Controlz;
using UniFiler10.Data.Model;
using UniFiler10.ViewModels;
using Utilz;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

// The User Control item template is documented at http://go.microsoft.com/fwlink/?LinkId=234236

namespace UniFiler10.Views
{
    public sealed partial class WalletView : ObservableControl
    {
        public BinderVM VM
        {
            get { return (BinderVM)GetValue(VMProperty); }
            set { SetValue(VMProperty, value); }
        }
        public static readonly DependencyProperty VMProperty =
            DependencyProperty.Register("VM", typeof(BinderVM), typeof(WalletView), new PropertyMetadata(null));

        public Folder Folder
        {
            get { return (Folder)GetValue(FolderProperty); }
            set { SetValue(FolderProperty, value); }
        }
        public static readonly DependencyProperty FolderProperty =
            DependencyProperty.Register("Folder", typeof(Folder), typeof(WalletView), new PropertyMetadata(null));

        public WalletView()
        {
            InitializeComponent();
        }

        private async void OnAdd_Click(object sender, RoutedEventArgs e)
        {
            if (VM != null && DataContext is Wallet)
                await VM.AddEmptyDocumentToWalletAsync(DataContext as Wallet).ConfigureAwait(false);
        }

        private async void OnShoot_Click(object sender, RoutedEventArgs e)
        {
            if (VM != null && DataContext is Wallet)
                await VM.Media.ShootAsync(DataContext as Wallet).ConfigureAwait(false);
        }

        private async void OnOpenFile_Click(object sender, RoutedEventArgs e)
        {
            if (VM != null && DataContext is Wallet)
                await VM.Media.LoadMediaFileAsync(DataContext as Wallet).ConfigureAwait(false);
        }

        private async void OnItemDelete_Click(object sender, RoutedEventArgs e)
        {
            if (VM != null && DataContext is Wallet && Folder != null)
                await VM.RemoveWalletFromFolderAsync(Folder, DataContext as Wallet).ConfigureAwait(false);
        }

        //private async void OnItemDelete_Click(object sender, RoutedEventArgs e)
        //{
        //    if (VM != null
        //        && DataContext is Wallet
        //        && sender is FrameworkElement && (sender as FrameworkElement).DataContext is Document)
        //        await VM.RemoveDocumentFromWalletAsync(DataContext as Wallet, (sender as FrameworkElement).DataContext as Document).ConfigureAwait(false);
        //}
    }
}
