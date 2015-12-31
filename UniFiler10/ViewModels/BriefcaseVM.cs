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

		private volatile bool _isCanImportExport = false;
		public bool IsCanImportExport { get { return _isCanImportExport; } private set { _isCanImportExport = value; RaisePropertyChanged_UI(); } }

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

			if (BinderExported == null) BinderExported += OnBinderExported;
			ResumeAfterExportBinder();
			if (ImportMergeBinderEnded == null) ImportMergeBinderEnded += OnImportMergeBinderEnded;
			await ResumeAfterImportBinderAsync().ConfigureAwait(false);
			await ResumeAfterMergeBinderAsync().ConfigureAwait(false);
		}

		private void OnBinderExported(object sender, EventArgs e)
		{
			ResumeAfterExportBinder();
		}

		private async void OnImportMergeBinderEnded(object sender, EventArgs e)
		{
			await ResumeAfterImportBinderAsync().ConfigureAwait(false);
			await ResumeAfterMergeBinderAsync().ConfigureAwait(false);
		}

		protected override Task CloseMayOverrideAsync()
		{
			BinderExported -= OnBinderExported;
			ImportMergeBinderEnded -= OnImportMergeBinderEnded;
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

		//public async Task StartImportDbAsync()
		//{
		//	bool isOk = false;
		//	var briefcase = _briefcase;
		//	if (briefcase != null)
		//	{
		//		// LOLLO TODO make this work with phone with low memory
		//		var fromDirectory = await Pickers.PickDirectoryAsync(new string[] { ConstantData.DB_EXTENSION, ConstantData.XML_EXTENSION });
		//		if (fromDirectory != null)
		//		{
		//			if (await briefcase.IsDbNameAvailableAsync(fromDirectory.Name))
		//			{
		//				var nextAction = await UserConfirmationPopup.GetInstance().GetUserConfirmationBeforeImportingBinderAsync().ConfigureAwait(false);

		//				if (nextAction == ImportBinderOperations.Merge)
		//				{
		//					isOk = await briefcase.MergeBinderAsync(fromDirectory).ConfigureAwait(false);
		//				}
		//				else if (nextAction == ImportBinderOperations.Import)
		//				{
		//					isOk = await briefcase.ImportBinderAsync(fromDirectory).ConfigureAwait(false);
		//				}
		//			}
		//			else
		//			{
		//				isOk = await briefcase.ImportBinderAsync(fromDirectory).ConfigureAwait(false);
		//			}
		//		}
		//	}
		//	if (isOk) _animationStarter.StartAnimation(AnimationStarter.Animations.Success);
		//	else _animationStarter.StartAnimation(AnimationStarter.Animations.Failure);
		//}

		public void StartImportBinder()
		{
			var bc = _briefcase;
			if (bc != null && _isCanImportExport)
			{
				// LOLLO TODO made this work with phone with low memory: check it
				IsCanImportExport = false;
				var pickTask = Pickers.PickDirectoryAsync(new string[] { ConstantData.DB_EXTENSION, ConstantData.XML_EXTENSION });
				var afterPickTask = pickTask.ContinueWith(async delegate
				{
					var directory = await pickTask; //.ConfigureAwait(false);
					if (directory != null)
					{
						if (await bc.IsDbNameAvailableAsync(directory.Name))
						{
							var nextAction = await UserConfirmationPopup.GetInstance().GetUserConfirmationBeforeImportingBinderAsync().ConfigureAwait(false);
							if (nextAction == ImportBinderOperations.Merge)
							{
								Task merge = ContinueAfterPickMergeBinder(bc, directory);
							}
							else if (nextAction == ImportBinderOperations.Import)
							{
								Task import = ContinueAfterPickImportBinder(bc, directory);
							}
						}
						else
						{
							Task import = ContinueAfterPickImportBinder(bc, directory);
						}
					}
				});
			}
			else
			{
				_animationStarter.EndAllAnimations();
				_animationStarter.StartAnimation(AnimationStarter.Animations.Failure);
			}
		}

		private async Task ContinueAfterPickImportBinder(Briefcase bc, StorageFolder dir)
		{
			bool isImported = false;
			bool isNeedsContinuing = false;
			try
			{
				if (bc != null && dir != null)
				{
					_animationStarter.StartAnimation(AnimationStarter.Animations.Updating);
					isImported = await bc.ImportBinderAsync(dir).ConfigureAwait(false);
					if (!isImported && !bc.IsOpen) isNeedsContinuing = true; // LOLLO if isOk is false because there was an error and not because the app was suspended, I must unlock impexp.
				}
			}
			catch (Exception ex)
			{
				await Logger.AddAsync(ex.ToString(), Logger.FileErrorLogFilename).ConfigureAwait(false);
			}

			_animationStarter.EndAllAnimations();
			if (isImported)
			{
				_animationStarter.StartAnimation(AnimationStarter.Animations.Success);
			}
			if (isNeedsContinuing)
			{
				RegistryAccess.SetValue(ConstantData.REG_IMPORT_BINDER_CONTINUE, true.ToString());
			}
			else
			{
				RegistryAccess.SetValue(ConstantData.REG_IMPORT_BINDER_CONTINUE, false.ToString());
				if (!isImported) _animationStarter.StartAnimation(AnimationStarter.Animations.Failure);
				IsCanImportExport = true;
			}

			ImportMergeBinderEnded?.Invoke(this, EventArgs.Empty);
		}
		private async Task ContinueAfterPickMergeBinder(Briefcase bc, StorageFolder dir)
		{
			bool isImported = false;
			bool isNeedsContinuing = false;
			try
			{
				if (bc != null && dir != null)
				{
					_animationStarter.StartAnimation(AnimationStarter.Animations.Updating);
					isImported = await bc.MergeBinderAsync(dir).ConfigureAwait(false);
					if (!isImported && !bc.IsOpen) isNeedsContinuing = true; // LOLLO if isOk is false because there was an error and not because the app was suspended, I must unlock impexp.
				}
			}
			catch (Exception ex)
			{
				await Logger.AddAsync(ex.ToString(), Logger.FileErrorLogFilename).ConfigureAwait(false);
			}

			_animationStarter.EndAllAnimations();
			if (isImported)
			{
				_animationStarter.StartAnimation(AnimationStarter.Animations.Success);
			}
			if (isNeedsContinuing)
			{
				RegistryAccess.SetValue(ConstantData.REG_MERGE_BINDER_CONTINUE, true.ToString());
			}
			else
			{
				RegistryAccess.SetValue(ConstantData.REG_MERGE_BINDER_CONTINUE, false.ToString());
				if (!isImported) _animationStarter.StartAnimation(AnimationStarter.Animations.Failure);
				IsCanImportExport = true;
			}

			ImportMergeBinderEnded?.Invoke(this, EventArgs.Empty);
		}

		private async Task ResumeAfterImportBinderAsync()
		{
			string continueImporting = RegistryAccess.GetValue(ConstantData.REG_IMPORT_BINDER_CONTINUE);
			if (continueImporting == true.ToString())
			{
				IsCanImportExport = false;
				_animationStarter.StartAnimation(AnimationStarter.Animations.Updating);
				var dir = await Pickers.GetLastPickedFolderJustOnceAsync().ConfigureAwait(false);
				await ContinueAfterPickImportBinder(_briefcase, dir).ConfigureAwait(false);
			}
			else
			{
				IsCanImportExport = true;
			}
		}

		private async Task ResumeAfterMergeBinderAsync()
		{
			string continueMerging = RegistryAccess.GetValue(ConstantData.REG_MERGE_BINDER_CONTINUE);
			if (continueMerging == true.ToString())
			{
				IsCanImportExport = false;
				_animationStarter.StartAnimation(AnimationStarter.Animations.Updating);
				var dir = await Pickers.GetLastPickedFolderJustOnceAsync().ConfigureAwait(false);
				await ContinueAfterPickMergeBinder(_briefcase, dir).ConfigureAwait(false);
			}
			else
			{
				IsCanImportExport = true;
			}
		}

		private static event EventHandler ImportMergeBinderEnded;

		public void StartExportBinder(string dbName)
		{
			var bc = _briefcase;

			if (!string.IsNullOrWhiteSpace(dbName) && bc?.DbNames?.Contains(dbName) == true && _isCanImportExport)
			{
				IsCanImportExport = false;
				RegistryAccess.SetValue(ConstantData.REG_EXPORT_BINDER_CONTINUE_EXPORTING, true.ToString());
				// LOLLO NOTE for some stupid reason, the following wants a non-empty extension list
				var pickDirectoryTask = Pickers.PickDirectoryAsync(new string[] { ConstantData.XML_EXTENSION });
				var afterPickedDirectoryTask = pickDirectoryTask.ContinueWith(delegate
				{
					return ContinueAfterExportBinderPickerAsync(pickDirectoryTask, dbName, bc);
				});
			}
			else
			{
				_animationStarter.EndAllAnimations();
				_animationStarter.StartAnimation(AnimationStarter.Animations.Failure);
			}
		}

		private async Task ContinueAfterExportBinderPickerAsync(Task<StorageFolder> toDirTask, string dbName, Briefcase bc)
		{
			bool isOk = false;
			try
			{
				if (bc != null)
				{
					var toDir = await toDirTask.ConfigureAwait(false);
					if (toDir != null)
					{
						_animationStarter.StartAnimation(AnimationStarter.Animations.Updating);
						isOk = await bc.ExportBinderAsync(dbName, toDir).ConfigureAwait(false);
					}
				}
			}
			catch (Exception ex)
			{
				await Logger.AddAsync(ex.ToString(), Logger.FileErrorLogFilename, Logger.Severity.Info).ConfigureAwait(false);
			}

			_animationStarter.EndAllAnimations();
			if (isOk) _animationStarter.StartAnimation(AnimationStarter.Animations.Success);
			else _animationStarter.StartAnimation(AnimationStarter.Animations.Failure);

			RegistryAccess.SetValue(ConstantData.REG_EXPORT_BINDER_CONTINUE_EXPORTING, false.ToString());
			IsCanImportExport = true;
			BinderExported?.Invoke(this, EventArgs.Empty);
		}

		private void ResumeAfterExportBinder()
		{
			string isBinderExporting = RegistryAccess.GetValue(ConstantData.REG_EXPORT_BINDER_CONTINUE_EXPORTING);
			if (isBinderExporting == true.ToString())
			{
				IsCanImportExport = false;
				_animationStarter.StartAnimation(AnimationStarter.Animations.Updating);
			}
			else
			{
				IsCanImportExport = true;
			}
		}

		private static event EventHandler BinderExported;
	}
}
