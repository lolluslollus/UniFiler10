using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Resources;
using System.Text;
using System.Threading.Tasks;
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
		private const string REG_IMPORT_FILEPATH = "SettingsFilePicker.ImportFilePath";
		private const string REG_EXPORT_FILEPATH = "SettingsFilePicker.ExportFilePath";

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

		private bool _isCanImportExport = true;
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
			lock (_importExportLock) { IsCanImportExport = !IsImportingExportingSaysTheRegistry(); }
			if (ImportExportMetadataEnded == null) ImportExportMetadataEnded += OnImportExportMetadataEnded;
			await ResumeAfterFileOpenPickAsync().ConfigureAwait(false);
			await ResumeAfterFileSavePickAsync().ConfigureAwait(false);
		}

		private async void OnImportExportMetadataEnded(object sender, EventArgs e)
		{
			await ResumeAfterFileOpenPickAsync().ConfigureAwait(false);
			await ResumeAfterFileSavePickAsync().ConfigureAwait(false);
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
			Task exp = RunFunctionWhileOpenAsyncA(delegate
			{
				lock (_importExportLock)
				{
					if (!_isCanImportExport || IsImportingExportingSaysTheRegistry())
					{
						IsCanImportExport = false;
						return;
					}
					else IsCanImportExport = false;
				}

				var bf = Briefcase.GetCurrentInstance();
				if (bf != null)
				{
					var pickTask = Pickers.PickSaveFileAsync(new string[] { ConstantData.XML_EXTENSION });
					var afterFilePickedTask = pickTask.ContinueWith(delegate
					{
						return AfterFileSavePickedTask(pickTask, true);
					});
				}
			});

			//bool isOk = false;
			//var file = await Pickers.PickSaveFileAsync(new string[] { ConstantData.XML_EXTENSION }).ConfigureAwait(false);
			//if (file != null)
			//{
			//	var bf = Briefcase.GetCurrentInstance();
			//	if (bf != null)
			//	{
			//		isOk = await bf.ExportSettingsAsync(file).ConfigureAwait(false);
			//	}
			//}

			//if (isOk) _animationStarter.StartAnimation(0);
			//else _animationStarter.StartAnimation(1);

			//return isOk;
		}

		private async Task AfterFileSavePickedTask(Task<StorageFile> pickTask, bool copyFile)
		{
			try
			{
				var file = await pickTask.ConfigureAwait(false);
				if (file == null)
				{
					// User cancelled picking
					_animationStarter.StartAnimation(1);
				}
				else
				{
					bool isSaved = false;
					var bf = Briefcase.GetCurrentInstance();
					if (bf != null)
					{
						isSaved = await bf.ExportSettingsAsync(file).ConfigureAwait(false);
					}

					if (isSaved)
					{
						RegistryAccess.SetValue(REG_EXPORT_FILEPATH, string.Empty);
					}
					else // could not complete the operation: write away the relevant values, Resume() will follow up.
						 // this happens with low memory devices, that suspend the app when opening a picker or the camera ui.
					{
						RegistryAccess.SetValue(REG_EXPORT_FILEPATH, file.Path);
						ImportExportMetadataEnded?.Invoke(this, EventArgs.Empty);
					}

					if (isSaved)
					{
						_animationStarter.StartAnimation(0);
					}
					else
					{
						_animationStarter.StartAnimation(1);
					}
				}
			}
			catch (Exception ex)
			{
				await Logger.AddAsync(ex?.ToString(), Logger.FileErrorLogFilename).ConfigureAwait(false);
			}
			lock (_importExportLock) { IsCanImportExport = !IsImportingExportingSaysTheRegistry(); }
		}

		private async Task ResumeAfterFileSavePickAsync()
		{
			string filePath = RegistryAccess.GetValue(REG_EXPORT_FILEPATH);
			bool wasPicking = !string.IsNullOrWhiteSpace(filePath);

			if (wasPicking)
			{
				var pickFileTask = StorageFile.GetFileFromPathAsync(filePath).AsTask();
				await AfterFileSavePickedTask(pickFileTask, false).ConfigureAwait(false);
			}
			else
			{
				await Logger.AddAsync("SettingsVM opened, was NOT picking before", Logger.FileErrorLogFilename, Logger.Severity.Info).ConfigureAwait(false);
			}
		}

		public void StartImport()
		{
			Task imp = RunFunctionWhileOpenAsyncA(delegate
			{
				lock (_importExportLock)
				{
					if (!_isCanImportExport || IsImportingExportingSaysTheRegistry())
					{
						IsCanImportExport = false;
						return;
					}
					else IsCanImportExport = false;
				}

				var bf = Briefcase.GetCurrentInstance();
				if (bf != null)
				{
					var pickTask = Pickers.PickOpenFileAsync(new string[] { ConstantData.XML_EXTENSION });
					var afterFilePickedTask = pickTask.ContinueWith(delegate
					{ // LOLLO TODO the manifest contains .xml, but as soon as I launch this, the hiking mate is started, automatically. Why?
					  // LOLLO TODO I cannot put text/xml and application/xml into the manifest, why?
						return AfterFileOpenPickedTask(pickTask, true);
					});
				}
			});

			//bool isOk = false;
			//var file = await Pickers.PickOpenFileAsync(new string[] { ConstantData.XML_EXTENSION }).ConfigureAwait(false);
			//if (file != null)
			//{
			//	var bf = Briefcase.GetCurrentInstance();
			//	if (bf != null)
			//	{
			//		isOk = await bf.ImportSettingsAsync(file).ConfigureAwait(false);
			//	}
			//}

			//if (isOk) _animationStarter.StartAnimation(0);
			//else _animationStarter.StartAnimation(1);

			//return isOk;
		}

		private async Task AfterFileOpenPickedTask(Task<StorageFile> pickTask, bool copyFile)
		{
			try
			{
				var file = await pickTask.ConfigureAwait(false);
				if (file == null)
				{
					// User cancelled picking
					_animationStarter.StartAnimation(1);
				}
				else
				{
					StorageFile newFile = null;
					if (copyFile)
					{
						newFile = await file.CopyAsync(ApplicationData.Current.TemporaryFolder, file.Name, NameCollisionOption.GenerateUniqueName).AsTask().ConfigureAwait(false); // copy right after the picker or access will be forbidden later
					}
					else
					{
						newFile = file;
					}

					bool isSaved = false;
					if (newFile != null)
					{
						var bf = Briefcase.GetCurrentInstance();
						if (bf != null)
						{
							isSaved = await bf.ImportSettingsAsync(newFile).ConfigureAwait(false);
						}

						if (isSaved)
						{
							RegistryAccess.SetValue(REG_IMPORT_FILEPATH, string.Empty);
							MetadataChanged?.Invoke(this, EventArgs.Empty);
						}
						else // could not complete the operation: write away the relevant values, Resume() will follow up.
							 // this happens with low memory devices, that suspend the app when opening a picker or the camera ui.
						{
							RegistryAccess.SetValue(REG_IMPORT_FILEPATH, newFile.Path);
							ImportExportMetadataEnded?.Invoke(this, EventArgs.Empty);
						}
					}

					if (isSaved)
					{
						_animationStarter.StartAnimation(0);
					}
					else
					{
						_animationStarter.StartAnimation(1);
					}
				}
			}
			catch (Exception ex)
			{
				await Logger.AddAsync(ex?.ToString(), Logger.FileErrorLogFilename).ConfigureAwait(false);
			}
			lock (_importExportLock) { IsCanImportExport = !IsImportingExportingSaysTheRegistry(); }
		}

		private async Task ResumeAfterFileOpenPickAsync()
		{
			string filePath = RegistryAccess.GetValue(REG_IMPORT_FILEPATH);
			bool wasPicking = !string.IsNullOrWhiteSpace(filePath);

			if (wasPicking)
			{
				var pickFileTask = StorageFile.GetFileFromPathAsync(filePath).AsTask();
				await AfterFileOpenPickedTask(pickFileTask, false).ConfigureAwait(false);
			}
			else
			{
				await Logger.AddAsync("SettingsVM opened, was NOT picking before", Logger.FileErrorLogFilename, Logger.Severity.Info).ConfigureAwait(false);
			}
		}

		private static event EventHandler ImportExportMetadataEnded;
		public event EventHandler MetadataChanged;

		private bool IsImportingExportingSaysTheRegistry()
		{
			string a = RegistryAccess.GetValue(REG_IMPORT_FILEPATH);
			string b = RegistryAccess.GetValue(REG_EXPORT_FILEPATH);
			return !(string.IsNullOrWhiteSpace(a) && string.IsNullOrWhiteSpace(b));
		}

		private readonly object _importExportLock = new object();
		//private bool _isImporting = false;
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
