using System;
using System.Collections.Generic;
using System.Linq;
using System.Resources;
using System.Text;
using System.Threading.Tasks;
using UniFiler10.Data.Metadata;
using UniFiler10.Data.Model;
using UniFiler10.Utilz;
using Utilz;
using Windows.ApplicationModel.Resources.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Data;

namespace UniFiler10.ViewModels
{
	public sealed class SettingsVM : ObservableData, IDisposable
	{
		private MetaBriefcase _metaBriefcase = null;
		public MetaBriefcase MetaBriefcase { get { return _metaBriefcase; } set { _metaBriefcase = value; RaisePropertyChanged_UI(); } }

		public static bool GetIsElevated()
		{
			return _instance?._metaBriefcase?.IsElevated == true;
		}

		private static SettingsVM _instance = null;
		public SettingsVM(MetaBriefcase metaBriefcase)
		{
			MetaBriefcase = metaBriefcase;
			_instance = this;
			UpdateUnassignedFields();
		}

		public void Dispose()
		{
			// we don't touch MetaBriefcase or other data, only app.xaml.cs may do so.
			_unassignedFields?.Dispose();
			_unassignedFields = null;
		}

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

		//     public bool AssignFieldDescriptionToCurrentCategory(FieldDescription fldDsc, Category toCat)
		//     {
		//var mb = _metaBriefcase;
		//if (mb == null || fldDsc == null || toCat == null) return false;

		//         var cat = mb.Categories?.FirstOrDefault(ca => ca.Id == toCat.Id);
		//         if (cat != null)
		//         {
		//             if (cat.AddFieldDescription(fldDsc))
		//             {
		//                 RefreshUnassignedFields();
		//                 return true;
		//             }
		//         }
		//         return false;
		//     }
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

		public void OnDataContextChanged()
		{
			UpdateUnassignedFields();
		}
		//     public bool UnassignFieldDescriptionFromCurrentCategory(FieldDescription fldDsc, Category toCat)
		//     {
		//var mb = _metaBriefcase;
		//if (mb == null || fldDsc == null || toCat == null) return false;

		//         var cat = mb.Categories?.FirstOrDefault(c => c.Id == toCat.Id);
		//         if (cat?.Id != null && (fldDsc.JustAssignedToCats.Contains(cat.Id) || IsElevated))
		//         {
		//             if (cat.RemoveFieldDescription(fldDsc))
		//             {
		//                 RefreshUnassignedFields();
		//                 return true;
		//             }
		//         }
		//         return false;
		//     }

		public Task<bool> AddPossibleValueToCurrentFieldDescriptionAsync()
		{
			// localisation localization globalisation globalization
			//string name = Windows.ApplicationModel.Resources.Core.ResourceManager.Current.MainResourceMap.GetValue("Resources/NewFieldValue/Text", ResourceContext.GetForCurrentView()).ValueAsString;
			return _metaBriefcase?.AddPossibleValueToCurrentFieldDescriptionAsync(/*new FieldValue() { Vaalue = name, IsCustom = true, IsJustAdded = true }*/);
		}
		//public bool AddPossibleValueToFieldDescription(FieldValue fldVal, FieldDescription toFldDesc)
		//{
		//    if (fldVal == null || toFldDesc == null) return false;

		//    fldVal.IsCustom = true;
		//    fldVal.IsJustAdded = true;

		//    var fldDesc = _metaBriefcase?.FieldDescriptions?.FirstOrDefault(fd => fd.Id == toFldDesc.Id);
		//    if (fldDesc != null)
		//    {
		//        fldDesc.AddPossibleValue(fldVal);
		//        return true;
		//    }
		//    return false;
		//}
		public Task<bool> RemovePossibleValueFromCurrentFieldDescriptionAsync(FieldValue fldVal)
		{
			return _metaBriefcase.RemovePossibleValueFromCurrentFieldDescriptionAsync(fldVal);
		}

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

		public async Task ExportAsync()
		{
			var file = await Pickers.PickSaveFileAsync(new string[] { ConstantData.XML_EXTENSION }).ConfigureAwait(false);
			if (file != null)
			{
				var bf = Briefcase.GetCurrentInstance();
				if (bf != null)
				{
					await bf.ExportSettingsAsync(file).ConfigureAwait(false);
				}
			}
		}
		public async Task ImportAsync()
		{
			var file = await Pickers.PickOpenFileAsync(new string[] { ConstantData.XML_EXTENSION }).ConfigureAwait(false);
			if (file != null)
			{
				var bf = Briefcase.GetCurrentInstance();
				if (bf != null)
				{
					await bf.ImportSettingsAsync(file).ConfigureAwait(false);
				}
			}
		}
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
