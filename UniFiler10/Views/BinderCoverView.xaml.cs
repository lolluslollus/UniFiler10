using Microsoft.CSharp.RuntimeBinder;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using UniFiler10.Controlz;
using UniFiler10.ViewModels;
using Utilz;
using Windows.Devices.Sensors;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Foundation.Metadata;
using Windows.Graphics.Display;
using Windows.Phone.UI.Input;
using Windows.UI.Core;
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
    public sealed partial class BinderCoverView : OpenableObservableControl
    {
        #region properties
        private BinderCoverVM _vm = null;
        public BinderCoverVM VM { get { return _vm; } set { _vm = value; RaisePropertyChanged_UI(); } }
        #endregion properties

        #region construct, dispose, open, close
        public BinderCoverView()
        {
            OpenCloseWhenLoadedUnloaded = false;
            InitializeComponent();
        }

        protected override async Task<bool> OpenMayOverrideAsync()
        {
            if (DataContext is Data.Model.Binder)
            {
                _vm = new BinderCoverVM(DataContext as Data.Model.Binder);
                await _vm.OpenAsync().ConfigureAwait(false);
                RaisePropertyChanged_UI(nameof(VM));

                RegisterEventHandlers();
                return true;
            }
            else
            {
                return false;
            }
        }
        protected override async Task CloseMayOverrideAsync()
        {
            UnregisterEventHandlers();
            await _vm?.CloseAsync();
            _vm?.Dispose();
            VM = null;
        }

        private static SemaphoreSlimSafeRelease _vmSemaphore = new SemaphoreSlimSafeRelease(1, 1);
        private async void OnDataContextChanged(FrameworkElement sender, DataContextChangedEventArgs args)
        {
            try
            {
                await _vmSemaphore.WaitAsync().ConfigureAwait(false);
                if (args != null)
                {
                    // LOLLO TODO so far, we don't need BinderCoverVM at all!
                    var newBinder = args.NewValue as Data.Model.Binder;
                    if (newBinder == null)
                    {
                        await CloseAsync().ConfigureAwait(false);
                    }
                    else if (_vm == null)
                    {
                        await CloseAsync().ConfigureAwait(false);
                        await TryOpenAsync().ConfigureAwait(false);
                    }
                    else if (_vm.Binder != newBinder)
                    {
                        await CloseAsync().ConfigureAwait(false);
                        await TryOpenAsync().ConfigureAwait(false);
                    }
                }
            }
            finally
            {
                SemaphoreSlimSafeRelease.TryRelease(_vmSemaphore);
            }
        }

        /// <summary>
        /// Registers event handlers for hardware buttons and orientation sensors, and performs an initial update of the UI rotation
        /// </summary>
        private void RegisterEventHandlers()
        {
            if (ApiInformation.IsTypePresent("Windows.Phone.UI.Input.HardwareButtons"))
            {
                HardwareButtons.BackPressed += OnHardwareButtons_BackPressed;
            }
            SystemNavigationManager.GetForCurrentView().BackRequested += OnTabletSoftwareButton_BackPressed;
        }

        /// <summary>
        /// Unregisters event handlers for hardware buttons and orientation sensors
        /// </summary>
        private void UnregisterEventHandlers()
        {
            if (ApiInformation.IsTypePresent("Windows.Phone.UI.Input.HardwareButtons"))
            {
                HardwareButtons.BackPressed -= OnHardwareButtons_BackPressed;
            }
            SystemNavigationManager.GetForCurrentView().BackRequested -= OnTabletSoftwareButton_BackPressed;
        }
        #endregion construct, dispose, open, close

        #region event handlers
        private async void OnBackButton_Tapped(object sender, TappedRoutedEventArgs e)
        {
            await RunFunctionWhileOpenAsyncA(CloseMe).ConfigureAwait(false);
        }
        private async void OnHardwareButtons_BackPressed(object sender, BackPressedEventArgs e)
        {
            await RunFunctionWhileOpenAsyncA(CloseMe).ConfigureAwait(false);
        }
        private async void OnTabletSoftwareButton_BackPressed(object sender, Windows.UI.Core.BackRequestedEventArgs e)
        {
            await RunFunctionWhileOpenAsyncA(CloseMe).ConfigureAwait(false);
        }

        private void CloseMe()
        {
            Task close = _vm?.CloseCoverAsync();
        }

        private void OnAll_Tapped(object sender, TappedRoutedEventArgs e)
        {
            Task all = _vm?.ReadAllFoldersAsync();
        }


        private void OnAllFolderPreviews_ItemClick(object sender, ItemClickEventArgs e)
        {
            Task all = _vm?.SelectFolderAsync((e?.ClickedItem as BinderCoverVM.FolderPreview)?.FolderId);
        }
        #endregion event handlers

        private void OnFolderPreview_Tapped(object sender, TappedRoutedEventArgs e)
        {
            e.Handled = true;
        }
    }
}
