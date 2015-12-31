using System;
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
using Windows.ApplicationModel.Resources.Core;
using Windows.Storage;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Data;

namespace UniFiler10.ViewModels
{
	public sealed class SettingsVM : OpenableObservableData, IDisposable
	{
		#region properties
		private MetaBriefcase _metaBriefcase = null;
		public MetaBriefcase MetaBriefcase { get { return _metaBriefcase; } set { _metaBriefcase = value; RaisePropertyChanged_UI(); } }

		private SwitchableObservableCollection<FieldDescription> _unassignedFields = new SwitchableObservableCollection<FieldDescription>();
		public SwitchableObservableCollection<FieldDescription> UnassignedFields { get { return _unassignedFields; } }
		private void UpdateUnassignedFields()
		{
			var mb = _metaBriefcase;
			_unassignedFields.Clear();
			if (mb != null && mb.FieldDescriptions != null && mb.CurrentCategory != null && mb.CurrentCategory.FieldDescriptionIds != null)
			{
				_unassignedFields.AddRange(mb.FieldDescriptions
					.Where(allFldDsc => !mb.CurrentCategory.FieldDescriptions.Any(catFldDsc => catFldDsc.Id == allFldDsc.Id)));
				RaisePropertyChanged_UI(nameof(UnassignedFields));
			}
		}
		public void OnDataContextChanged()
		{
			UpdateUnassignedFields();
		}

		public static bool GetIsElevated()
		{
			return _instance?._metaBriefcase?.IsElevated == true;
		}

		private volatile bool _isCanImportExport = true;
		public bool IsCanImportExport { get { return _isCanImportExport; } private set { _isCanImportExport = value; RaisePropertyChanged_UI(); } }

		private static SettingsVM _instance = null;
		private AnimationStarter _animationStarter = null;
		#endregion properties


		#region ctor and dispose
		public SettingsVM(MetaBriefcase metaBriefcase, AnimationStarter animationStarter)
		{
			MetaBriefcase = metaBriefcase;
			_instance = this;
			_animationStarter = animationStarter;
			UpdateUnassignedFields();
		}

		protected override void Dispose(bool isDisposing)
		{
			base.Dispose(isDisposing);

			// we don't touch MetaBriefcase or other data, only app.xaml.cs may do so.
			_unassignedFields?.Clear();
			_unassignedFields?.Dispose();
			_unassignedFields = null;
		}
		#endregion ctor and dispose


		#region open and close
		protected override async Task OpenMayOverrideAsync()
		{
			if (ImportExportMetadataEnded == null) ImportExportMetadataEnded += OnImportExportMetadataEnded;
			await ResumeAfterImportSettingsAsync().ConfigureAwait(false);
			await ResumeAfterExportSettingsAsync().ConfigureAwait(false);
		}

		private async void OnImportExportMetadataEnded(object sender, EventArgs e)
		{
			await ResumeAfterImportSettingsAsync().ConfigureAwait(false);
			await ResumeAfterExportSettingsAsync().ConfigureAwait(false);
		}

		protected override async Task CloseMayOverrideAsync()
		{
			ImportExportMetadataEnded -= OnImportExportMetadataEnded;
			var mbc = _metaBriefcase;
			if (mbc != null) await mbc.SaveACopyAsync().ConfigureAwait(false);
		}
		#endregion open and close


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

		public void StartExport()
		{
			var bc = Briefcase.GetCurrentInstance();
			if (bc != null && _isCanImportExport)
			{
				IsCanImportExport = false;
				//RegistryAccess.SetValue(ConstantData.REG_IMPEXP_SETTINGS_IS_EXPORTING, true.ToString());
				var pickTask = Pickers.PickSaveFileAsync(new string[] { ConstantData.XML_EXTENSION });
				var afterFilePickedTask = pickTask.ContinueWith(delegate
				{
					return ContinueAfterFileSavePickerAsync(pickTask, bc);
				});
			}
			else
			{
				_animationStarter.EndAllAnimations();
				_animationStarter.StartAnimation(AnimationStarter.Animations.Failure);
			}
		}

		private async Task ContinueAfterFileSavePickerAsync(Task<StorageFile> pickTask, Briefcase bc)
		{
			bool isExported = false;
			bool isNeedsContinuing = false;
			try
			{
				if (bc != null)
				{
					var file = await pickTask.ConfigureAwait(false);
					if (file != null)
					{
						_animationStarter.StartAnimation(AnimationStarter.Animations.Updating);
						isExported = await bc.ExportSettingsAsync(file).ConfigureAwait(false);
						if (!isExported && !bc.IsOpen) isNeedsContinuing = true; // LOLLO if isOk is false because there was an error and not because the app was suspended, I must unlock impexp.
					}
				}
			}
			catch (Exception ex)
			{
				await Logger.AddAsync(ex.ToString(), Logger.FileErrorLogFilename).ConfigureAwait(false);
			}

			_animationStarter.EndAllAnimations();
			if (isExported)
			{
				_animationStarter.StartAnimation(AnimationStarter.Animations.Success);
			}
			if (isNeedsContinuing)
			{
				RegistryAccess.SetValue(ConstantData.REG_IMPEXP_SETTINGS_CONTINUE_EXPORTING, true.ToString());
			}
			else
			{
				RegistryAccess.SetValue(ConstantData.REG_IMPEXP_SETTINGS_CONTINUE_EXPORTING, false.ToString());
				if (!isExported) _animationStarter.StartAnimation(AnimationStarter.Animations.Failure);
				//RegistryAccess.SetValue(ConstantData.REG_IMPEXP_SETTINGS_IS_EXPORTING, false.ToString());
				IsCanImportExport = true;
			}

			ImportExportMetadataEnded?.Invoke(this, EventArgs.Empty);
		}

		private async Task ResumeAfterExportSettingsAsync()
		{
			//string isExporting = RegistryAccess.GetValue(ConstantData.REG_IMPEXP_SETTINGS_IS_EXPORTING);
			//if (isExporting == true.ToString())
			//{
			//	IsCanImportExport = false;
			//	_animationStarter.StartAnimation(AnimationStarter.Animations.Updating);
			string continueExporting = RegistryAccess.GetValue(ConstantData.REG_IMPEXP_SETTINGS_CONTINUE_EXPORTING);
			if (continueExporting == true.ToString())
			{
				IsCanImportExport = false;
				_animationStarter.StartAnimation(AnimationStarter.Animations.Updating);
				await ContinueAfterFileSavePickerAsync(Pickers.GetLastPickedSaveFileJustOnceAsync(), Briefcase.GetCurrentInstance()).ConfigureAwait(false);
			}
			//}
			else
			{
				IsCanImportExport = true;
			}
		}

		private async Task ResumeAfterImportSettingsAsync()
		{
			//string isImporting = RegistryAccess.GetValue(ConstantData.REG_IMPEXP_SETTINGS_IS_IMPORTING);
			//if (isImporting == true.ToString())
			//{
			//	IsCanImportExport = false;
			//	_animationStarter.StartAnimation(AnimationStarter.Animations.Updating);
			string continueImporting = RegistryAccess.GetValue(ConstantData.REG_IMPEXP_SETTINGS_CONTINUE_IMPORTING);
			if (continueImporting == true.ToString())
			{
				IsCanImportExport = false;
				_animationStarter.StartAnimation(AnimationStarter.Animations.Updating);
				await ContinueAfterFileOpenPickerAsync(Pickers.GetLastPickedOpenFileJustOnceAsync(), Briefcase.GetCurrentInstance()).ConfigureAwait(false);
			}
			//}
			else
			{
				IsCanImportExport = true;
			}
		}

		public void StartImport()
		{
			var bc = Briefcase.GetCurrentInstance();
			if (bc != null && _isCanImportExport)
			{
				IsCanImportExport = false;
				// RegistryAccess.SetValue(ConstantData.REG_IMPEXP_SETTINGS_IS_IMPORTING, true.ToString());
				var pickTask = Pickers.PickOpenFileAsync(new string[] { ConstantData.XML_EXTENSION });
				var afterPickTask = pickTask.ContinueWith(delegate
				{ // LOLLO TODO the manifest contains .xml, but as soon as I launch this, the hiking mate is started, automatically. Why? Because the hiking mate registered a file open picker, which is wrong.
				  // LOLLO TODO I cannot put text/xml and application/xml into the manifest, why?
					return ContinueAfterFileOpenPickerAsync(pickTask, bc);
				});
			}
			else
			{
				_animationStarter.EndAllAnimations();
				_animationStarter.StartAnimation(AnimationStarter.Animations.Failure);
			}
		}

		private async Task ContinueAfterFileOpenPickerAsync(Task<StorageFile> pickTask, Briefcase bc)
		{
			bool isImported = false;
			bool isNeedsContinuing = false;
			try
			{
				if (bc != null)
				{
					var file = await pickTask.ConfigureAwait(false);
					if (file != null)
					{
						_animationStarter.StartAnimation(AnimationStarter.Animations.Updating);
						isImported = await bc.ImportSettingsAsync(file).ConfigureAwait(false);
						if (!isImported && !bc.IsOpen) isNeedsContinuing = true; // LOLLO if isOk is false because there was an error and not because the app was suspended, I must unlock impexp.
					}
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
				MetadataChanged?.Invoke(this, EventArgs.Empty);
			}
			if (isNeedsContinuing)
			{
				RegistryAccess.SetValue(ConstantData.REG_IMPEXP_SETTINGS_CONTINUE_IMPORTING, true.ToString());
				//RegistryAccess.SetValue(ConstantData.REG_IMPEXP_SETTINGS_IS_IMPORTING, true.ToString());
			}
			else
			{
				RegistryAccess.SetValue(ConstantData.REG_IMPEXP_SETTINGS_CONTINUE_IMPORTING, false.ToString());
				if (!isImported) _animationStarter.StartAnimation(AnimationStarter.Animations.Failure);
				//RegistryAccess.SetValue(ConstantData.REG_IMPEXP_SETTINGS_IS_IMPORTING, false.ToString());
				IsCanImportExport = true;
			}

			ImportExportMetadataEnded?.Invoke(this, EventArgs.Empty);
		}

		private static event EventHandler ImportExportMetadataEnded;
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
