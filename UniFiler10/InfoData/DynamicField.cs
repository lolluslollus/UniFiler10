using SQLite;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using UniFiler10.Data.DB;
using UniFiler10.Data.Metadata;
using System;

namespace UniFiler10.Data.Model
{
	[DataContract]
	public class DynamicField : DbBoundObservableData
	{
		#region properties
		private FieldValue _fieldValue = null;
		[IgnoreDataMember]
		[Ignore]
		public FieldValue FieldValue { get { return _fieldValue; } private set { if (_fieldValue != value) { _fieldValue = value; RaisePropertyChanged_UI(); } } }

		private string _fieldValueId = DEFAULT_ID;
		[DataMember]
		public string FieldValueId
		{
			get { return _fieldValueId; }
			set
			{
				string newValue = value ?? DEFAULT_ID; // this property may be null or empty at any time
				string oldValue = _fieldValueId;
				if (newValue != oldValue)
				{
					_fieldValueId = newValue;
					RaisePropertyChanged_UI();
					UpdateDynamicValues();

					Task upd = RunFunctionWhileOpenAsyncA_MT(delegate
					{
						if (DBManager.OpenInstance?.UpdateDynamicFields(this) == false)
						{
							_fieldValueId = oldValue;
							RaisePropertyChanged_UI();
							UpdateDynamicValues();
						}
					});
				}
				else if (_fieldValue == null)
				{
					UpdateDynamicValues();
				}
			}
		}

		private FieldDescription _fieldDescription = null;
		[IgnoreDataMember]
		[Ignore]
		public FieldDescription FieldDescription { get { return _fieldDescription; } private set { if (_fieldDescription != value) { _fieldDescription = value; RaisePropertyChanged_UI(); } } }

		private string _fieldDescriptionId = DEFAULT_ID;
		[DataMember]
		public string FieldDescriptionId
		{
			get { return _fieldDescriptionId; }
			set
			{
				string newValue = value ?? DEFAULT_ID; // this property may be null or empty at any time
				string oldValue = _fieldDescriptionId;
				if (newValue != oldValue)
				{
					_fieldDescriptionId = newValue;
					RaisePropertyChanged_UI();
					UpdateDynamicValues();

					Task upd = RunFunctionWhileOpenAsyncA_MT(delegate
					{
						if (DBManager.OpenInstance?.UpdateDynamicFields(this) == false)
						{
							_fieldDescriptionId = oldValue;
							RaisePropertyChanged_UI();
							UpdateDynamicValues();
						}
					});
				}
				else if (_fieldDescription == null)
				{
					UpdateDynamicValues();
				}
			}
		}
		private void UpdateDynamicValues()
		{
			var metaBriefcase = MetaBriefcase.OpenInstance;
			if (metaBriefcase != null && metaBriefcase.FieldDescriptions != null && !string.IsNullOrEmpty(_fieldDescriptionId))
			{
				FieldDescription = metaBriefcase.FieldDescriptions.FirstOrDefault(fldDsc => fldDsc.Id == _fieldDescriptionId);
			}
			else
			{
				FieldDescription = null;
			}

			if (string.IsNullOrEmpty(_fieldValueId) || _fieldDescription == null || _fieldDescription.PossibleValues == null)
			{
				FieldValue = null;
			}
			else
			{
				FieldValue = _fieldDescription.PossibleValues.FirstOrDefault(posVal => posVal.Id == _fieldValueId);
			}
		}
		#endregion properties

		protected override bool UpdateDbMustOverride()
		{
			var ins = DBManager.OpenInstance;
			if (ins != null) return ins.UpdateDynamicFields(this);
			else return false;
		}
		//protected override async Task<bool> UpdateDbMustOverrideAsync()
		//{
		//	if (DBManager.OpenInstance != null) return await DBManager.OpenInstance.UpdateDynamicFieldsAsync(this).ConfigureAwait(false);
		//	else return false;
		//}

		protected override bool IsEqualToMustOverride(DbBoundObservableData that)
		{
			var target = that as DynamicField;

			return
				_fieldValueId == target.FieldValueId &&
				_fieldValue == target.FieldValue &&
				_fieldDescriptionId == target.FieldDescriptionId &&
				_fieldDescription == target.FieldDescription;
		}
		private bool IsValueAllowed()
		{
			if (_fieldDescription != null && _fieldValue != null)
				return string.IsNullOrWhiteSpace(_fieldValue.Vaalue) || _fieldDescription.IsAnyValueAllowed || _fieldDescription.PossibleValues.Any(a => a.Vaalue == _fieldValue.Vaalue);
			else if (_fieldDescription != null && _fieldValue == null)
				return true;
			else
				return false;
		}
		protected override bool CheckMeMustOverride()
		{
			bool result = _id != DEFAULT_ID && _parentId != DEFAULT_ID && _fieldDescriptionId != DEFAULT_ID && IsValueAllowed();
			return result;
		}
		//protected override void CopyMustOverride(ref DbBoundObservableData target)
		//{
		//	var tgt = target as DynamicField;

		//	tgt.FieldValueId = _fieldValueId;
		//	tgt.FieldDescriptionId = _fieldDescriptionId;
		//}

		public Task<bool> SetFieldValueAsync(string newValue)
		{
			return RunFunctionWhileOpenAsyncB(delegate
			{
				var availableFldVal = _fieldDescription.GetValueFromPossibleValues(newValue);
				if (availableFldVal != null)
				{
					FieldValueId = availableFldVal.Id;
					return true;
				}
				else if (_fieldDescription.IsAnyValueAllowed)
				{
					var newFldVal = new FieldValue() { IsCustom = true, IsJustAdded = true, Vaalue = newValue };
					if (_fieldDescription.AddPossibleValue(newFldVal))
					{
						FieldValueId = newFldVal.Id;
						return true;
					}
				}
				return false;
			});
		}

	}
}
