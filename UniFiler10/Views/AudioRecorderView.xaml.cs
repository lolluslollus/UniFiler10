using System.Threading.Tasks;
using UniFiler10.Controlz;
using UniFiler10.Services;
using UniFiler10.ViewModels;
using Utilz;
using Windows.ApplicationModel;
using Windows.Foundation.Metadata;
using Windows.Phone.UI.Input;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;

// The User Control item template is documented at http://go.microsoft.com/fwlink/?LinkId=234236

namespace UniFiler10.Views
{
    /// <summary>
    /// This control is supposed to run inside a Popup.
    /// Showing or hiding the popup will open or close the control.
    /// </summary>
    public sealed partial class AudioRecorderView : OpenableObservableControl, IMessageWriter
    {
        public BinderVM VM
        {
            get { return (BinderVM)GetValue(VMProperty); }
            set { SetValue(VMProperty, value); }
        }
        public static readonly DependencyProperty VMProperty =
            DependencyProperty.Register("VM", typeof(BinderVM), typeof(AudioRecorderView), new PropertyMetadata(null, OnVMChanged));
        /// <summary>
        /// LOLLO VM may not be available yet when OnLoaded fires, it is required though, hence the complexity
        /// </summary>
        /// <param name="obj"></param>
        /// <param name="args"></param>
        private static async void OnVMChanged(DependencyObject obj, DependencyPropertyChangedEventArgs args)
        {
            var instance = obj as AudioRecorderView;
            if (instance != null && instance._isLoaded && args.NewValue is BinderVM && args.NewValue != args.OldValue)
            {
                await instance.CloseAsync().ConfigureAwait(false);
                await instance.TryOpenAsync().ConfigureAwait(false);
            }
        }


        private AudioRecorder _audioRecorder = null;

        private string _lastMessage = string.Empty;
        public string LastMessage { get { return _lastMessage; } set { _lastMessage = value; RaisePropertyChanged_UI(); } }

        // Prevent the screen from sleeping while the camera is running
        //private readonly DisplayRequest _displayRequest = new DisplayRequest();

        // For listening to media property changes
        //private readonly SystemMediaTransportControls _systemMediaControls = SystemMediaTransportControls.GetForCurrentView();

        public AudioRecorderView()
        {
            OpenCloseWhenLoadedUnloaded = true;
            IsEnabled = false;
            InitializeComponent();
        }

        protected override async Task<bool> OpenMayOverrideAsync()
        {
            if (VM != null)
            {
                _audioRecorder = new AudioRecorder(this, VM.Media);
                await _audioRecorder.OpenAsync();
                RegisterEventHandlers();

                await _audioRecorder.RecordStartAsync().ConfigureAwait(false);
                return true;
            }
            return false;
        }
        protected override async Task CloseMayOverrideAsync()
        {
            UnregisterEventHandlers();

            await StopRecordingAsync().ConfigureAwait(false);

            _audioRecorder?.Dispose();
            _audioRecorder = null;
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
            await RunFunctionWhileOpenAsyncA(CloseMe).ConfigureAwait(false);
        }
        private async void OnHardwareButtons_BackPressed(object sender, BackPressedEventArgs e)
        {
            await RunFunctionWhileOpenAsyncA(CloseMe).ConfigureAwait(false);
        }
        private async void OnTabletSoftwareButton_BackPressed(object sender, BackRequestedEventArgs e)
        {
            await RunFunctionWhileOpenAsyncA(CloseMe).ConfigureAwait(false);
        }

        private async Task StopRecordingAsync()
        {
            if (_audioRecorder != null) await _audioRecorder.RecordStopAsync();
            VM?.Media?.EndRecordAudio();
        }
        private void CloseMe()
        {
            if (VM != null) VM.Media.IsAudioRecorderOverlayOpen = false;
        }
    }
}
