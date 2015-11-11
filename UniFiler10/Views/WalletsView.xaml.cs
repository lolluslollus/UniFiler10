using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using UniFiler10.Data.Model;
using UniFiler10.ViewModels;
using Utilz;
using Windows.Foundation;
using Windows.Foundation.Collections;
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
    public sealed partial class WalletsView : UserControl
    {
        public BinderVM VM
        {
            get { return (BinderVM)GetValue(VMProperty); }
            set { SetValue(VMProperty, value); }
        }
        public static readonly DependencyProperty VMProperty =
            DependencyProperty.Register("VM", typeof(BinderVM), typeof(WalletsView), new PropertyMetadata(null));

        public WalletsView()
        {
            InitializeComponent();
        }

        private async void OnAdd_Click(object sender, RoutedEventArgs e)
        {
            if (VM != null && DataContext is Folder)
                await VM.AddWalletToFolderAsync(DataContext as Folder).ConfigureAwait(false);
        }

        //private async void OnItemDelete_Click(object sender, RoutedEventArgs e)
        //{
        //    if (VM != null
        //        && DataContext is Folder
        //        && sender is FrameworkElement && (sender as FrameworkElement).DataContext is Wallet)
        //        await VM.RemoveWalletFromFolderAsync(DataContext as Folder, (sender as FrameworkElement).DataContext as Wallet).ConfigureAwait(false);
        //}

        private async void OnShoot_Click(object sender, RoutedEventArgs e)
        {
            if (VM != null && DataContext is Folder)
                await VM.Media.ShootAsync(DataContext as Folder).ConfigureAwait(false);
        }

        private async void OnOpenFile_Click(object sender, RoutedEventArgs e)
        {
            if (VM != null && DataContext is Folder)
                await VM.Media.LoadMediaFileAsync(DataContext as Folder).ConfigureAwait(false);
        }

        private async void OnRecordSound_Click(object sender, RoutedEventArgs e)
        {
            if (VM != null && DataContext is Folder)
                await VM.Media.RecordAudioAsync(DataContext as Folder).ConfigureAwait(false);
        }

        //private void OnStopRecordingSound_Click(object sender, RoutedEventArgs e)
        //{
        //    if (VM != null)
        //        VM.EndRecordSound();
        //}
    }
}
