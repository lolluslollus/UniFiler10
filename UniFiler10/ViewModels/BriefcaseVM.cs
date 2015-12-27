using System;
using System.Threading.Tasks;
using UniFiler10.Controlz;
using UniFiler10.Data.Constants;
using UniFiler10.Data.Model;
using UniFiler10.Data.Runtime;
using Utilz;
using Windows.ApplicationModel.Resources;
using Windows.Storage;

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

		private AnimationStarter _animationStarter = null;

		public BriefcaseVM(AnimationStarter animationStarter)
		{
			if (animationStarter == null) throw new ArgumentNullException("BriefcaseVM ctor: animationStarter may not be null");
			_animationStarter = animationStarter;
		}

		protected override async Task OpenMayOverrideAsync()
		{
			if (_briefcase == null) _briefcase = Briefcase.GetCreateInstance();
			await _briefcase.OpenAsync().ConfigureAwait(false);
			RaisePropertyChanged_UI(nameof(Briefcase)); // notify UI once briefcase is open

			//await ResumeAfterExportBinderAsync().ConfigureAwait(false);
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

		public async Task<bool> TryOpenCurrentBinderAsync(string dbName)
		{
			var bf = _briefcase;
			if (bf != null)
			{
				if (await bf.SetCurrentBinderNameAsync(dbName).ConfigureAwait(false))
				{
					return true;
					// await bf.OpenCurrentBinderAsync().ConfigureAwait(false);
				}
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

			bool isDeleted = await UserConfirmationPopup.GetInstance().GetUserConfirmationBeforeDeletingBinderAsync() && await briefcase.DeleteBinderAsync(dbName);

			return isDeleted;
		}

		public enum ImportBinderOperations { Cancel, Import, Merge }

		public async Task StartImportDbAsync()
		{
			bool isOk = false;
			var briefcase = _briefcase;
			if (briefcase != null)
			{
				// LOLLO TODO make this work with phone with low memory
				var fromDirectory = await Pickers.PickFolderAsync(new string[] { ConstantData.DB_EXTENSION, ConstantData.XML_EXTENSION });
				if (fromDirectory != null)
				{
					if (await briefcase.IsDbNameAvailableAsync(fromDirectory.Name))
					{
						var nextAction = await UserConfirmationPopup.GetInstance().GetUserConfirmationBeforeImportingBinderAsync().ConfigureAwait(false);

						if (nextAction == ImportBinderOperations.Merge)
						{
							isOk = await briefcase.MergeBinderAsync(fromDirectory).ConfigureAwait(false);
						}
						else if (nextAction == ImportBinderOperations.Import)
						{
							isOk = await briefcase.ImportBinderAsync(fromDirectory).ConfigureAwait(false);
						}
					}
					else
					{
						isOk = await briefcase.ImportBinderAsync(fromDirectory).ConfigureAwait(false);
					}
				}
			}
			if (isOk) _animationStarter.StartAnimation(AnimationStarter.Animations.Success);
			else _animationStarter.StartAnimation(AnimationStarter.Animations.Failure);
		}

		public async Task StartBackupBinderAsync(string dbName)
		{
			// LOLLO TODO avoid copying all the files twice, which is time consuming
			bool isOk = false;
			var bc = _briefcase;
			StorageFolder tempDir = null;

			isOk = !string.IsNullOrWhiteSpace(dbName) && bc?.DbNames?.Contains(dbName) == true;
			if (isOk)
			{
				_animationStarter.StartAnimation(AnimationStarter.Animations.Updating);
				tempDir = await ApplicationData.Current.TemporaryFolder.CreateFolderAsync(ConstantData.BAK_BINDER_TEMP_DIR, CreationCollisionOption.ReplaceExisting).AsTask(); //.ConfigureAwait(false);
				isOk = await bc.ExportBinderAsync(dbName, tempDir) && tempDir != null; //.ConfigureAwait(false);
			}
			if (isOk)
			{
				// LOLLO NOTE for some stupid reason, the following wants a non-empty extension list
				var pickDirectoryTask = Pickers.PickFolderAsync(new string[] { ConstantData.XML_EXTENSION });
				var afterPickedDirectoryTask = pickDirectoryTask.ContinueWith(delegate
				{
					return AfterBackupBinderAsync(pickDirectoryTask, tempDir, dbName);
				});
			}
			else
			{
				if (tempDir != null) await tempDir.DeleteAsync(StorageDeleteOption.PermanentDelete).AsTask().ConfigureAwait(false);
				_animationStarter.EndAllAnimations();
				_animationStarter.StartAnimation(AnimationStarter.Animations.Failure);
			}
		}

		private async Task AfterBackupBinderAsync(Task<StorageFolder> toDirTask, StorageFolder sourceDir, string dbName)
		{
			bool isOk = false;
			if (sourceDir != null && toDirTask != null && !string.IsNullOrWhiteSpace(dbName))
			{
				var toParentDir = await toDirTask.ConfigureAwait(false);
				if (toParentDir != null)
				{
					var toDir = await toParentDir.CreateFolderAsync(dbName, CreationCollisionOption.ReplaceExisting).AsTask().ConfigureAwait(false);

					if (toDir != null)
					{
						await sourceDir.CopyDirContentsReplacingAsync(toDir).ConfigureAwait(false);
						if (sourceDir.Path.Contains(ApplicationData.Current.TemporaryFolder.Path)) await sourceDir.DeleteAsync(StorageDeleteOption.PermanentDelete).AsTask().ConfigureAwait(false);
						isOk = true;
					}
				}
			}

			_animationStarter.EndAllAnimations();
			if (isOk) _animationStarter.StartAnimation(AnimationStarter.Animations.Success);
			else _animationStarter.StartAnimation(AnimationStarter.Animations.Failure);
		}

		//private async Task AfterBackupBinderAsync(Task<StorageFolder> toDirTask, string dbName, Briefcase bc)
		//{
		//	bool isOk = false;
		//	bool isOk2 = false;
		//	var toDir = await toDirTask.ConfigureAwait(false);
		//	_animationStarter.StartAnimation(AnimationStarter.Animations.Updating);

		//	if (bc != null && toDir != null && !string.IsNullOrWhiteSpace(dbName))
		//	{
		//		isOk = await bc.ExportBinderAsync(dbName, toDir).ConfigureAwait(false);
		//		if (isOk)
		//		{
		//			if (toDir.Path.Contains(ApplicationData.Current.TemporaryFolder.Path)) await toDir.DeleteAsync(StorageDeleteOption.PermanentDelete).AsTask().ConfigureAwait(false);
		//			RegistryAccess.SetValue(ConstantData.REG_BAK_BINDER_NAME, string.Empty);
		//			RegistryAccess.SetValue(ConstantData.REG_BAK_DIR_PATH, string.Empty);
		//		}
		//		else
		//		{
		//			//var tempDir = await ApplicationData.Current.TemporaryFolder.CreateFolderAsync(ConstantData.BAK_TEMP_DIR, CreationCollisionOption.GenerateUniqueName).AsTask().ConfigureAwait(false);
		//			//isOk2 = await bc.ExportBinderAsync(dbName, tempDir).ConfigureAwait(false);
		//			//if (isOk2)
		//			//{
		//			//	RegistryAccess.SetValue(ConstantData.REG_BAK_BINDER_NAME, dbName);
		//			//	RegistryAccess.SetValue(ConstantData.REG_BAK_DIR_PATH, tempDir.Path);
		//			//}
		//			//else
		//			//{
		//				//if (tempDir != null) await tempDir.DeleteAsync(StorageDeleteOption.PermanentDelete).AsTask().ConfigureAwait(false);
		//				RegistryAccess.SetValue(ConstantData.REG_BAK_BINDER_NAME, string.Empty);
		//				RegistryAccess.SetValue(ConstantData.REG_BAK_DIR_PATH, string.Empty);
		//			//}
		//		}
		//	}

		//	_animationStarter.EndAllAnimations();
		//	if (isOk) _animationStarter.StartAnimation(AnimationStarter.Animations.Success);
		//	else
		//	{
		//		if (isOk2) _animationStarter.StartAnimation(AnimationStarter.Animations.Updating);
		//		else _animationStarter.StartAnimation(AnimationStarter.Animations.Failure);
		//	}
		//}

		//private async Task ResumeAfterExportBinderAsync()
		//{
		//	string dbName = RegistryAccess.GetValue(ConstantData.REG_BAK_BINDER_NAME);
		//	string dirPath = RegistryAccess.GetValue(ConstantData.REG_BAK_DIR_PATH);
		//	var bc = _briefcase;
		//	if (bc != null && !string.IsNullOrWhiteSpace(dbName) && !string.IsNullOrWhiteSpace(dirPath))
		//	{
		//		var dirTask = StorageFolder.GetFolderFromPathAsync(dirPath).AsTask();
		//		await AfterBackupBinderAsync(dirTask, dbName, bc).ConfigureAwait(false);
		//	}
		//}
	}
}
