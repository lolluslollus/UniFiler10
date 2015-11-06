using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Networking.Connectivity;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using Utilz;
using Windows.ApplicationModel.Core;
using Windows.Foundation;
using Windows.UI.Core;
using Windows.Devices.Enumeration;

namespace UniFiler10.Data.Model
{
    public sealed class RuntimeData : ObservableData, IDisposable
    {
        #region properties
        private volatile bool _isConnectionAvailable = false;
        public bool IsConnectionAvailable
        {
            get { return _isConnectionAvailable; }
            private set
            {
                if (_isConnectionAvailable != value)
                {
                    _isConnectionAvailable = value;
                    RaisePropertyChanged_UI();
                }
            }
        }
        private void UpdateIsConnectionAvailable()
        {
            var profile = NetworkInformation.GetInternetConnectionProfile();
            if (profile == null)
            {
                IsConnectionAvailable = false;
            }
            else
            {
                var level = profile.GetNetworkConnectivityLevel();
                if (level == NetworkConnectivityLevel.InternetAccess || level == NetworkConnectivityLevel.LocalAccess)
                {
                    if (_briefcase != null && _briefcase.IsOpen)
                    {
                        if (
                            _briefcase.IsAllowMeteredConnection
                            ||
                            NetworkInformation.GetInternetConnectionProfile()?.GetConnectionCost()?.NetworkCostType == NetworkCostType.Unrestricted
                            )
                        {
                            IsConnectionAvailable = true;
                        }
                    }
                    else
                    {
                        IsConnectionAvailable = false;
                    }
                }
                else
                {
                    IsConnectionAvailable = false;
                }
            }
        }

        private bool _isCameraAvailable = false;
        public bool IsCameraAvailable
        {
            get { return _isCameraAvailable; }
            private set
            {
                if (_isCameraAvailable != value)
                {
                    _isCameraAvailable = value;
                    RaisePropertyChanged_UI();
                }
            }
        }

        private async Task UpdateIsCameraAvailableAsync()
        {
            _videoDevice = await FindCameraDeviceByPanelAsync(Panel.Back).ConfigureAwait(false);
            IsCameraAvailable = _videoDevice?.IsEnabled == true;
        }

        private DeviceInformation _videoDevice = null;
        public DeviceInformation VideoDevice { get { return _videoDevice; } }

        private bool _isMicrophoneAvailable = false;
        public bool IsMicrophoneAvailable
        {
            get { return _isMicrophoneAvailable; }
            private set
            {
                if (_isMicrophoneAvailable != value)
                {
                    _isMicrophoneAvailable = value;
                    RaisePropertyChanged_UI();
                }
            }
        }

        private async Task UpdateIsMicrophoneAvailableAsync()
        {
            _audioDevice = await FindMicrophoneDeviceByPanelAsync(Panel.Back).ConfigureAwait(false);
            IsMicrophoneAvailable = _audioDevice?.IsEnabled == true;
        }

        private DeviceInformation _audioDevice = null;
        public DeviceInformation AudioDevice { get { return _audioDevice; } }
        #endregion properties

        #region construct and dispose
        private static RuntimeData _instance = null;
        public static RuntimeData Instance { get { return _instance; } }

        private static readonly object _instanceLock = new object();
        public static RuntimeData CreateInstance(Briefcase briefcase)
        {
            lock (_instanceLock)
            {
                if (_instance == null || _instance._isDisposed)
                {
                    _instance = new RuntimeData(briefcase);
                }
                return _instance;
            }
        }

        private static Briefcase _briefcase = null;
        private static DeviceWatcher _videoDeviceWatcher = null;
        private static DeviceWatcher _audioDeviceWatcher = null;

        private RuntimeData(Briefcase briefcase)
        {
            _briefcase = briefcase;
            _videoDeviceWatcher = DeviceInformation.CreateWatcher(DeviceClass.VideoCapture);
            _audioDeviceWatcher = DeviceInformation.CreateWatcher(DeviceClass.AudioCapture);
            Task act = ActivateAsync();
        }
        private async Task ActivateAsync()
        {
            AddHandlers();
            UpdateIsConnectionAvailable();

            // _videoDeviceWatcher.Start(); // LOLLO TODO check this
            await UpdateIsCameraAvailableAsync().ConfigureAwait(false);
            await UpdateIsMicrophoneAvailableAsync().ConfigureAwait(false);
        }
        private bool _isDisposed = false;
        public void Dispose()
        {
            _isDisposed = true;
            RemoveHandlers();
            // _videoDeviceWatcher.Stop(); // LOLLO TODO check this
            ClearListeners();
        }
        #endregion construct and dispose

        #region event handlers
        private bool _isHandlersActive = false;
        private void AddHandlers()
        {
            if (!_isHandlersActive && _briefcase != null)
            {
                NetworkInformation.NetworkStatusChanged += OnNetworkStatusChanged;
                _briefcase.PropertyChanged += OnPersistentData_PropertyChanged;
                _videoDeviceWatcher.EnumerationCompleted += OnVideoDeviceWatcher_EnumerationCompleted;
                _audioDeviceWatcher.EnumerationCompleted += OnAudioDeviceWatcher_EnumerationCompleted;
                _isHandlersActive = true;
            }
        }

        private void RemoveHandlers()
        {
            if (_briefcase != null)
            {
                NetworkInformation.NetworkStatusChanged -= OnNetworkStatusChanged;
                _briefcase.PropertyChanged -= OnPersistentData_PropertyChanged;
                _videoDeviceWatcher.EnumerationCompleted -= OnVideoDeviceWatcher_EnumerationCompleted;
                _audioDeviceWatcher.EnumerationCompleted -= OnAudioDeviceWatcher_EnumerationCompleted;
                _isHandlersActive = false;
            }
        }

        private void OnNetworkStatusChanged(object sender)
        {
            UpdateIsConnectionAvailable();
        }
        private void OnPersistentData_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(Briefcase.IsAllowMeteredConnection)
                || (e.PropertyName == nameof(Briefcase.IsOpen) && _briefcase.IsOpen))
                UpdateIsConnectionAvailable();
        }

        private async void OnVideoDeviceWatcher_EnumerationCompleted(DeviceWatcher sender, object args)
        {
            await UpdateIsCameraAvailableAsync().ConfigureAwait(false);
        }
        private async void OnAudioDeviceWatcher_EnumerationCompleted(DeviceWatcher sender, object args)
        {
            await UpdateIsMicrophoneAvailableAsync().ConfigureAwait(false);
        }

        #endregion event handlers

        #region helpers
        private static async Task<DeviceInformation> FindCameraDeviceByPanelAsync(Panel desiredPanel)
        {
            // Get available devices for capturing pictures
            var allVideoDevices = await DeviceInformation.FindAllAsync(DeviceClass.VideoCapture);

            // Get the desired camera by panel
            DeviceInformation desiredDevice = allVideoDevices.FirstOrDefault(x => x.EnclosureLocation != null && x.EnclosureLocation.Panel == desiredPanel);

            // If there is no device mounted on the desired panel, return the first device found
            return desiredDevice ?? allVideoDevices.FirstOrDefault();
        }
        private static async Task<DeviceInformation> FindMicrophoneDeviceByPanelAsync(Panel desiredPanel)
        {
            // Get available devices for capturing pictures
            var allAudioDevices = await DeviceInformation.FindAllAsync(DeviceClass.AudioCapture);

            // Get the desired camera by panel
            DeviceInformation desiredDevice = allAudioDevices.FirstOrDefault(x => x.EnclosureLocation != null && x.EnclosureLocation.Panel == desiredPanel);

            // If there is no device mounted on the desired panel, return the first device found
            return desiredDevice ?? allAudioDevices.FirstOrDefault();
        }
        #endregion helpers
    }
}
