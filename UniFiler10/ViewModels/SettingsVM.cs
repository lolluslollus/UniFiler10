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
    {
        private MetaBriefcase _metaBriefcase = null;
        public MetaBriefcase MetaBriefcase { get { return _metaBriefcase; } set { _metaBriefcase = value; RaisePropertyChanged_UI(); } }

        public SettingsVM(MetaBriefcase metaBriefcase)
        {
            MetaBriefcase = metaBriefcase;
        }

        public async Task<bool> AddCategoryAsync()
        {
            if (_metaBriefcase == null) return false;
           
            return await _metaBriefcase.AddCategoryAsync().ConfigureAwait(false);
        }

        public async Task<bool> RemoveCategoryAsync(Category cat)
        {
            if (_metaBriefcase == null) return false;

            return await _metaBriefcase.RemoveCategoryAsync(cat).ConfigureAwait(false);
        }

        public async Task<bool> AddFieldDescriptionAsync()
        {
            if (_metaBriefcase == null) return false;

            if (await _metaBriefcase.AddFieldDescriptionAsync())
            {
                RefreshUnassignedFields();
                return true;
            }
            return false;
        }

        public async Task<bool> RemoveFieldDescriptionAsync(FieldDescription fldDesc)
        {
            if (_metaBriefcase == null) return false;

            if (await _metaBriefcase.RemoveFieldDescription(fldDesc))
            {
                RefreshUnassignedFields();
                return true;
            }
            return false;
        }

        public bool AssignFieldDescriptionToCategory(FieldDescription fldDsc, Category toCat)
        {
            if (fldDsc == null || toCat == null) return false;

            var cat = _metaBriefcase?.Categories?.FirstOrDefault(c => c.Id == toCat.Id);
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
            if (fldDsc == null || toCat == null) return false;

            var cat = _metaBriefcase?.Categories?.FirstOrDefault(c => c.Id == toCat.Id);
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
            return RemovePossibleValueFromFieldDescription(fldVal, MetaBriefcase?.CurrentFieldDescription);
        }
        public bool RemovePossibleValueFromFieldDescription(FieldValue fldVal, FieldDescription toFldDesc)
        {
            if (fldVal == null || toFldDesc == null || !fldVal.IsJustAdded) return false;

            var fldDesc = _metaBriefcase?.FieldDescriptions?.FirstOrDefault(fd => fd.Id == toFldDesc.Id);
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
            _unassignedFields.Clear();
            if (MetaBriefcase != null && MetaBriefcase.FieldDescriptions != null && MetaBriefcase.CurrentCategory != null && MetaBriefcase.CurrentCategory.FieldDescriptions != null)
            {
                _unassignedFields.AddRange(MetaBriefcase.FieldDescriptions
                    .Where(allFldDsc => !MetaBriefcase.CurrentCategory.FieldDescriptions.Any(catFldDsc => catFldDsc.Id == allFldDsc.Id)));
                // _unassignedFields.AddRange(MetaBriefcase.FieldDescriptions.Except(MetaBriefcase.CurrentCategory.FieldDescriptions, new FieldDescription.Comparer()));
                RaisePropertyChanged_UI(nameof(UnassignedFields));
            }
        }

        public void UpdateCurrentCategory(Category newItem)
        {
            if (MetaBriefcase != null && newItem != null)
            {
                MetaBriefcase.CurrentCategoryId = newItem.Id;
                RefreshUnassignedFields();
            }
        }
        public void UpdateCurrentFieldDescription(FieldDescription newItem)
        {
            if (MetaBriefcase != null && newItem != null)
            {
                MetaBriefcase.CurrentFieldDescriptionId = newItem.Id;
            }
        }
    }
}
