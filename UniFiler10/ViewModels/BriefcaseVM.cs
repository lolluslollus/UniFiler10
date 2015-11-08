using System;
using System.Threading.Tasks;
using UniFiler10.Data.Model;
using Windows.Storage;
using Windows.Storage.Pickers;

namespace UniFiler10.ViewModels
{
    public class BriefcaseVM : OpenableObservableData
    {
        private Briefcase _briefcase = null;
        public Briefcase Briefcase { get { return _briefcase; } private set { _briefcase = value; RaisePropertyChanged_UI(); } }


        public BriefcaseVM() { }
        protected override async Task OpenMayOverrideAsync()
        {
            if (_briefcase == null)
            {
                Briefcase = Briefcase.CreateInstance();
            }
            if (!_briefcase.IsOpen)
            {
                await _briefcase.OpenAsync().ConfigureAwait(false);
            }
        }
        protected override async Task CloseMayOverrideAsync()
        {
            if (_briefcase != null)
            {
                await _briefcase.CloseAsync().ConfigureAwait(false);
                _briefcase?.Dispose();
                _briefcase = null;
            }

        }
        public Task<bool> AddDbAsync(string dbName)
        {
            return RunFunctionWhileOpenAsyncTB(async delegate
            {
                return await _briefcase.AddBinderAsync(dbName).ConfigureAwait(false);
            });
        }
        public Task<bool> DeleteDbAsync(string dbName)
        {
            return RunFunctionWhileOpenAsyncTB(async delegate
            {
                return await _briefcase.DeleteBinderAsync(dbName).ConfigureAwait(false);
            });
        }
        public Task<bool> RestoreDbAsync()
        {
            return RunFunctionWhileOpenAsyncTB(async delegate
            {
                var fromStorageFolder = await PickFolderAsync();
                return await _briefcase.RestoreBinderAsync(fromStorageFolder).ConfigureAwait(false);
            });
        }

        public Task<bool> BackupDbAsync(string dbName)
        {
            return RunFunctionWhileOpenAsyncTB(async delegate
            {
                if (string.IsNullOrWhiteSpace(dbName) || !_briefcase.DbNames.Contains(dbName)) return false;

                var toParentStorageFolder = await PickFolderAsync();
                return await _briefcase.BackupBinderAsync(dbName, toParentStorageFolder).ConfigureAwait(false);
            });
        }

        public bool CheckDbName(string newDbName)
        {
            if (_briefcase != null && _briefcase.IsOpen && !string.IsNullOrWhiteSpace(newDbName))
                return !_briefcase.DbNames.Contains(newDbName);
            else
                return false;
        }
        public Task<bool> OpenBinderAsync(string dbName)
        {
            return RunFunctionWhileOpenAsyncB(delegate
            {
                _briefcase.CurrentBinderName = dbName;
                return true;
            });
        }
        private async Task<StorageFolder> PickFolderAsync()
        {
            //bool unsnapped = ((ApplicationView.Value != ApplicationViewState.Snapped) || ApplicationView.TryUnsnap());
            //if (unsnapped)
            //{

            FolderPicker openPicker = new FolderPicker();
            openPicker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
            //openPicker.CommitButtonText=
            //openPicker.ViewMode = PickerViewMode.List;
            openPicker.FileTypeFilter.Add(".db");
            openPicker.FileTypeFilter.Add(".xml");
            var folder = await openPicker.PickSingleFolderAsync();
            return folder;

            //}
            //return false;
        }
    }
}
