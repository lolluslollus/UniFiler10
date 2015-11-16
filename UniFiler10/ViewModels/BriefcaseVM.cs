using System;
using System.Threading.Tasks;
using UniFiler10.Data.Model;
using Windows.ApplicationModel.Resources;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.UI.Popups;

namespace UniFiler10.ViewModels
{
    public class BriefcaseVM : OpenableObservableData
    {
        private Briefcase _briefcase = null;
        public Briefcase Briefcase { get { return _briefcase; } private set { _briefcase = value; RaisePropertyChanged_UI(); } }

		private bool _isNewDbNameVisible = false;
		public bool IsNewDbNameVisible { get { return _isNewDbNameVisible; } set { _isNewDbNameVisible = value; RaisePropertyChanged_UI(); if (_isNewDbNameVisible) UpdateIsNewDbNameErrorMessageVisible(); } }

		private bool _isNewDbNameErrorMessageVisible = false;
		public bool IsNewDbNameErrorMessageVisible { get { return _isNewDbNameErrorMessageVisible; } set { _isNewDbNameErrorMessageVisible = value; RaisePropertyChanged_UI(); } }

		private string _newDbName = string.Empty;
		public string NewDbName { get { return _newDbName; } set { _newDbName = value; RaisePropertyChanged_UI(); UpdateIsNewDbNameErrorMessageVisible(); } }

		public BriefcaseVM() { }
        protected override async Task OpenMayOverrideAsync()
        {
            if (_briefcase == null) Briefcase = Briefcase.CreateInstance();
            await _briefcase.OpenAsync().ConfigureAwait(false);
        }
        protected override async Task CloseMayOverrideAsync()
        {
            await _briefcase?.CloseAsync();
            _briefcase?.Dispose();
            _briefcase = null;
        }
		public bool AddDbStep0()
		{
			if (_briefcase == null || !_briefcase.IsOpen) return false;

			_briefcase.IsShowingSettings = false;
			IsNewDbNameVisible = true;

			return true;
		}

		public async Task<bool> AddDbStep1()
		{
			bool isDbNameOk = _briefcase?.CheckNewDbName(_newDbName) == true;
			if (isDbNameOk)
			{
				isDbNameOk = await AddDbAsync(_newDbName);
			}
			if (isDbNameOk)
			{
				OpenBinder(_newDbName);
				_briefcase.SetIsCoverOpen(false);
			}

			return isDbNameOk;
		}
		public bool OpenBinder(string newDbName)
		{
			return _briefcase?.OpenBinder(newDbName) == true;
		}
		private void UpdateIsNewDbNameErrorMessageVisible()
		{
			bool isDbNameOk = _briefcase?.CheckNewDbName(_newDbName) == true;
			if (isDbNameOk)
			{
				IsNewDbNameErrorMessageVisible = false;
			}
			else
			{
				IsNewDbNameErrorMessageVisible = true;
			}

		}
		public Task<bool> AddDbAsync(string dbName)
        {
            return _briefcase?.AddBinderAsync(dbName);
        }
        public async Task<bool> DeleteDbAsync(string dbName)
        {
			if (_briefcase == null) return false;

			bool isDeleted = await GetUserConfirmationBeforeDeletingBinderAsync() && await _briefcase?.DeleteBinderAsync(dbName);

			return isDeleted;
        }
		private async Task<bool> GetUserConfirmationBeforeDeletingBinderAsync()
		{
			//raise confirmation popup
			var rl = new ResourceLoader(); // localisation globalisation localization globalization
			string strQuestion = rl.GetString("DeleteBinderConfirmationRequest");
			string strYes = rl.GetString("Yes");
			string strNo = rl.GetString("No");

			var dialog = new MessageDialog(strQuestion);
			UICommand yesCommand = new UICommand(strYes, (command) => { });
			UICommand noCommand = new UICommand(strNo, (command) => { });
			dialog.Commands.Add(yesCommand);
			dialog.Commands.Add(noCommand);
			dialog.DefaultCommandIndex = 1; // Set the command that will be invoked by default
			IUICommand reply = await dialog.ShowAsync().AsTask(); // Show the message dialog

			return reply == yesCommand;
		}

		public async Task<bool> RestoreDbAsync()
        {
            var fromStorageFolder = await PickFolderAsync();
            return await _briefcase?.RestoreBinderAsync(fromStorageFolder);
        }

        public async Task<bool> BackupDbAsync(string dbName)
        {
            if (string.IsNullOrWhiteSpace(dbName) || _briefcase == null || !_briefcase.DbNames.Contains(dbName)) return false;

            var toParentStorageFolder = await PickFolderAsync();
            return await _briefcase.BackupBinderAsync(dbName, toParentStorageFolder).ConfigureAwait(false);
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

		public void OpenCover()
		{
			_briefcase?.SetIsCoverOpen(true);
		}
		public void ShowSettings(bool newValue)
		{
			_briefcase.SetIsCoverOpen(false);
			_briefcase?.ShowSettings(newValue);
		}
	}
}
