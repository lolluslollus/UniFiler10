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

			if (ImportMergeBinderStep1Ended == null) ImportMergeBinderStep1Ended += OnImportMergeBinderStep1Ended;
			await ResumeAfterImportBinderStep1Async().ConfigureAwait(false);

			if (ImportMergeBinderStep2Ended == null) ImportMergeBinderStep2Ended += OnImportMergeBinderStep2Ended;
			await ResumeAfterImportBinderStep2Async().ConfigureAwait(false);
			await ResumeAfterMergeBinderStep2Async().ConfigureAwait(false);
		}

		private void OnBinderExported(object sender, EventArgs e)
		{
			ResumeAfterExportBinder();
		}

		private async void OnImportMergeBinderStep1Ended(object sender, EventArgs e)
		{
			await ResumeAfterImportBinderStep1Async().ConfigureAwait(false);
		}

		private async void OnImportMergeBinderStep2Ended(object sender, EventArgs e)
		{
			await ResumeAfterImportBinderStep2Async().ConfigureAwait(false);
			await ResumeAfterMergeBinderStep2Async().ConfigureAwait(false);
		}

		protected override Task CloseMayOverrideAsync()
		{
			BinderExported -= OnBinderExported;
			ImportMergeBinderStep1Ended -= OnImportMergeBinderStep1Ended;
			ImportMergeBinderStep2Ended -= OnImportMergeBinderStep2Ended;
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

			var userWantsDelete = await UserConfirmationPopup.GetInstance().GetUserConfirmationBeforeDeletingBinderAsync();
			var isDeleted = userWantsDelete.Item1 && await briefcase.DeleteBinderAsync(dbName);

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
				// LOLLO TODO made this work with phone with low memory: when importing an existing binder, the dialog never shows up.
				IsCanImportExport = false;
				var pickTask = Pickers.PickDirectoryAsync(new string[] { ConstantData.DB_EXTENSION, ConstantData.XML_EXTENSION });
				var afterPickTask = pickTask.ContinueWith(delegate
				{
					Task cont = ContinueAfterImportSourceDirPickedStep1(bc, pickTask);
					//var directory = await pickTask; //.ConfigureAwait(false);
					//if (directory != null)
					//{
					//if (bc?.IsOpen == true)
					//{
					//	if (await bc.IsDbNameAvailableAsync(directory.Name))
					//	{
					//		await Logger.AddAsync("db name is available", Logger.FileErrorLogFilename, Logger.Severity.Info); //.ConfigureAwait(false);
					//		var nextAction = await UserConfirmationPopup.GetInstance().GetUserConfirmationBeforeImportingBinderAsync(); //.ConfigureAwait(false);
					//		await Logger.AddAsync("user choice = " + nextAction.Item1.ToString(), Logger.FileErrorLogFilename, Logger.Severity.Info).ConfigureAwait(false);
					//		await Logger.AddAsync("user has interacted = " + nextAction.Item2.ToString(), Logger.FileErrorLogFilename, Logger.Severity.Info).ConfigureAwait(false);
					//		if (nextAction.Item1 == ImportBinderOperations.Merge)
					//		{
					//			Task merge = ContinueAfterPickMergeBinder(bc, directory);
					//		}
					//		else if (nextAction.Item1 == ImportBinderOperations.Import)
					//		{
					//			Task import = ContinueAfterPickImportBinder(bc, directory);
					//		}
					//	}
					//	else
					//	{
					//		await Logger.AddAsync("db name is NOT available", Logger.FileErrorLogFilename, Logger.Severity.Info).ConfigureAwait(false);
					//		Task import = ContinueAfterPickImportBinder(bc, directory);
					//	}
					//}
					//}
				});
			}
			else
			{
				_animationStarter.EndAllAnimations();
				_animationStarter.StartAnimation(AnimationStarter.Animations.Failure);
			}
		}

		private async Task ContinueAfterImportSourceDirPickedStep1(Briefcase bc, Task<StorageFolder> pickDir)
		{
			bool isNeedsContinuing = false;
			try
			{
				var dir = await pickDir; //.ConfigureAwait(false);
				if (bc != null && dir != null)
				{
					var isDbNameAvailable = await bc.IsDbNameAvailableAsync(dir.Name);
					if (isDbNameAvailable == BoolWhenOpen.Yes)
					{
						await Logger.AddAsync("db name is available", Logger.FileErrorLogFilename, Logger.Severity.Info); //.ConfigureAwait(false);
						var nextAction = await UserConfirmationPopup.GetInstance().GetUserConfirmationBeforeImportingBinderAsync(); //.ConfigureAwait(false);
						await Logger.AddAsync("user choice = " + nextAction.Item1.ToString(), Logger.FileErrorLogFilename, Logger.Severity.Info).ConfigureAwait(false);
						await Logger.AddAsync("user has interacted = " + nextAction.Item2.ToString(), Logger.FileErrorLogFilename, Logger.Severity.Info).ConfigureAwait(false);
						if (nextAction.Item1 == ImportBinderOperations.Merge)
						{
							await ContinueAfterPickMergeBinderStep2(bc, dir).ConfigureAwait(false);
						}
						else if (nextAction.Item1 == ImportBinderOperations.Import)
						{
							await ContinueAfterPickImportBinderStep2(bc, dir).ConfigureAwait(false);
						}
					}
					else if (isDbNameAvailable == BoolWhenOpen.No)
					{
						await Logger.AddAsync("db name is NOT available", Logger.FileErrorLogFilename, Logger.Severity.Info).ConfigureAwait(false);
						await ContinueAfterPickImportBinderStep2(bc, dir).ConfigureAwait(false);
					}
					else if (isDbNameAvailable == BoolWhenOpen.ObjectClosed)
					{
						await Logger.AddAsync("briefcase is closed", Logger.FileErrorLogFilename, Logger.Severity.Info).ConfigureAwait(false);
						isNeedsContinuing = true;
					}
				}
			}
			catch (Exception ex)
			{
				await Logger.AddAsync(ex.ToString(), Logger.FileErrorLogFilename).ConfigureAwait(false);
			}

			RegistryAccess.SetValue(ConstantData.REG_IMPORT_MERGE_BINDER_STEP1_CONTINUE, isNeedsContinuing.ToString());

			ImportMergeBinderStep1Ended?.Invoke(this, EventArgs.Empty);
		}

		private async Task ContinueAfterPickImportBinderStep2(Briefcase bc, StorageFolder dir)
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

			RegistryAccess.SetValue(ConstantData.REG_IMPORT_BINDER_STEP2_CONTINUE, isNeedsContinuing.ToString());
			if (!isNeedsContinuing)
			{
				if (!isImported) _animationStarter.StartAnimation(AnimationStarter.Animations.Failure);
				IsCanImportExport = true;
			}

			ImportMergeBinderStep2Ended?.Invoke(this, EventArgs.Empty);
		}
		private async Task ContinueAfterPickMergeBinderStep2(Briefcase bc, StorageFolder dir)
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

			RegistryAccess.SetValue(ConstantData.REG_MERGE_BINDER_STEP2_CONTINUE, isNeedsContinuing.ToString());
			if (!isNeedsContinuing)
			{
				if (!isImported) _animationStarter.StartAnimation(AnimationStarter.Animations.Failure);
				IsCanImportExport = true;
			}

			ImportMergeBinderStep2Ended?.Invoke(this, EventArgs.Empty);
		}

		private async Task ResumeAfterImportBinderStep1Async()
		{
			await Logger.AddAsync("starting", Logger.FileErrorLogFilename, Logger.Severity.Info);
			string continueImporting = RegistryAccess.GetValue(ConstantData.REG_IMPORT_MERGE_BINDER_STEP1_CONTINUE);
			if (continueImporting == true.ToString())
			{
				Task run = RunInUiThreadAsync(delegate 
				{
					Task cont = ContinueAfterImportSourceDirPickedStep1(_briefcase, Pickers.GetLastPickedFolderJustOnceAsync());
				});
//				await ContinueAfterImportSourceDirPickedStep1(_briefcase, Pickers.GetLastPickedFolderJustOnceAsync()).ConfigureAwait(false);
			}
		}

		private async Task ResumeAfterImportBinderStep2Async()
		{
			string continueImporting = RegistryAccess.GetValue(ConstantData.REG_IMPORT_BINDER_STEP2_CONTINUE);
			if (continueImporting == true.ToString())
			{
				IsCanImportExport = false;
				_animationStarter.StartAnimation(AnimationStarter.Animations.Updating);
				var dir = await Pickers.GetLastPickedFolderJustOnceAsync().ConfigureAwait(false);
				await ContinueAfterPickImportBinderStep2(_briefcase, dir).ConfigureAwait(false);
			}
			else
			{
				IsCanImportExport = true;
			}
		}

		private async Task ResumeAfterMergeBinderStep2Async()
		{
			string continueMerging = RegistryAccess.GetValue(ConstantData.REG_MERGE_BINDER_STEP2_CONTINUE);
			if (continueMerging == true.ToString())
			{
				IsCanImportExport = false;
				_animationStarter.StartAnimation(AnimationStarter.Animations.Updating);
				var dir = await Pickers.GetLastPickedFolderJustOnceAsync().ConfigureAwait(false);
				await ContinueAfterPickMergeBinderStep2(_briefcase, dir).ConfigureAwait(false);
			}
			else
			{
				IsCanImportExport = true;
			}
		}

		private static event EventHandler ImportMergeBinderStep1Ended;
		private static event EventHandler ImportMergeBinderStep2Ended;

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
