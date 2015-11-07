using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using UniFiler10.Services;
using UniFiler10.ViewModels;
using Windows.ApplicationModel;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Foundation.Metadata;
using Windows.Media;
using Windows.Phone.UI.Input;
using Windows.Storage;
using Windows.System.Display;
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
    public sealed partial class AudioRecorderView : UserControl
    {
        public BinderVM VM
        {
            get { return (BinderVM)GetValue(VMProperty); }
            set { SetValue(VMProperty, value); }
        }
        public static readonly DependencyProperty VMProperty =
            DependencyProperty.Register("VM", typeof(BinderVM), typeof(AudioRecorderView), new PropertyMetadata(null, OnVMChanged));
        private static async void OnVMChanged(DependencyObject obj, DependencyPropertyChangedEventArgs args)
        {
            var instance = obj as AudioRecorderView;
            if (instance._isLoaded && args.NewValue is BinderVM && args.NewValue != args.OldValue)
            {
                await instance.OpenAsync().ConfigureAwait(false);
            }
        }
        private AudioRecorder _audioRecorder = null;

        // Prevent the screen from sleeping while the camera is running
        //private readonly DisplayRequest _displayRequest = new DisplayRequest();

        // For listening to media property changes
        //private readonly SystemMediaTransportControls _systemMediaControls = SystemMediaTransportControls.GetForCurrentView();

        public AudioRecorderView()
        {
            InitializeComponent();
            //Application.Current.Suspending += OnApplication_Suspending; // LOLLO TODO see if we need these event handlers
            //Application.Current.Resuming += OnApplication_Resuming;
        }

        //private void OnApplication_Suspending(object sender, SuspendingEventArgs e)
        //{
        //    // Handle global application events only if this page is active
        //    //if (Frame.CurrentSourcePageType == typeof(CameraPage))
        //    //{
        //    var deferral = e.SuspendingOperation.GetDeferral();

        //    UnregisterEventHandlers();

        //    deferral.Complete();
        //    //}
        //}

        //private void OnApplication_Resuming(object sender, object o)
        //{
        //    // Handle global application events only if this page is active
        //    //if (Frame.CurrentSourcePageType == typeof(CameraPage))
        //    //{
        //    RegisterEventHandlers();
        //    //}
        //}
        //private async void OnSizeChanged(object sender, SizeChangedEventArgs e)
        //{
        //    if (VM?.AudioRecorderFile != null)
        //    {
        //        _audioRecorder = new AudioRecorder();
        //        await _audioRecorder.OpenAsync(VM.AudioRecorderFile);
        //        RegisterEventHandlers();

        //        await _audioRecorder.RecordStartAsync().ConfigureAwait(false);
        //    }
        //}
        private bool _isLoaded = false;
        private async void OnLoaded(object sender, RoutedEventArgs e) // LOLLO VM is not available yet when this fires
        {
            _isLoaded = true;
            await OpenAsync().ConfigureAwait(false);
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            _isLoaded = false;
            UnregisterEventHandlers();

            _audioRecorder?.Dispose();
            _audioRecorder = null;
        }
        private async Task OpenAsync()
        {
            if (VM?.AudioRecorderFile != null)
            {
                _audioRecorder = new AudioRecorder();
                await _audioRecorder.OpenAsync(VM.AudioRecorderFile);
                RegisterEventHandlers();

                await _audioRecorder.RecordStartAsync().ConfigureAwait(false);
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

            //_systemMediaControls.PropertyChanged += OnSystemMediaControls_PropertyChanged;
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

            //_systemMediaControls.PropertyChanged -= OnSystemMediaControls_PropertyChanged;
        }

        private async void OnBackButton_Tapped(object sender, TappedRoutedEventArgs e)
        {
            await StopRecordingAsync().ConfigureAwait(false);
        }
        private async void OnHardwareButtons_BackPressed(object sender, BackPressedEventArgs e)
        {
            await StopRecordingAsync().ConfigureAwait(false);
        }
        private async void OnTabletSoftwareButton_BackPressed(object sender, Windows.UI.Core.BackRequestedEventArgs e)
        {
            await StopRecordingAsync().ConfigureAwait(false);
        }

        private async Task StopRecordingAsync()
        {
            await _audioRecorder.RecordStopAsync();
            if (VM != null)
            {
                VM.EndAudioRecorder();
                VM.IsAudioRecorderOverlayOpen = false;
            }
        }


    }
}
