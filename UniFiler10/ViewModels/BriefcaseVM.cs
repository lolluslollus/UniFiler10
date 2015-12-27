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

		public async Task<bool> ImportDbAsync()
		{
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

		public async Task StartBackupBinderAsync(string dbName)
		{
			bool isOk = false;
			var bc = _briefcase;

			if (!string.IsNullOrWhiteSpace(dbName) && bc != null && bc.DbNames.Contains(dbName))
			{
				_animationStarter.StartAnimation(AnimationStarter.Animations.Updating);
				var tempDir = await ApplicationData.Current.TemporaryFolder.CreateFolderAsync(BAK_TEMP_DIR, CreationCollisionOption.GenerateUniqueName).AsTask(); //.ConfigureAwait(false);
				isOk = await bc.ExportBinderAsync(dbName, tempDir); //.ConfigureAwait(false);
				if (isOk)
				{
					var toParentDirectoryTask = Pickers.PickFolderAsync(new string[0]); // string[] { ConstantData.DB_EXTENSION, ConstantData.XML_EXTENSION });
					var afterPickedTask = toParentDirectoryTask.ContinueWith(delegate
					{
						return AfterBackupBinderAsync(tempDir, toParentDirectoryTask);
					});
				}
				else
				{
					if (tempDir != null) await tempDir.DeleteAsync(StorageDeleteOption.PermanentDelete).AsTask().ConfigureAwait(false);
					_animationStarter.EndAllAnimations();
					_animationStarter.StartAnimation(AnimationStarter.Animations.Failure);
				}
			}
		}

		private const string BAK_TEMP_DIR = "BakBinderDir";
		private async Task AfterBackupBinderAsync(StorageFolder tempDir, Task<StorageFolder> toParentDirectory)
		{
			bool isOk = false;
			if (tempDir != null && toParentDirectory != null)
			{
				await new FileDirectoryExts().CopyDirContentsReplacingAsync(tempDir, await toParentDirectory.ConfigureAwait(false)).ConfigureAwait(false);
				isOk = true;
			}

			if (tempDir != null) await tempDir.DeleteAsync(StorageDeleteOption.PermanentDelete).AsTask().ConfigureAwait(false);

			_animationStarter.EndAllAnimations();
			if (isOk) _animationStarter.StartAnimation(AnimationStarter.Animations.Success);
			else _animationStarter.StartAnimation(AnimationStarter.Animations.Failure);
		}
	}
}
