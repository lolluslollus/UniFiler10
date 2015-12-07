﻿using System;
using System.Threading.Tasks;
using UniFiler10.Data.Constants;
using UniFiler10.Data.Model;
using UniFiler10.Data.Runtime;
using Utilz;
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
		public bool IsNewDbNameVisible { get { return _isNewDbNameVisible; } set { _isNewDbNameVisible = value; RaisePropertyChanged_UI(); if (_isNewDbNameVisible) { Task upd = UpdateIsNewDbNameErrorMessageVisibleAsync(); } } }

		private bool _isNewDbNameErrorMessageVisible = false;
		public bool IsNewDbNameErrorMessageVisible { get { return _isNewDbNameErrorMessageVisible; } set { _isNewDbNameErrorMessageVisible = value; RaisePropertyChanged_UI(); } }

		private string _newDbName = string.Empty;
		public string NewDbName { get { return _newDbName; } set { _newDbName = value; RaisePropertyChanged_UI(); Task upd = UpdateIsNewDbNameErrorMessageVisibleAsync(); } }

		protected override async Task OpenMayOverrideAsync()
		{
			if (_briefcase == null) _briefcase = Briefcase.GetCreateInstance();
			await _briefcase.OpenAsync().ConfigureAwait(false);
			RaisePropertyChanged_UI(nameof(Briefcase)); // notify UI once briefcase is open
		}
		protected override Task CloseMayOverrideAsync()
		{
			// briefcase and other data model classes cannot be destroyed by view models. Only app.xaml may do so.
			Briefcase = null;
			return Task.CompletedTask;
		}
		public bool AddDbStep0()
		{
			var bf = _briefcase;
			if (bf == null || !bf.IsOpen) return false;

			IsNewDbNameVisible = true;

			return true;
		}

		public async Task<bool> AddDbStep1Async()
		{
			var bf = _briefcase; if (bf == null) return false;

			if (await bf.AddBinderAsync(_newDbName).ConfigureAwait(false))
			{
				if (await bf.SetCurrentBinderNameAsync(_newDbName).ConfigureAwait(false))
				{
					IsNewDbNameVisible = false;
					return true;
				}
			}

			return false;
		}

		public async Task<bool> SetCurrentBinderAsync(string dbName)
		{
			var bf = _briefcase;
			if (bf != null)
			{
				return await bf.SetCurrentBinderNameAsync(dbName).ConfigureAwait(false);
			}
			return false;
		}
		public Task CloseBinderAsync()
		{
			return _briefcase?.CloseCurrentBinderAsync();
		}

		private async Task UpdateIsNewDbNameErrorMessageVisibleAsync()
		{
			var bf = _briefcase;
			if (bf != null)
			{
				bool isDbNameWrongAndBriefcaseIsOpen = await bf.IsNewDbNameWrongAsync(_newDbName).ConfigureAwait(false);
				if (isDbNameWrongAndBriefcaseIsOpen)
				{
					IsNewDbNameErrorMessageVisible = true;
				}
				else
				{
					IsNewDbNameErrorMessageVisible = false;
				}
			}
			else
			{
				IsNewDbNameErrorMessageVisible = false;
			}
		}

		public async Task<bool> DeleteDbAsync(string dbName)
		{
			var briefcase = _briefcase;
			if (briefcase == null) return false;

			bool isDeleted = await GetUserConfirmationBeforeDeletingBinderAsync() && await briefcase.DeleteBinderAsync(dbName);

			return isDeleted;
		}
		private async Task<bool> GetUserConfirmationBeforeDeletingBinderAsync()
		{
			//raise confirmation popup
			// var rl = new ResourceLoader(); // localisation globalisation localization globalization
			string strQuestion = RuntimeData.ResourceLoader.GetString("DeleteBinderConfirmationRequest");
			string strYes = RuntimeData.ResourceLoader.GetString("Yes");
			string strNo = RuntimeData.ResourceLoader.GetString("No");

			var dialog = new MessageDialog(strQuestion);
			UICommand yesCommand = new UICommand(strYes, (command) => { });
			UICommand noCommand = new UICommand(strNo, (command) => { });
			dialog.Commands.Add(yesCommand);
			dialog.Commands.Add(noCommand);
			dialog.DefaultCommandIndex = 1; // Set the command that will be invoked by default
			IUICommand reply = await dialog.ShowAsync().AsTask(); // Show the message dialog

			return reply == yesCommand;
		}

		private enum ImportBinderOperations { Cancel, Import, Merge }
		private async Task<ImportBinderOperations> GetUserConfirmationBeforeImportingBinderAsync()
		{
			//raise confirmation popup
			//var rl = new ResourceLoader(); // localisation globalisation localization globalization
			string strQuestion = RuntimeData.ResourceLoader.GetString("ImportBinderConfirmationRequest");
			string strMerge = RuntimeData.ResourceLoader.GetString("Merge");
			string strImport = RuntimeData.ResourceLoader.GetString("Import1");
			string strCancel = RuntimeData.ResourceLoader.GetString("Cancel");

			var dialog = new MessageDialog(strQuestion);
			UICommand mergeCommand = new UICommand(strMerge, (command) => { });
			UICommand importCommand = new UICommand(strImport, (command) => { });
			UICommand cancelCommand = new UICommand(strCancel, (command) => { });

			dialog.Commands.Add(mergeCommand);
			dialog.Commands.Add(importCommand);
			dialog.Commands.Add(cancelCommand);

			dialog.DefaultCommandIndex = 1; // Set the command that will be invoked by default
			IUICommand reply = await dialog.ShowAsync().AsTask(); // Show the message dialog

			if (reply == mergeCommand) return ImportBinderOperations.Merge;
			else if (reply == importCommand) return ImportBinderOperations.Import;
			else return ImportBinderOperations.Cancel;
		}
		public async Task<bool> ImportDbAsync()
		{
			var briefcase = _briefcase;
			if (briefcase != null)
			{
				var fromDirectory = await PickFolderAsync();
				if (fromDirectory != null)
				{
					if (await briefcase.IsDbNameAvailableAsync(fromDirectory.Name))
					{
						var nextAction = await GetUserConfirmationBeforeImportingBinderAsync().ConfigureAwait(false);
						if (nextAction == ImportBinderOperations.Merge)
						{
							return await briefcase.MergeBinderAsync(fromDirectory).ConfigureAwait(false);
						}
						else if (nextAction == ImportBinderOperations.Import)
						{
							return await briefcase.ImportBinderAsync(fromDirectory).ConfigureAwait(false);
						}
					}
					else
					{
						return await briefcase.ImportBinderAsync(fromDirectory).ConfigureAwait(false);
					}
				}
			}
			return false;
		}

		public async Task<bool> BackupDbAsync(string dbName)
		{
			var bc = _briefcase;
			if (string.IsNullOrWhiteSpace(dbName) || bc == null || !bc.DbNames.Contains(dbName)) return false;

			var toParentStorageFolder = await PickFolderAsync();
			return await bc.BackupBinderAsync(dbName, toParentStorageFolder).ConfigureAwait(false);
		}

		private Task<StorageFolder> PickFolderAsync()
		{
			return Pickers.PickFolderAsync(new string[] { ConstantData.DB_EXTENSION, ConstantData.XML_EXTENSION });
			////bool unsnapped = ((ApplicationView.Value != ApplicationViewState.Snapped) || ApplicationView.TryUnsnap());
			////if (unsnapped)
			////{

			//FolderPicker openPicker = new FolderPicker();
			//openPicker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
			////openPicker.CommitButtonText=
			////openPicker.ViewMode = PickerViewMode.List;
			//openPicker.FileTypeFilter.Add(".db");
			//openPicker.FileTypeFilter.Add(".xml");
			//var folder = await openPicker.PickSingleFolderAsync();
			//return folder;

			////}
			////return false;
		}
	}
}
