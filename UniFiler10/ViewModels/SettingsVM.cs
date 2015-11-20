using System;
using System.Collections.Generic;
using System.Linq;
using System.Resources;
using System.Text;
using System.Threading.Tasks;
using UniFiler10.Data.Metadata;
using Utilz;
using Windows.ApplicationModel.Resources.Core;

namespace UniFiler10.ViewModels
{
    public class SettingsVM : ObservableData
    {// LOLLO TODO make disposable ?
        private MetaBriefcase _metaBriefcase = null;
        public MetaBriefcase MetaBriefcase { get { return _metaBriefcase; } set { _metaBriefcase = value; RaisePropertyChanged_UI(); } }

        public SettingsVM(MetaBriefcase metaBriefcase)
        {
            MetaBriefcase = metaBriefcase;
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
			var metaBriefcase = _metaBriefcase;
			if (metaBriefcase != null)
			{
				if (await metaBriefcase.AddFieldDescriptionAsync())
				{
					RefreshUnassignedFields();
					return true;
				}
			}
            return false;
        }

        public async Task<bool> RemoveFieldDescriptionAsync(FieldDescription fldDesc)
        {
			var metaBriefcase = _metaBriefcase;
			if (metaBriefcase != null)
			{
				if (await metaBriefcase.RemoveFieldDescription(fldDesc))
				{
					RefreshUnassignedFields();
					return true;
				}
			}
            return false;
        }

        public bool AssignFieldDescriptionToCategory(FieldDescription fldDsc, Category toCat)
        {
			var mb = _metaBriefcase;
			if (mb == null || fldDsc == null || toCat == null) return false;

            var cat = mb.Categories?.FirstOrDefault(ca => ca.Id == toCat.Id);
            if (cat != null)
            {
                if (cat.AddFieldDescription(fldDsc))
                {
                    RefreshUnassignedFields();
                    return true;
                }
            }
            return false;
        }
        public bool UnassignFieldDescriptionFromCategory(FieldDescription fldDsc, Category toCat)
        {
			var mb = _metaBriefcase;
			if (mb == null || fldDsc == null || toCat == null) return false;

            var cat = mb.Categories?.FirstOrDefault(c => c.Id == toCat.Id);
            if (cat?.Id != null && fldDsc.JustAssignedToCats.Contains(cat.Id))
            {
                if (cat.RemoveFieldDescription(fldDsc))
                {
                    RefreshUnassignedFields();
                    return true;
                }
            }
            return false;
        }

        public bool AddPossibleValueToFieldDescription()
        {
			// localisation localization globalisation globalization
            string name = Windows.ApplicationModel.Resources.Core.ResourceManager.Current.MainResourceMap.GetValue("Resources/NewFieldValue/Text", ResourceContext.GetForCurrentView()).ValueAsString;
            return AddPossibleValueToFieldDescription(new FieldValue() { Vaalue = name, IsCustom = true, IsJustAdded = true }, MetaBriefcase?.CurrentFieldDescription);
        }
        public bool AddPossibleValueToFieldDescription(FieldValue fldVal, FieldDescription toFldDesc)
        {
            if (fldVal == null || toFldDesc == null) return false;

            fldVal.IsCustom = true;
            fldVal.IsJustAdded = true;

            var fldDesc = _metaBriefcase?.FieldDescriptions?.FirstOrDefault(fd => fd.Id == toFldDesc.Id);
            if (fldDesc != null)
            {
                fldDesc.AddPossibleValue(fldVal);
                return true;
            }
            return false;
        }
        public bool RemovePossibleValueFromFieldDescription(FieldValue fldVal)
        {
            if (fldVal == null || !fldVal.IsJustAdded) return false;
            return RemovePossibleValueFromFieldDescription(fldVal, _metaBriefcase?.CurrentFieldDescription);
        }
        public bool RemovePossibleValueFromFieldDescription(FieldValue fldVal, FieldDescription toFldDesc)
        {
			var mb = _metaBriefcase;
			if (mb == null || fldVal == null || toFldDesc == null || !fldVal.IsJustAdded) return false;

            var fldDesc = mb.FieldDescriptions?.FirstOrDefault(fd => fd.Id == toFldDesc.Id);
            if (fldDesc != null)
            {
                return fldDesc.RemovePossibleValue(fldVal);
            }
            return false;
        }

        private SwitchableObservableCollection<FieldDescription> _unassignedFields = new SwitchableObservableCollection<FieldDescription>();
        public SwitchableObservableCollection<FieldDescription> UnassignedFields { get { return _unassignedFields; } }
        private void RefreshUnassignedFields()
        {
			var mb = _metaBriefcase;
			_unassignedFields.Clear();
            if (mb != null && mb.FieldDescriptions != null && mb.CurrentCategory != null && mb.CurrentCategory.FieldDescriptionIds != null)
            {
                _unassignedFields.AddRange(mb.FieldDescriptions
                    .Where(allFldDsc => !mb.CurrentCategory.FieldDescriptions.Any(catFldDsc => catFldDsc.Id == allFldDsc.Id)));
                // _unassignedFields.AddRange(MetaBriefcase.FieldDescriptions.Except(MetaBriefcase.CurrentCategory.FieldDescriptions, new FieldDescription.Comparer()));
                RaisePropertyChanged_UI(nameof(UnassignedFields));
            }
        }

        public void UpdateCurrentCategory(Category newItem)
        {
			var mb = _metaBriefcase;
			if (mb != null && newItem != null)
            {
                mb.CurrentCategoryId = newItem.Id;
                RefreshUnassignedFields();
            }
        }
        public void UpdateCurrentFieldDescription(FieldDescription newItem)
        {
			var mb = _metaBriefcase;
            if (mb != null && newItem != null) mb.CurrentFieldDescriptionId = newItem.Id;
        }
    }
}
