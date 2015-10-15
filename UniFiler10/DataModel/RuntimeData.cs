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
                    RaisePropertyChanged_UI(nameof(IsConnectionAvailable));
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
                    if (_persistentData != null && _persistentData.IsOpen)
                    {
                        if (
                            _persistentData.IsAllowMeteredConnection
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

        private static Briefcase _persistentData = null;
        private RuntimeData(Briefcase briefcase)
        {
            _persistentData = briefcase;
            Task act = ActivateAsync();
        }
        private async Task ActivateAsync()
        {
            await _persistentData.RunFunctionWhileOpenAsyncA(delegate 
            {
                AddHandlers();
                if (_persistentData.IsOpen) UpdateIsConnectionAvailable();
            });
        }
        private bool _isDisposed = false;
        public void Dispose()
        {
            _isDisposed = true;
            RemoveHandlers();
            ClearListeners();
        }
        #endregion construct and dispose

        #region event handlers
        private bool _isHandlersActive = false;
        private void AddHandlers()
        {
            if (!_isHandlersActive && _persistentData != null)
            {
                NetworkInformation.NetworkStatusChanged += OnNetworkStatusChanged;
                _persistentData.PropertyChanged += OnPersistentData_PropertyChanged;
                _isHandlersActive = true;
            }
        }
        private void RemoveHandlers()
        {
            if (_persistentData != null)
            {
                NetworkInformation.NetworkStatusChanged -= OnNetworkStatusChanged;
                _persistentData.PropertyChanged -= OnPersistentData_PropertyChanged;
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
                || (e.PropertyName==nameof(Briefcase.IsOpen) && _persistentData.IsOpen))
                UpdateIsConnectionAvailable();
        }
        #endregion event handlers
    }
}
