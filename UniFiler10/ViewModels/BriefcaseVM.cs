using System;
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
		public Briefcase Briefcase { get { return _briefcase; } private set { _briefcase = value; RaisePropertyChanged_UI(); } }

		private volatile bool _isNewDbNameVisible = false;
		public bool IsNewDbNameVisible { get { return _isNewDbNameVisible; } set { _isNewDbNameVisible = value; RaisePropertyChanged_UI(); if (_isNewDbNameVisible) { Task upd = UpdateIsNewDbNameErrorMessageVisibleAsync(); } } }

		private volatile bool _isNewDbNameErrorMessageVisible = false;
		public bool IsNewDbNameErrorMessageVisible { get { return _isNewDbNameErrorMessageVisible; } set { _isNewDbNameErrorMessageVisible = value; RaisePropertyChanged_UI(); } }

		private string _newDbName = string.Empty;
		public string NewDbName { get { return _newDbName; } set { _newDbName = value; RaisePropertyChanged_UI(); Task upd = UpdateIsNewDbNameErrorMessageVisibleAsync(); } }

		private volatile bool _isCanImportExport = false;
		public bool IsCanImportExport { get { return _isCanImportExport; } private set { _isCanImportExport = value; RaisePropertyChanged_UI(); } }
		private void UpdateIsCanImportExport()
		{
			lock (_isImportingExportingLocker)
			{
				IsCanImportExport = !IsExportingBinder && !IsImportingBinder;
			}
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
					RaisePropertyChanged_UI();
					IsCanImportExport = !value && !IsExportingBinder;
				}
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
					RaisePropertyChanged_UI();
					IsCanImportExport = !value && !IsImportingBinder;
				}
			}
		}

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

			UpdateIsCanImportExport();

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

		public async void StartImportBinder()
		{
			var bc = _briefcase;
			if (bc != null && !IsImportingBinder)
			{
				IsImportingBinder = true;

				//RegistryAccess.SetValue(ConstantData.REG_IMPORT_BINDER_STEP, "1");

				var dir = await Pickers.PickDirectoryAsync(new string[] { ConstantData.DB_EXTENSION, ConstantData.XML_EXTENSION }).ConfigureAwait(false);

				// LOLLO NOTE at this point, OnResuming() has just started, if the app was suspended. We cannot even know if we are open.
				// To avoid surprises, we try the following here under _isOpenSemaphore. If it does not run through, IsImportingBinder will stay true.
				// In OpenMayOverrideAsync, we check IsImportingBinder and, if true, we try again.
				// ContinueAfterPickAsync sets IsImportingBinder to false, so there won't be redundant attempts.
				await RunFunctionIfOpenThreeStateAsyncT(delegate
				{
					return ContinueImportBinderStep1Async(bc, dir);
				}).ConfigureAwait(false);
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

						//RegistryAccess.SetValue(ConstantData.REG_IMPORT_BINDER_STEP, "2");

						var nextAction = await UserConfirmationPopup.GetInstance().GetUserConfirmationBeforeImportingBinderAsync().ConfigureAwait(false);
						//RegistryAccess.SetValue(ConstantData.REG_IMPORT_BINDER_STEP2_ACTION, nextAction.Item1.ToString()); // small race here, hard to avoid

						// LOLLO there is no suspend here, so we don't need the extra complexity for step 2 and _isOpenOrOpening.

						// LOLLO NOTE at this point, OnResuming() has just started, if the app was suspended. We cannot even know if we are open.
						// To avoid surprises, we try the following here under _isOpenSemaphore. 
						// note that this method is always called under _isOpenSemaphore.
						// If it does not run through, IsImportingFolders will stay true.
						// In OpenMayOverrideAsync, we check IsImportingFolders and, if true, we try again.
						// ContinueAfterPickAsync sets IsImportingFolders to false, so there won't be redundant attempts.
						//if (_isOpen) // this will be false when called from OpenMayOverrideAsync()
						//{
						//if (_isOpenOrOpening) // this will be false when running on a phone with low memory - No it won't!
						//{
							Logger.Add_TPL("ContinueImportBinderStep1Async(): user choice = " + nextAction.Item1.ToString(), Logger.AppEventsLogFilename, Logger.Severity.Info);
							Logger.Add_TPL("ContinueImportBinderStep1Async(): user has interacted = " + nextAction.Item2.ToString(), Logger.AppEventsLogFilename, Logger.Severity.Info);
							if (nextAction.Item1 == ImportBinderOperations.Merge) await ContinueImportBinderStep2_Merge_Async(bc, dir).ConfigureAwait(false);
							else if (nextAction.Item1 == ImportBinderOperations.Import) await ContinueImportBinderStep2_Import_Async(bc, dir).ConfigureAwait(false);
							else ContinueImportBinderStep2_Cancel();
						//}
						//else
						//{
						//	// LOLLO there is no suspend here, so we don't need the extra complexity for step 2 and _isOpenOrOpening.
						//	// This line will never hit then, check it.
						//	Logger.Add_TPL("ContinueImportBinderStep1Async(): _isOpenOrOpening = false", Logger.AppEventsLogFilename, Logger.Severity.Info);
						//}
						//}
					}
					else if (isDbNameAvailable == BoolWhenOpen.No)
					{
						Logger.Add_TPL("ContinueImportBinderStep1Async(): db name is NOT available", Logger.AppEventsLogFilename, Logger.Severity.Info);
						await ContinueImportBinderStep2_Import_Async(bc, dir).ConfigureAwait(false);
					}
					else if (isDbNameAvailable == BoolWhenOpen.ObjectClosed)
					{
						await Logger.AddAsync("ContinueImportBinderStep1Async(): briefcase is closed, which should never happen", Logger.AppEventsLogFilename).ConfigureAwait(false);
					}
				}
			}
			catch (Exception ex)
			{
				await Logger.AddAsync(ex.ToString(), Logger.AppEventsLogFilename).ConfigureAwait(false);
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
			if (isImported)
			{
				_animationStarter.StartAnimation(AnimationStarter.Animations.Success);
			}
			else
			{
				_animationStarter.StartAnimation(AnimationStarter.Animations.Failure);
			}

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
			if (isImported)
			{
				_animationStarter.StartAnimation(AnimationStarter.Animations.Success);
			}
			else
			{
				_animationStarter.StartAnimation(AnimationStarter.Animations.Failure);
			}

			IsImportingBinder = false;
		}

		private void ContinueImportBinderStep2_Cancel()
		{
			_animationStarter.EndAllAnimations();

			IsImportingBinder = false;
		}

		public async void StartExportBinder(string dbName)
		{
			var bc = _briefcase;
			if (!string.IsNullOrWhiteSpace(dbName) && bc?.DbNames?.Contains(dbName) == true && !IsExportingBinder)
			{
				IsExportingBinder = true;

				RegistryAccess.TrySetValue(ConstantData.REG_EXPORT_BINDER_DBNAME, dbName);
				// LOLLO NOTE for some stupid reason, the following wants a non-empty extension list
				var dir = await Pickers.PickDirectoryAsync(new string[] { ConstantData.XML_EXTENSION }).ConfigureAwait(false);

				// LOLLO NOTE at this point, OnResuming() has just started, if the app was suspended. We cannot even know if we are open.
				// To avoid surprises, we try the following here under _isOpenSemaphore. If it does not run through, IsImportingFolders will stay true.
				// In OpenMayOverrideAsync, we check IsImportingFolders and, if true, we try again.
				// ContinueAfterPickAsync sets IsImportingFolders to false, so there won't be redundant attempts.
				await RunFunctionIfOpenThreeStateAsyncT(delegate
				{
					return ContinueAfterExportBinderPickerAsync(dir, dbName, bc);
				}).ConfigureAwait(false);
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
					isExported = await bc.ExportBinderAsync(dbName, toDir).ConfigureAwait(false);
				}
			}
			catch (Exception ex)
			{
				await Logger.AddAsync(ex.ToString(), Logger.FileErrorLogFilename, Logger.Severity.Info).ConfigureAwait(false);
			}

			_animationStarter.EndAllAnimations();
			if (isExported) _animationStarter.StartAnimation(AnimationStarter.Animations.Success);
			else _animationStarter.StartAnimation(AnimationStarter.Animations.Failure);

			IsExportingBinder = false;
		}
	}
}