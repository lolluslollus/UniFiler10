using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UniFiler10.Controlz;
using UniFiler10.Data.Constants;
using UniFiler10.Data.Metadata;
using UniFiler10.Data.Model;
using UniFiler10.Services;
using Utilz;
using Utilz.Data;
using Windows.Storage;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Media;

namespace UniFiler10.ViewModels
{
	public sealed class SettingsVM : OpenableObservableDisposableData
	{
		public class FieldDescriptionPlus : ObservableData
		{
			public enum PermissionLevels { No, WithCaution, Yes }

			private PermissionLevels _isAllowUnassign = PermissionLevels.No;
			public PermissionLevels IsAllowUnassign { get { return _isAllowUnassign; } /*private set { _isAllowUnassign = value; RaisePropertyChanged_UI(); } */}
			private PermissionLevels _isAllowDelete = PermissionLevels.No;
			public PermissionLevels IsAllowDelete { get { return _isAllowDelete; } /*private set { _isAllowDelete = value; RaisePropertyChanged_UI(); }*/ }

			private FieldDescription _fieldDescription = null;
			public FieldDescription FieldDescription { get { return _fieldDescription; } }

			private FieldDescriptionPlus(FieldDescription fldDsc)
			{
				if (fldDsc == null) return;
				var mbc = MetaBriefcase.OpenInstance;
				if (mbc == null) return;

				_fieldDescription = fldDsc;
				// LOLLO TODO the following line was buggy, check it
				var catsWhereThisFieldWasAssignedBefore = mbc.Categories.Where(cat => cat?.FieldDescriptionIds != null && !fldDsc.JustAssignedToCats.Contains(cat.Id) && cat.FieldDescriptionIds.Contains(fldDsc.Id));

				if (catsWhereThisFieldWasAssignedBefore?.Any() == true)
				{
					if (GetIsElevated()) _isAllowDelete = PermissionLevels.WithCaution;
				}
				else _isAllowDelete = PermissionLevels.Yes;

				string currCatId = mbc.CurrentCategoryId;
				if (!string.IsNullOrEmpty(currCatId))
				{
					if (fldDsc.JustAssignedToCats.Contains(currCatId)) _isAllowUnassign = PermissionLevels.Yes;
					else
					{
						if (GetIsElevated()) _isAllowUnassign = PermissionLevels.WithCaution;
					}
				}
				else _isAllowUnassign = PermissionLevels.Yes;
			}

			public static IList<FieldDescriptionPlus> Get(IEnumerable<FieldDescription> fldDscs)
			{
				var result = new List<FieldDescriptionPlus>();
				if (fldDscs == null) return result;
				foreach (var fldDsc in fldDscs)
				{
					result.Add(new FieldDescriptionPlus(fldDsc));
				}
				return result;
			}
		}

		#region properties
		private readonly BackgroundTaskHelper _backgroundTaskHelper = null;
		public BackgroundTaskHelper BackgroundTaskHelper { get { return _backgroundTaskHelper; } }

		private readonly Briefcase _briefcase = null;
		public Briefcase Briefcase { get { return _briefcase; } }

		private SwitchableObservableDisposableCollection<FieldDescriptionPlus> _assignedFields = null;
		public SwitchableObservableDisposableCollection<FieldDescriptionPlus> AssignedFields { get { return _assignedFields; } private set { _assignedFields = value; RaisePropertyChanged_UI(); } }

		private SwitchableObservableDisposableCollection<FieldDescriptionPlus> _unassignedFields = null;
		public SwitchableObservableDisposableCollection<FieldDescriptionPlus> UnassignedFields { get { return _unassignedFields; } private set { _unassignedFields = value; RaisePropertyChanged_UI(); } }

		private async Task UpdateAssignedUnassignedFieldsAsync()
		{
			await RunInUiThreadAsync(() =>
			{
				var mbc = _briefcase.MetaBriefcase;
				var unaFlds = _unassignedFields; var assFlds = AssignedFields;
				if (unaFlds == null || assFlds == null) return;

				unaFlds.Clear(); assFlds.Clear();
				if (mbc?.FieldDescriptions == null || mbc.CurrentCategory?.FieldDescriptions == null) return;

				assFlds.AddRange(FieldDescriptionPlus.Get(mbc.FieldDescriptions
					.Where(allFldDsc => mbc.CurrentCategory.FieldDescriptions.Any(catFldDsc => catFldDsc.Id == allFldDsc.Id))));

				unaFlds.AddRange(FieldDescriptionPlus.Get(mbc.FieldDescriptions
					.Where(allFldDsc => mbc.CurrentCategory.FieldDescriptions.All(catFldDsc => catFldDsc.Id != allFldDsc.Id))));
			}).ConfigureAwait(false);
			RaisePropertyChanged_UI(nameof(UnassignedFields));
		}

		public void Refresh()
		{
			Task upd = UpdateAssignedUnassignedFieldsAsync();
		}

		public static bool GetIsElevated()
		{
			lock (_instanceLocker)
			{
				return _instance?._briefcase?.MetaBriefcase?.IsElevated == true;
			}
		}

		private static readonly object _isImportingExportingLocker = new object();
		public bool IsImportingSettings
		{
			get
			{
				lock (_isImportingExportingLocker)
				{
					string tf = RegistryAccess.GetValue(ConstantData.REG_SETTINGS_IS_IMPORTING);
					return tf == true.ToString();
				}
			}
			private set
			{
				lock (_isImportingExportingLocker)
				{
					RegistryAccess.TrySetValue(ConstantData.REG_SETTINGS_IS_IMPORTING, value.ToString());
					RaisePropertyChanged_UI();
				}
			}
		}
		private bool TrySetIsImportingSettings(bool newValue)
		{
			lock (_isImportingExportingLocker)
			{
				if (IsImportingSettings == newValue) return false;

				IsImportingSettings = newValue;
				return true;
			}
		}
		public bool IsExportingSettings
		{
			get
			{
				lock (_isImportingExportingLocker)
				{
					string tf = RegistryAccess.GetValue(ConstantData.REG_SETTINGS_IS_EXPORTING);
					return tf == true.ToString();
				}
			}
			private set
			{
				lock (_isImportingExportingLocker)
				{
					RegistryAccess.TrySetValue(ConstantData.REG_SETTINGS_IS_EXPORTING, value.ToString());
					RaisePropertyChanged_UI();
				}
			}
		}
		private bool TrySetIsExportingSettings(bool newValue)
		{
			lock (_isImportingExportingLocker)
			{
				if (IsExportingSettings == newValue) return false;

				IsExportingSettings = newValue;
				return true;
			}
		}

		private static readonly object _instanceLocker = new object();
		private static SettingsVM _instance = null;
		private readonly AnimationStarter _animationStarter = null;
		#endregion properties


		#region lifecycle
		public SettingsVM(Briefcase briefcase, AnimationStarter animationStarter)
		{
			lock (_instanceLocker)
			{
				_instance = this;

				_animationStarter = animationStarter;
				_backgroundTaskHelper = App.BackgroundTaskHelper;
				RaisePropertyChanged_UI(nameof(BackgroundTaskHelper));
				_briefcase = briefcase;
				RaisePropertyChanged_UI(nameof(Briefcase));
				//UpdateUnassignedFields();
			}
		}

		protected override async Task OpenMayOverrideAsync(object args = null)
		{
			await RunInUiThreadAsync(() =>
			{
				if (_assignedFields == null || _assignedFields.IsDisposed) AssignedFields = new SwitchableObservableDisposableCollection<FieldDescriptionPlus>();
				if (_unassignedFields == null || _unassignedFields.IsDisposed) UnassignedFields = new SwitchableObservableDisposableCollection<FieldDescriptionPlus>();
			}).ConfigureAwait(false);

			if (IsExportingSettings)
			{
				var file = await Pickers.GetLastPickedSaveFileAsync().ConfigureAwait(false);
				await ContinueAfterFileSavePickerAsync(file, Briefcase.GetCurrentInstance()).ConfigureAwait(false);
			}
			if (IsImportingSettings)
			{
				var file = await Pickers.GetLastPickedOpenFileAsync().ConfigureAwait(false);
				await ContinueAfterFileOpenPickerAsync(file, Briefcase.GetCurrentInstance()).ConfigureAwait(false);
			}
			var mbc = _briefcase?.MetaBriefcase;
			if (mbc != null) MetaBriefcaseRubbishBin.DataChanged += OnMetaDataChanged;
		}

		protected override async Task CloseMayOverrideAsync()
		{
			var mbc = _briefcase?.MetaBriefcase;
			if (mbc != null)
			{
				MetaBriefcaseRubbishBin.DataChanged -= OnMetaDataChanged;
				await mbc.SaveAsync().ConfigureAwait(false);
			}
			_unassignedFields?.Dispose();
		}

		private void OnMetaDataChanged(object sender, EventArgs e)
		{
			Refresh();
		}
		#endregion lifecycle


		#region user actions
		public async Task<bool> AddCategoryAsync()
		{
			var mbc = _briefcase?.MetaBriefcase;
			if (mbc == null) return false;
			return await RunFunctionIfOpenAsyncTB(mbc.AddNewCategoryAsync).ConfigureAwait(false);
		}

		public async Task<bool> RemoveCategoryAsync(Category cat)
		{
			var mbc = _briefcase?.MetaBriefcase;
			if (mbc == null) return false;

			return await RunFunctionIfOpenAsyncTB(() => mbc.RemoveCategoryAsync(cat)).ConfigureAwait(false);
		}

		public Task<bool> AddFieldDescriptionAsync()
		{
			return RunFunctionIfOpenAsyncTB(async delegate
			{
				var mbc = _briefcase?.MetaBriefcase;
				if (mbc == null) return false;

				if (await mbc.AddNewFieldDescriptionAsync())
				{
					await UpdateAssignedUnassignedFieldsAsync().ConfigureAwait(false);
					return true;
				}
				return false;
			});
		}

		public Task<bool> RemoveFieldDescriptionAsync(FieldDescription fldDesc)
		{
			return RunFunctionIfOpenAsyncTB(async delegate
			{
				var mbc = _briefcase?.MetaBriefcase;
				if (mbc == null) return false;

				if (await mbc.RemoveFieldDescriptionAsync(fldDesc))
				{
					await UpdateAssignedUnassignedFieldsAsync().ConfigureAwait(false);
					return true;
				}
				return false;
			});
		}
		public Task<bool> AssignFieldDescriptionToCurrentCategoryAsync(FieldDescription fldDsc)
		{
			return RunFunctionIfOpenAsyncTB(async delegate
			{
				var mbc = _briefcase?.MetaBriefcase;
				if (mbc == null) return false;

				if (await mbc.AssignFieldDescriptionToCurrentCategoryAsync(fldDsc))
				{
					await UpdateAssignedUnassignedFieldsAsync().ConfigureAwait(false);
					return true;
				}
				return false;
			});
		}

		public Task<bool> UnassignFieldDescriptionFromCurrentCategoryAsync(FieldDescription fldDsc)
		{
			return RunFunctionIfOpenAsyncTB(async delegate
			{
				var mbc = _briefcase?.MetaBriefcase;
				if (mbc == null) return false;

				if (await mbc.UnassignFieldDescriptionFromCurrentCategoryAsync(fldDsc))
				{
					await UpdateAssignedUnassignedFieldsAsync().ConfigureAwait(false);
					return true;
				}
				return false;
			});
		}

		public async Task<bool> AddPossibleValueToCurrentFieldDescriptionAsync()
		{
			var mbc = _briefcase?.MetaBriefcase;
			if (mbc == null) return false;

			return await RunFunctionIfOpenAsyncTB(mbc.AddNewPossibleValueToCurrentFieldDescriptionAsync).ConfigureAwait(false);
		}

		public async Task<bool> RemovePossibleValueFromCurrentFieldDescriptionAsync(FieldValue fldVal)
		{
			var mbc = _briefcase?.MetaBriefcase;
			if (mbc == null) return false;

			return await RunFunctionIfOpenAsyncTB(() => mbc.RemovePossibleValueFromCurrentFieldDescriptionAsync(fldVal)).ConfigureAwait(false);
		}
		public Task SetCurrentCategoryAsync(Category newItem)
		{
			return RunFunctionIfOpenAsyncT(async delegate
			{
				var mbc = _briefcase?.MetaBriefcase;
				if (mbc == null) return;

				await mbc.SetCurrentCategoryAsync(newItem);
				await UpdateAssignedUnassignedFieldsAsync().ConfigureAwait(false);
			});
		}
		public Task SetCurrentFieldDescriptionAsync(FieldDescription newItem)
		{
			return RunFunctionIfOpenAsyncT(async delegate
			{
				var mbc = _briefcase?.MetaBriefcase;
				if (mbc == null) return;

				await mbc.SetCurrentFieldDescriptionAsync(newItem);
			});
		}

		public async void StartExport()
		{
			if (!IsOpen) return;
			var bc = Briefcase.GetCurrentInstance();
			if (bc != null && TrySetIsExportingSettings(true))
			{
				var file = await Pickers.PickSaveFileAsync(new string[] { ConstantData.XML_EXTENSION }).ConfigureAwait(false);

				// LOLLO NOTE at this point, OnResuming() has just started, if the app was suspended. We cannot even know if we are open.
				// To avoid surprises, we try the following here under _isOpenSemaphore. If it does not run through, IsImportingSettings will stay true.
				// In OpenMayOverrideAsync, we check IsImportingSettings and, if true, we try again.
				// ContinueAfter...() sets IsImportingSettings to false, so there won't be redundant attempts.
				var isThrough = await RunFunctionIfOpenThreeStateAsyncT(delegate
				{
					Logger.Add_TPL("StartExportSettings(): _isOpen == true, about to call ContinueAfterFileSavePickerAsync()", Logger.AppEventsLogFilename, Logger.Severity.Info, false);
					return ContinueAfterFileSavePickerAsync(file, bc);
				}).ConfigureAwait(false);
			}
			else
			{
				_animationStarter.EndAllAnimations();
				_animationStarter.StartAnimation(AnimationStarter.Animations.Failure);
			}
		}

		private async Task ContinueAfterFileSavePickerAsync(StorageFile file, Briefcase bc)
		{
			bool isExported = false;

			try
			{
				if (bc != null && file != null)
				{
					_animationStarter.StartAnimation(AnimationStarter.Animations.Updating);
					isExported = await bc.ExportSettingsAsync(file).ConfigureAwait(false);
				}
			}
			catch (Exception ex)
			{
				await Logger.AddAsync(ex.ToString(), Logger.AppEventsLogFilename).ConfigureAwait(false);
			}

			_animationStarter.EndAllAnimations();
			_animationStarter.StartAnimation(isExported
				? AnimationStarter.Animations.Success
				: AnimationStarter.Animations.Failure);

			IsExportingSettings = false;
		}

		public async void StartImport()
		{
			if (!IsOpen) return;
			var bc = Briefcase.GetCurrentInstance();
			if (bc != null && TrySetIsImportingSettings(true))
			{
				var file = await Pickers.PickOpenFileAsync(new[] { ConstantData.XML_EXTENSION }).ConfigureAwait(false);

				// LOLLO NOTE at this point, OnResuming() has just started, if the app was suspended. We cannot even know if we are open.
				// To avoid surprises, we try the following here under _isOpenSemaphore. If it does not run through, IsImportingSettings will stay true.
				// In OpenMayOverrideAsync, we check IsImportingSettings and, if true, we try again.
				// ContinueAfter...() sets IsImportingSettings to false, so there won't be redundant attempts.
				var isThrough = await RunFunctionIfOpenThreeStateAsyncT(delegate
				{
					Logger.Add_TPL("StartImportSettings(): _isOpen == true, about to call ContinueAfterFileOpenPickerAsync()", Logger.AppEventsLogFilename, Logger.Severity.Info, false);
					return ContinueAfterFileOpenPickerAsync(file, bc);
				}).ConfigureAwait(false);
			}
			else
			{
				_animationStarter.EndAllAnimations();
				_animationStarter.StartAnimation(AnimationStarter.Animations.Failure);
			}
		}

		private async Task ContinueAfterFileOpenPickerAsync(StorageFile file, Briefcase bc)
		{
			bool isImported = false;

			try
			{
				if (bc != null && file != null)
				{
					_animationStarter.StartAnimation(AnimationStarter.Animations.Updating);
					isImported = await bc.ImportSettingsAsync(file).ConfigureAwait(false);
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
				Refresh();
			}
			else
			{
				_animationStarter.StartAnimation(AnimationStarter.Animations.Failure);
			}

			IsImportingSettings = false;
		}

		public Task RetrySyncFromOneDriveAsync()
		{
			return RunFunctionIfOpenAsyncT(() =>
			{
				var bc = _briefcase;
				if (bc == null || !bc.IsWantToUseOneDrive) return Task.CompletedTask;

				return SetIsWantToUseOneDrive2Async(true, true);
			});
		}
		public Task SetIsWantToUseOneDriveAsync(bool newValue)
		{
			return RunFunctionIfOpenAsyncT(() =>
			{
				var bc = _briefcase;
				if (bc == null) return Task.CompletedTask;

				return SetIsWantToUseOneDrive2Async(newValue, false);
			});
		}
		private async Task SetIsWantToUseOneDrive2Async(bool newIsWantToUseOneDrive, bool force)
		{
			var bc = _briefcase;
			if (bc == null) return;

			bool isLoadFromOneDriveThisOneTime = bc.IsWantToUseOneDrive;

			if (newIsWantToUseOneDrive)
			{
				var loadFromOneDriveThisOneTime_dialogueResponse = await UserConfirmationPopup.GetInstance().GetUserChoiceBeforeChangingMetadataSourceAsync(CancToken).ConfigureAwait(false);
				if (CancToken.IsCancellationRequested) return;
				isLoadFromOneDriveThisOneTime = !loadFromOneDriveThisOneTime_dialogueResponse.Item2 || !loadFromOneDriveThisOneTime_dialogueResponse.Item1;
			}

			_animationStarter.StartAnimation(AnimationStarter.Animations.Updating);
			await bc.SetIsWantToUseOneDriveAsync(newIsWantToUseOneDrive, isLoadFromOneDriveThisOneTime, force);
			_animationStarter.EndAllAnimations();
			_animationStarter.StartAnimation(bc.IsWantAndCannotUseOneDrive ? AnimationStarter.Animations.Failure : AnimationStarter.Animations.Success);
		}
		#endregion user actions
	}

	public class BooleanAndElevated : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, string language)
		{
			if (!(value is bool)) return false;
			bool output = (bool)value || SettingsVM.GetIsElevated();
			return output;
		}
		public object ConvertBack(object value, Type targetType, object parameter, string language)
		{
			throw new Exception("this is a one-way binding, it should never come here");
		}
	}
	public class BooleanAndElevatedToVisible : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, string language)
		{
			if (!(value is bool)) return Visibility.Collapsed;
			bool output = (bool)value || SettingsVM.GetIsElevated();
			return output ? Visibility.Visible : Visibility.Collapsed;
		}
		public object ConvertBack(object value, Type targetType, object parameter, string language)
		{
			throw new Exception("this is a one-way binding, it should never come here");
		}
	}

	public class PermissionLevelsToVisible : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, string language)
		{
			if (!(value is SettingsVM.FieldDescriptionPlus.PermissionLevels)) return Visibility.Collapsed;
			var output = (SettingsVM.FieldDescriptionPlus.PermissionLevels)value;
			return output == SettingsVM.FieldDescriptionPlus.PermissionLevels.WithCaution || output == SettingsVM.FieldDescriptionPlus.PermissionLevels.Yes ? Visibility.Visible : Visibility.Collapsed;
		}
		public object ConvertBack(object value, Type targetType, object parameter, string language)
		{
			throw new Exception("this is a one-way binding, it should never come here");
		}
	}

	public class PermissionLevelsToColor : IValueConverter
	{
		private static SolidColorBrush _default = (SolidColorBrush)Application.Current.Resources["SystemControlForegroundBaseHighBrush"];
		private static SolidColorBrush _flashy = (SolidColorBrush)Application.Current.Resources["FlashyForeground"];
		public object Convert(object value, Type targetType, object parameter, string language)
		{
			if (!(value is SettingsVM.FieldDescriptionPlus.PermissionLevels)) return _default;
			var output = (SettingsVM.FieldDescriptionPlus.PermissionLevels)value;
			return output == SettingsVM.FieldDescriptionPlus.PermissionLevels.WithCaution ? _flashy : _default;
		}
		public object ConvertBack(object value, Type targetType, object parameter, string language)
		{
			throw new Exception("this is a one-way binding, it should never come here");
		}
	}
}