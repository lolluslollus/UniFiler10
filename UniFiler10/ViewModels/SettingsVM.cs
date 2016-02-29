using System;
using System.Linq;
using System.Threading.Tasks;
using UniFiler10.Controlz;
using UniFiler10.Data.Constants;
using UniFiler10.Data.Metadata;
using UniFiler10.Data.Model;
using UniFiler10.Data.Runtime;
using UniFiler10.Services;
using Utilz;
using Utilz.Data;
using Windows.Storage;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Data;

namespace UniFiler10.ViewModels
{
	public sealed class SettingsVM : OpenableObservableDisposableData
	{
		#region properties
		private readonly BackgroundTaskHelper _backgroundTaskHelper = null;
		public BackgroundTaskHelper BackgroundTaskHelper { get { return _backgroundTaskHelper; } }

		private readonly Briefcase _briefcase = null;
		public Briefcase Briefcase { get { return _briefcase; } }

		private SwitchableObservableDisposableCollection<FieldDescription> _unassignedFields = null;
		public SwitchableObservableDisposableCollection<FieldDescription> UnassignedFields { get { return _unassignedFields; } private set { _unassignedFields = value; RaisePropertyChanged_UI(); } }
		private void UpdateUnassignedFields()
		{
			var mbc = _briefcase.MetaBriefcase;
			var unaFlds = _unassignedFields;
			if (unaFlds == null) return;

			unaFlds.Clear();
			if (mbc?.FieldDescriptions == null || mbc.CurrentCategory?.FieldDescriptionIds == null) return;

			_unassignedFields.AddRange(mbc.FieldDescriptions
				.Where(allFldDsc => mbc.CurrentCategory.FieldDescriptions.All(catFldDsc => catFldDsc.Id != allFldDsc.Id)));
			//				_unassignedFields.AddRange(mbc.FieldDescriptions
			//					.Where(allFldDsc => !mbc.CurrentCategory.FieldDescriptions.Any(catFldDsc => catFldDsc.Id == allFldDsc.Id)));
			RaisePropertyChanged_UI(nameof(UnassignedFields));
		}
		public void OnDataContextChanged()
		{
			UpdateUnassignedFields();
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
				UpdateUnassignedFields();
			}
		}

		protected override async Task OpenMayOverrideAsync()
		{
			await RunInUiThreadAsync(delegate
			{
				UnassignedFields = new SwitchableObservableDisposableCollection<FieldDescription>();
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
		}

		protected override async Task CloseMayOverrideAsync()
		{
			var mbc = _briefcase?.MetaBriefcase;
			if (mbc != null) await mbc.SaveAsync().ConfigureAwait(false);

			_unassignedFields?.Dispose();
		}
		#endregion lifecycle


		#region user actions
		public async Task<bool> AddCategoryAsync()
		{
			var mbc = _briefcase?.MetaBriefcase;
			if (mbc == null) return false;
			return await RunFunctionIfOpenAsyncTB(mbc.AddCategoryAsync).ConfigureAwait(false);
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

				if (await mbc.AddFieldDescriptionAsync())
				{
					UpdateUnassignedFields();
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
					UpdateUnassignedFields();
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
					UpdateUnassignedFields();
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
					UpdateUnassignedFields();
					return true;
				}
				return false;
			});
		}

		public async Task<bool> AddPossibleValueToCurrentFieldDescriptionAsync()
		{
			var mbc = _briefcase?.MetaBriefcase;
			if (mbc == null) return false;

			return await RunFunctionIfOpenAsyncTB(mbc.AddPossibleValueToCurrentFieldDescriptionAsync).ConfigureAwait(false);
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
				UpdateUnassignedFields();
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
				MetadataChanged?.Invoke(this, EventArgs.Empty);
			}
			else
			{
				_animationStarter.StartAnimation(AnimationStarter.Animations.Failure);
			}

			IsImportingSettings = false;
		}

		public event EventHandler MetadataChanged;

		public Task RetrySyncFromOneDriveAsync()
		{
			var bc = _briefcase;
			if (bc == null || !bc.IsWantAndCannotUseOneDrive) return Task.CompletedTask;

			return SetIsWantToUseOneDriveAsync(true);
		}
		public async Task SetIsWantToUseOneDriveAsync(bool newValue)
		{
			var bc = _briefcase;
			if (bc == null) return;

			_animationStarter.StartAnimation(AnimationStarter.Animations.Updating);
			await bc.SetIsWantToUseOneDriveAsync(newValue);
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
}