﻿using System;
using System.Threading.Tasks;
using UniFiler10.Controlz;
using UniFiler10.Data.Constants;
using UniFiler10.Data.Model;
using Utilz;
using Utilz.Data;
using Windows.Storage;

namespace UniFiler10.ViewModels
{
	public class BriefcaseVM : OpenableObservableDisposableData
	{
		private Briefcase _briefcase = null;
		public Briefcase Briefcase { get { return _briefcase; } }

		private bool _isNewDbNameVisible = false;
		public bool IsNewDbNameVisible { get { return _isNewDbNameVisible; } set { if (_isNewDbNameVisible != value) { _isNewDbNameVisible = value; RaisePropertyChanged_UI(); if (_isNewDbNameVisible) { Task upd = UpdateIsNewDbNameErrorMessageVisibleAsync(); } } } }

		private bool _isNewDbNameErrorMessageVisible = false;
		public bool IsNewDbNameErrorMessageVisible { get { return _isNewDbNameErrorMessageVisible; } set { if (_isNewDbNameErrorMessageVisible != value) { _isNewDbNameErrorMessageVisible = value; RaisePropertyChanged_UI(); } } }

		private string _newDbName = string.Empty;
		public string NewDbName { get { return _newDbName; } set { if (_newDbName != value) { _newDbName = value; RaisePropertyChanged_UI(); Task upd = UpdateIsNewDbNameErrorMessageVisibleAsync(); } } }

		private bool _isCanImportExport = false;
		public bool IsCanImportExport
		{
			get
			{
				// return Volatile.Read(ref _isCanImportExport); // this is slower than volatile
				lock (_isImportingExportingLocker)
				{
					return _isCanImportExport;
				}
			}
		}
		private Task UpdateIsCanImportExportAsync()
		{
			// run it in the UI thread to avoid deadlocks between the locker in the getter and the locker in the setter
			return RunInUiThreadAsync(() =>
			{
				_isCanImportExport = !IsExportingBinder && !IsImportingBinder;
				RaisePropertyChanged(nameof(IsCanImportExport));
			});
		}

		private static readonly object _isImportingExportingLocker = new object();
		public bool IsImportingBinder
		{
			get
			{
				lock (_isImportingExportingLocker)
				{
					string tf = RegistryAccess.GetValue(ConstantData.REG_EXPORT_BINDER_IS_EXPORTING);
					return tf == true.ToString();
				}
			}
			private set
			{
				lock (_isImportingExportingLocker)
				{
					RegistryAccess.TrySetValue(ConstantData.REG_EXPORT_BINDER_IS_EXPORTING, value.ToString());
				}
				UpdateIsCanImportExportAsync();
			}
		}
		private bool TrySetIsImportingBinder(bool newValue)
		{
			lock (_isImportingExportingLocker)
			{
				if (IsImportingBinder == newValue) return false;

				IsImportingBinder = newValue;
				return true;
			}
		}

		public bool IsExportingBinder
		{
			get
			{
				lock (_isImportingExportingLocker)
				{
					string tf = RegistryAccess.GetValue(ConstantData.REG_IMPORT_BINDER_IS_IMPORTING);
					return tf == true.ToString();
				}
			}
			private set
			{
				lock (_isImportingExportingLocker)
				{
					RegistryAccess.TrySetValue(ConstantData.REG_IMPORT_BINDER_IS_IMPORTING, value.ToString());
				}
				UpdateIsCanImportExportAsync();
			}
		}
		private bool TrySetIsExportingBinder(bool newValue)
		{
			lock (_isImportingExportingLocker)
			{
				if (IsExportingBinder == newValue) return false;

				IsExportingBinder = newValue;
				return true;
			}
		}

		private readonly AnimationStarter _animationStarter = null;

		public BriefcaseVM(AnimationStarter animationStarter)
		{
			if (animationStarter == null) throw new ArgumentNullException("BriefcaseVM ctor: animationStarter may not be null");
			_animationStarter = animationStarter;
		}

		protected override async Task OpenMayOverrideAsync(object args = null)
		{
			_briefcase = Briefcase.GetCreateInstance();
			await _briefcase.OpenAsync();
			RaisePropertyChanged_UI(nameof(Briefcase)); // notify UI once briefcase is open

			await UpdateIsCanImportExportAsync().ConfigureAwait(false);

			if (IsExportingBinder)
			{
				string dbName = RegistryAccess.GetValue(ConstantData.REG_EXPORT_BINDER_DBNAME);
				await ContinueAfterExportBinderPickerAsync(await Pickers.GetLastPickedFolderAsync().ConfigureAwait(false), dbName, _briefcase).ConfigureAwait(false);
			}
			if (IsImportingBinder)
			{
				var dir = await Pickers.GetLastPickedFolderAsync().ConfigureAwait(false);
				//string step = RegistryAccess.GetValue(ConstantData.REG_IMPORT_BINDER_STEP);
				//if (step == "1")
				//{
				await ContinueImportBinderStep1Async(_briefcase, dir).ConfigureAwait(false);
				//}
				//else if (step == "2")
				//{
				//	string action = RegistryAccess.GetValue(ConstantData.REG_IMPORT_BINDER_STEP2_ACTION);
				//	if (action == ImportBinderOperations.Import.ToString())
				//	{
				//		await ContinueImportBinderStep2_Import_Async(_briefcase, dir).ConfigureAwait(false);
				//	}
				//	else if (action == ImportBinderOperations.Merge.ToString())
				//	{
				//		await ContinueImportBinderStep2_Merge_Async(_briefcase, dir).ConfigureAwait(false);
				//	}
				//	else
				//	{
				//		ContinueImportBinderStep2_Cancel();
				//	}
				//}
			}
		}

		public Task<bool> AddDbStep0Async()
		{
			return RunFunctionIfOpenAsyncB(delegate
			{
				var bf = _briefcase;
				if (bf == null || !bf.IsOpen) return false;

				IsNewDbNameVisible = true;

				return true;
			});
		}

		public Task<bool> AddDbStep1Async()
		{
			return RunFunctionIfOpenAsyncTB(async delegate
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
			});
		}

		public async Task<bool> TryOpenCurrentBinderAsync(string dbName)
		{
			var bf = _briefcase;
			if (bf == null) return false;

			return await bf.SetCurrentBinderNameAsync(dbName).ConfigureAwait(false);
		}
		public Task CloseBinderAsync()
		{
			return _briefcase?.CloseCurrentBinderAsync();
		}

		private Task UpdateIsNewDbNameErrorMessageVisibleAsync()
		{
			return RunFunctionIfOpenAsyncT(async delegate
			{
				var bf = _briefcase;
				if (bf != null)
				{
					bool isDbNameWrongAndBriefcaseIsOpen = await bf.IsNewDbNameWrongAsync(_newDbName).ConfigureAwait(false);
					IsNewDbNameErrorMessageVisible = isDbNameWrongAndBriefcaseIsOpen;
				}
				else
				{
					IsNewDbNameErrorMessageVisible = false;
				}
			});
		}

		public async Task<bool> DeleteDbAsync(string dbName)
		{
			var briefcase = _briefcase;
			if (briefcase == null) return false;

			var userWantsDelete = await UserConfirmationPopup.GetInstance().GetUserConfirmationBeforeDeletingBinderAsync(CancToken);
			if (CancToken.IsCancellationRequested) { return false; }
			var isDeleted = userWantsDelete.Item1 && await briefcase.DeleteBinderAsync(dbName);

			return isDeleted;
		}

		public enum ImportBinderOperations { Cancel, Import, Merge }

		public async void StartImportBinderIntoBriefcase()
		{
			if (!IsOpen) return;
			var bc = _briefcase;
			if (bc != null && TrySetIsImportingBinder(true))
			{
				var dir = await Pickers.PickDirectoryAsync(new[] { ConstantData.DB_EXTENSION, ConstantData.XML_EXTENSION }).ConfigureAwait(false);
				// LOLLO NOTE at this point, OnResuming() has just started, if the app was suspended. We cannot even know if we are open.
				// To avoid surprises, we try the following here under _isOpenSemaphore. If it does not run through, IsImportingBinder will stay true.
				// In OpenMayOverrideAsync, we check IsImportingBinder and, if true, we try again.
				// ContinueAfterPickAsync sets IsImportingBinder to false, so there won't be redundant attempts.
				await RunFunctionIfOpenThreeStateAsyncT(() => ContinueImportBinderStep1Async(bc, dir)).ConfigureAwait(false);
			}
			else
			{
				_animationStarter.EndAllAnimations();
				_animationStarter.StartAnimation(AnimationStarter.Animations.Failure);
			}
		}

		public async void StartImportBinderIntoBinder(string targetBinderName)
		{
			if (!IsOpen) return;
			var bc = _briefcase;
			if (bc != null && TrySetIsImportingBinder(true))
			{
				var userChoice = await UserConfirmationPopup.GetInstance().GetUserChoiceBeforeImportingBinderAsync(targetBinderName, CancToken);
				if (CancToken.IsCancellationRequested) { IsImportingBinder = false; return; }
				if (userChoice.Item1 == ImportBinderOperations.Cancel) { IsImportingBinder = false; return; }

				StorageFolder dir = null;
				if (string.IsNullOrWhiteSpace(userChoice.Item2))
				{
					dir = await Pickers.PickDirectoryAsync(new[] { ConstantData.DB_EXTENSION, ConstantData.XML_EXTENSION }).ConfigureAwait(false);
				}
				else
				{
					dir = await Briefcase.BindersDirectory.TryGetItemAsync(userChoice.Item2).AsTask().ConfigureAwait(false) as StorageFolder;
					Pickers.SetLastPickedFolder(dir);
				}
				// LOLLO NOTE at this point, OnResuming() has just started, if the app was suspended. We cannot even know if we are open.
				// To avoid surprises, we try the following here under _isOpenSemaphore. If it does not run through, IsImportingBinder will stay true.
				// In OpenMayOverrideAsync, we check IsImportingBinder and, if true, we try again.
				// ContinueAfterPickAsync sets IsImportingBinder to false, so there won't be redundant attempts.
				await RunFunctionIfOpenThreeStateAsyncT(() => ContinueImportBinderStep1Async(bc, dir)).ConfigureAwait(false);
			}
			else
			{
				_animationStarter.EndAllAnimations();
				_animationStarter.StartAnimation(AnimationStarter.Animations.Failure);
			}
		}

		private async Task ContinueImportBinderStep1Async(Briefcase bc, StorageFolder dir)
		{
			try
			{
				if (bc != null && dir != null)
				{
					var isDbNameAvailable = await bc.IsDbNameAvailableAsync(dir.Name).ConfigureAwait(false);
					if (isDbNameAvailable == BoolWhenOpen.Yes)
					{
						Logger.Add_TPL("ContinueImportBinderStep1Async(): db name is available", Logger.AppEventsLogFilename, Logger.Severity.Info);

						var nextAction = await UserConfirmationPopup.GetInstance().GetUserConfirmationBeforeImportingBinderAsync(CancToken).ConfigureAwait(false);
						if (CancToken.IsCancellationRequested) ContinueImportBinderStep2_Cancel();
						Logger.Add_TPL("ContinueImportBinderStep1Async(): user choice = " + nextAction.Item1.ToString(), Logger.AppEventsLogFilename, Logger.Severity.Info);
						Logger.Add_TPL("ContinueImportBinderStep1Async(): user has interacted = " + nextAction.Item2.ToString(), Logger.AppEventsLogFilename, Logger.Severity.Info);
						if (nextAction.Item1 == ImportBinderOperations.Merge) await ContinueImportBinderStep2_Merge_Async(bc, dir).ConfigureAwait(false);
						else if (nextAction.Item1 == ImportBinderOperations.Import) await ContinueImportBinderStep2_Import_Async(bc, dir).ConfigureAwait(false);
						else ContinueImportBinderStep2_Cancel();
					}
					else if (isDbNameAvailable == BoolWhenOpen.No)
					{
						Logger.Add_TPL("ContinueImportBinderStep1Async(): db name is NOT available", Logger.AppEventsLogFilename, Logger.Severity.Info);
						await ContinueImportBinderStep2_Import_Async(bc, dir).ConfigureAwait(false);
					}
					else if (isDbNameAvailable == BoolWhenOpen.ObjectClosed)
					{
						await Logger.AddAsync("ContinueImportBinderStep1Async(): briefcase is closed, which should never happen, or the operation was cancelled", Logger.AppEventsLogFilename).ConfigureAwait(false);
					}
				}
			}
			catch (Exception ex)
			{
				await Logger.AddAsync(ex.ToString(), Logger.AppEventsLogFilename).ConfigureAwait(false);
			}
			finally
			{
				IsImportingBinder = false;
			}
		}

		private async Task ContinueImportBinderStep2_Import_Async(Briefcase bc, StorageFolder dir)
		{
			bool isImported = false;

			try
			{
				if (bc != null && dir != null)
				{
					_animationStarter.StartAnimation(AnimationStarter.Animations.Updating);
					isImported = await bc.ImportBinderAsync(dir).ConfigureAwait(false);
				}
			}
			catch (Exception ex)
			{
				await Logger.AddAsync(ex.ToString(), Logger.AppEventsLogFilename).ConfigureAwait(false);
			}

			_animationStarter.EndAllAnimations();
			_animationStarter.StartAnimation(isImported
				? AnimationStarter.Animations.Success
				: AnimationStarter.Animations.Failure);

			IsImportingBinder = false;
		}
		private async Task ContinueImportBinderStep2_Merge_Async(Briefcase bc, StorageFolder dir)
		{
			bool isImported = false;

			try
			{
				if (bc != null && dir != null)
				{
					_animationStarter.StartAnimation(AnimationStarter.Animations.Updating);
					isImported = await bc.MergeBinderAsync(dir).ConfigureAwait(false);
				}
			}
			catch (Exception ex)
			{
				await Logger.AddAsync(ex.ToString(), Logger.FileErrorLogFilename).ConfigureAwait(false);
			}

			_animationStarter.EndAllAnimations();
			_animationStarter.StartAnimation(isImported
				? AnimationStarter.Animations.Success
				: AnimationStarter.Animations.Failure);

			IsImportingBinder = false;
		}

		private void ContinueImportBinderStep2_Cancel()
		{
			_animationStarter.EndAllAnimations();

			IsImportingBinder = false;
		}

		public async void StartExportBinder(string dbName)
		{
			if (!IsOpen) return;
			var bc = _briefcase;
			if (!string.IsNullOrWhiteSpace(dbName) && bc?.DbNames?.Contains(dbName) == true && TrySetIsExportingBinder(true))
			{
				RegistryAccess.TrySetValue(ConstantData.REG_EXPORT_BINDER_DBNAME, dbName);
				// LOLLO NOTE for some stupid reason, the following wants a non-empty extension list
				var dir = await Pickers.PickDirectoryAsync(new string[] { ConstantData.XML_EXTENSION }).ConfigureAwait(false);

				// LOLLO NOTE at this point, OnResuming() has just started, if the app was suspended. We cannot even know if we are open.
				// To avoid surprises, we try the following here under _isOpenSemaphore. If it does not run through, IsImportingFolders will stay true.
				// In OpenMayOverrideAsync, we check IsImportingFolders and, if true, we try again.
				// ContinueAfterPickAsync sets IsImportingFolders to false, so there won't be redundant attempts.
				await RunFunctionIfOpenThreeStateAsyncT(() => ContinueAfterExportBinderPickerAsync(dir, dbName, bc)).ConfigureAwait(false);
			}
			else
			{
				_animationStarter.EndAllAnimations();
				_animationStarter.StartAnimation(AnimationStarter.Animations.Failure);
			}
		}

		private async Task ContinueAfterExportBinderPickerAsync(StorageFolder toDir, string dbName, Briefcase bc)
		{
			bool isExported = false;
			try
			{
				if (bc != null && toDir != null)
				{
					_animationStarter.StartAnimation(AnimationStarter.Animations.Updating);

					if (string.IsNullOrWhiteSpace(dbName) /*|| _dbNames?.Contains(dbName) == false */|| toDir == null) return;

					var fromDirectory = await Briefcase.BindersDirectory
						.GetFolderAsync(dbName)
						.AsTask().ConfigureAwait(false);
					if (fromDirectory == null) return;
					// what if you copy a directory to an existing one? Shouldn't you delete the contents first? No! But then, shouldn't you issue a warning?
					var toDirectoryTest = await toDir.TryGetItemAsync(dbName).AsTask().ConfigureAwait(false);
					if (toDirectoryTest != null)
					{
						var confirmation =
							await UserConfirmationPopup.GetInstance().GetUserConfirmationBeforeExportingBinderAsync(CancToken).ConfigureAwait(false);
						if (CancToken.IsCancellationRequested) return;
						if (confirmation == null || confirmation.Item1 == false || confirmation.Item2 == false) return;
					}

					isExported = await bc.ExportBinderAsync(dbName, fromDirectory, toDir).ConfigureAwait(false);
				}
			}
			catch (Exception ex)
			{
				await Logger.AddAsync(ex.ToString(), Logger.FileErrorLogFilename, Logger.Severity.Info).ConfigureAwait(false);
			}
			finally
			{
				_animationStarter.EndAllAnimations();
				if (!CancToken.IsCancellationRequested)
					_animationStarter.StartAnimation(isExported
						? AnimationStarter.Animations.Success
						: AnimationStarter.Animations.Failure);

				IsExportingBinder = false;
			}
		}
	}
}