using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using UniFiler10.Data.Model;
using Utilz;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.UI.ViewManagement;

namespace UniFiler10.ViewModels
{
    public class BriefcaseVM : ObservableData
    {
        private volatile bool _isLoaded = false;
        public bool IsLoaded { get { return _isLoaded; } private set { if (_isLoaded != value) { _isLoaded = value; RaisePropertyChanged_UI(); } } }

        private Briefcase _briefcase = null;
        public Briefcase Briefcase { get { return _briefcase; } private set { _briefcase = value; RaisePropertyChanged_UI(); } }


        public BriefcaseVM() { }
        public async Task ActivateAsync()
        {
            if (!_isLoaded)
            {
                if (_briefcase == null || !_briefcase.IsOpen)
                {
                    Briefcase = Briefcase.CreateInstance();
                    await _briefcase.OpenAsync().ConfigureAwait(false);
                    IsLoaded = true;
                }
            }
        }

        public async Task<bool> AddDbAsync(string dbName)
        {
            if (!_isLoaded || _briefcase == null) return false;

            return await _briefcase.AddBinderAsync(dbName).ConfigureAwait(false);
        }
        public async Task<bool> DeleteDbAsync(string dbName)
        {
            if (!_isLoaded || _briefcase == null) return false;

            return await _briefcase.DeleteBinderAsync(dbName).ConfigureAwait(false);
        }
        public async Task<bool> RestoreDbAsync()
        {
            if (!_isLoaded || _briefcase == null) return false;

            //bool unsnapped = ((ApplicationView.Value != ApplicationViewState.Snapped) || ApplicationView.TryUnsnap());
            //if (unsnapped)
            //{

                FolderPicker openPicker = new FolderPicker();
                openPicker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
                //openPicker.CommitButtonText=
                //openPicker.ViewMode = PickerViewMode.List;
                openPicker.FileTypeFilter.Add(".db");
                openPicker.FileTypeFilter.Add(".xml");
                var fromStorageFolder = await openPicker.PickSingleFolderAsync();

                return await _briefcase.RestoreBinderAsync(fromStorageFolder).ConfigureAwait(false);
            //}
            //return false;
        }
        public async Task<bool> BackupDbAsync(string dbName)
        {
            if (!_isLoaded || _briefcase == null || string.IsNullOrWhiteSpace(dbName) || !_briefcase.DbNames.Contains(dbName)) return false;

            FolderPicker savePicker = new FolderPicker();
            savePicker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
            //savePicker.CommitButtonText=
            //savePicker.ViewMode = PickerViewMode.List;
            savePicker.FileTypeFilter.Add(".db");
            savePicker.FileTypeFilter.Add(".xml");

            var toParentStorageFolder = await savePicker.PickSingleFolderAsync();

            return await _briefcase.BackupBinderAsync(dbName, toParentStorageFolder).ConfigureAwait(false);
        }

        public bool CheckDbName(string newDbName)
        {
            if (_briefcase != null && _briefcase.IsOpen && !string.IsNullOrWhiteSpace(newDbName))
                return !_briefcase.DbNames.Contains(newDbName);
            else
                return false;
        }
        public bool OpenBinder(string dbName)
        {
            if (!_isLoaded || _briefcase == null) return false;

            _briefcase.CurrentBinderName = dbName;
            return true;
        }
    }
}
