﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Resources;
using System.Text;
using System.Threading.Tasks;
using UniFiler10.Controlz;
using UniFiler10.Data.Constants;
using UniFiler10.Data.Metadata;
using UniFiler10.Data.Model;
using Utilz;
using Utilz.Data;
using Windows.ApplicationModel.Resources.Core;
using Windows.Storage;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Data;

namespace UniFiler10.ViewModels
{
	public sealed class SettingsVM : OpenableObservableDisposableData
	{
		#region properties
		private readonly MetaBriefcase _metaBriefcase = null;
		public MetaBriefcase MetaBriefcase { get { return _metaBriefcase; } /*set { _metaBriefcase = value; RaisePropertyChanged_UI(); }*/ }

		private SwitchableObservableDisposableCollection<FieldDescription> _unassignedFields = null; // new SwitchableObservableDisposableCollection<FieldDescription>();
		public SwitchableObservableDisposableCollection<FieldDescription> UnassignedFields { get { return _unassignedFields; } private set { _unassignedFields = value; RaisePropertyChanged_UI(); } }
		private void UpdateUnassignedFields()
		{
			var mb = _metaBriefcase;
			var unaFlds = _unassignedFields;
			if (unaFlds != null)
			{
				unaFlds.Clear();
				if (mb != null && mb.FieldDescriptions != null && mb.CurrentCategory != null && mb.CurrentCategory.FieldDescriptionIds != null)
				{
					_unassignedFields.AddRange(mb.FieldDescriptions
						.Where(allFldDsc => !mb.CurrentCategory.FieldDescriptions.Any(catFldDsc => catFldDsc.Id == allFldDsc.Id)));
					RaisePropertyChanged_UI(nameof(UnassignedFields));
				}
			}
		}
		public void OnDataContextChanged()
		{
			UpdateUnassignedFields();
		}

		public static bool GetIsElevated()
		{
			lock (_instanceLocker)
			{
				return _instance?._metaBriefcase?.IsElevated == true;
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
				if (IsImportingSettings != newValue)
				{
					IsImportingSettings = newValue;
					return true;
				}
				return false;
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
				if (IsExportingSettings != newValue)
				{
					IsExportingSettings = newValue;
					return true;
				}
				return false;
			}
		}

		private static readonly object _instanceLocker = new object();
		private static SettingsVM _instance = null;
		private AnimationStarter _animationStarter = null;
		#endregion properties


		#region lifecycle
		public SettingsVM(MetaBriefcase metaBriefcase, AnimationStarter animationStarter)
		{
			lock (_instanceLocker)
			{
				_metaBriefcase = metaBriefcase;
				RaisePropertyChanged_UI(nameof(MetaBriefcase));
				_instance = this;
				_animationStarter = animationStarter;
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
			var mbc = _metaBriefcase;
			if (mbc != null) await mbc.SaveACopyAsync().ConfigureAwait(false);

			_unassignedFields?.Dispose();
		}
		#endregion lifecycle


		#region user actions
		public Task<bool> AddCategoryAsync()
		{
			return _metaBriefcase?.AddCategoryAsync();
		}

		public Task<bool> RemoveCategoryAsync(Category cat)
		{
			return _metaBriefcase?.RemoveCategoryAsync(cat);
		}

		public async Task<bool> AddFieldDescriptionAsync()
		{
			var mbf = _metaBriefcase;
			if (mbf != null)
			{
				if (await mbf.AddFieldDescriptionAsync())
				{
					UpdateUnassignedFields();
					return true;
				}
			}
			return false;
		}

		public async Task<bool> RemoveFieldDescriptionAsync(FieldDescription fldDesc)
		{
			var mbf = _metaBriefcase;
			if (mbf != null)
			{
				if (await mbf.RemoveFieldDescriptionAsync(fldDesc))
				{
					UpdateUnassignedFields();
					return true;
				}
			}
			return false;
		}
		public async Task<bool> AssignFieldDescriptionToCurrentCategoryAsync(FieldDescription fldDsc)
		{
			var mb = _metaBriefcase;
			if (mb == null) return false;

			if (await mb.AssignFieldDescriptionToCurrentCategoryAsync(fldDsc))
			{
				UpdateUnassignedFields();
				return true;
			}
			return false;
		}

		public async Task<bool> UnassignFieldDescriptionFromCurrentCategoryAsync(FieldDescription fldDsc)
		{
			var mb = _metaBriefcase;
			if (mb == null) return false;

			if (await mb.UnassignFieldDescriptionFromCurrentCategoryAsync(fldDsc))
			{
				UpdateUnassignedFields();
				return true;
			}
			return false;
		}

		public Task<bool> AddPossibleValueToCurrentFieldDescriptionAsync()
		{
			return _metaBriefcase?.AddPossibleValueToCurrentFieldDescriptionAsync();
		}

		public Task<bool> RemovePossibleValueFromCurrentFieldDescriptionAsync(FieldValue fldVal)
		{
			return _metaBriefcase.RemovePossibleValueFromCurrentFieldDescriptionAsync(fldVal);
		}
		public async Task SetCurrentCategoryAsync(Category newItem)
		{
			var mb = _metaBriefcase;
			if (mb != null)
			{
				await mb.SetCurrentCategoryAsync(newItem);
				UpdateUnassignedFields();
			}
		}
		public async Task SetCurrentFieldDescriptionAsync(FieldDescription newItem)
		{
			var mb = _metaBriefcase;
			if (mb != null)
			{
				await mb.SetCurrentFieldDescriptionAsync(newItem);
			}
		}

		public async void StartExport()
		{
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
			if (isExported) _animationStarter.StartAnimation(AnimationStarter.Animations.Success);
			else _animationStarter.StartAnimation(AnimationStarter.Animations.Failure);

			IsExportingSettings = false;
		}

		public async void StartImport()
		{
			var bc = Briefcase.GetCurrentInstance();
			if (bc != null && TrySetIsImportingSettings(true))
			{
				var file = await Pickers.PickOpenFileAsync(new string[] { ConstantData.XML_EXTENSION }).ConfigureAwait(false);

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
		#endregion user actions
	}

	public class BooleanAndElevated : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, string language)
		{
			if (value == null || !(value is bool)) return false;
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
			if (value == null || !(value is bool)) return Visibility.Collapsed;
			bool output = (bool)value || SettingsVM.GetIsElevated();
			if (output) return Visibility.Visible;
			else return Visibility.Collapsed;
		}
		public object ConvertBack(object value, Type targetType, object parameter, string language)
		{
			throw new Exception("this is a one-way binding, it should never come here");
		}
	}
}