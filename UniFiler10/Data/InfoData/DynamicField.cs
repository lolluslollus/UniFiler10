﻿using SQLite;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using UniFiler10.Data.DB;
using UniFiler10.Data.Metadata;
using System;
using Utilz;

namespace UniFiler10.Data.Model
{
	[DataContract]
	public class DynamicField : DbBoundObservableData
	{
		public DynamicField() { }
		public DynamicField(DBManager dbManager, string parentId, string fieldDescriptionId)
		{
			_dbManager = dbManager;
			ParentId = parentId;
			FieldDescriptionId = fieldDescriptionId;
		}
		protected override void Dispose(bool isDisposing)
		{
			base.Dispose(isDisposing);

			_dbManager = null;
		}


		#region properties
		private DBManager _dbManager = null;
		[IgnoreDataMember]
		[Ignore]
		public DBManager DBManager { get { return _dbManager; } set { _dbManager = value; } }

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
					UpdateDynamicValues2();
					RaisePropertyChanged_UI();

					Task upd = RunFunctionWhileOpenAsyncA_MT(delegate
					{
						if (_dbManager?.UpdateDynamicFields(this) == false)
						{
							//_fieldValueId = oldValue;
							//UpdateDynamicValues2();
							//RaisePropertyChanged_UI();
							Logger.Add_TPL(GetType().ToString() + "." + nameof(FieldValueId) + " could not be set", Logger.ForegroundLogFilename);
						}
					});
				}
				else if (_fieldValue == null)
				{
					UpdateDynamicValues2();
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
					UpdateDynamicValues2();
					RaisePropertyChanged_UI();

					Task upd = RunFunctionWhileOpenAsyncA_MT(delegate
					{
						if (_dbManager?.UpdateDynamicFields(this) == false)
						{
							//_fieldDescriptionId = oldValue;
							//UpdateDynamicValues2();
							//RaisePropertyChanged_UI();
							Logger.Add_TPL(GetType().ToString() + "." + nameof(FieldDescriptionId) + " could not be set", Logger.ForegroundLogFilename);
						}
					});
				}
				else if (_fieldDescription == null)
				{
					UpdateDynamicValues2();
				}
			}
		}
		private void UpdateDynamicValues2()
		{
			var mbf = MetaBriefcase.OpenInstance;
			if (mbf != null && mbf.FieldDescriptions != null && !string.IsNullOrEmpty(_fieldDescriptionId))
			{
				FieldDescription = mbf.FieldDescriptions.FirstOrDefault(fldDsc => fldDsc.Id == _fieldDescriptionId);
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
			return _dbManager?.UpdateDynamicFields(this) == true;
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


		#region while open methods
		public Task<bool> TrySetFieldValueAsync(string newValue)
		{
			return RunFunctionWhileOpenAsyncTB(async delegate
			{
				if (_fieldDescription == null) return false;

				bool isOk = false;

				if (_fieldValue != null && _fieldValue.Vaalue != newValue)
				{
					string oldValue = _fieldValue.Vaalue;
					isOk = await TrySetFieldValueId(newValue);
					if (!isOk) _fieldValue.Vaalue = oldValue;
				}
				else if (_fieldValue == null)
				{
					isOk = await TrySetFieldValueId(newValue);
				}

				return isOk;
			});
		}

		private async Task<bool> TrySetFieldValueId(string newValue)
		{
			if (_fieldDescription == null) return false;

			var availableFldVal = _fieldDescription.GetValueFromPossibleValues(newValue);
			if (availableFldVal != null)
			{
				FieldValueId = availableFldVal.Id;
				return true;
			}
			else if (_fieldDescription.IsAnyValueAllowed)
			{
				var newFldVal = new FieldValue() { Vaalue = newValue, IsCustom = true, IsJustAdded = true };
				var mb = MetaBriefcase.OpenInstance;
				if (mb != null)
				{
					// LOLLO NOTE save metaBriefcase, in case there is a crash before the next Suspend.
					// This problem actually affects all XML-based stuff, because they only save on closing.
					// We only take extra care of MetaBriefcase because Briefcase and Binder do not save critical data.
					// The DB, instead, saves at once. If there is a crash between the DB and the XML being saved, the next startup will have corrupt data.
					if (await mb.AddPossibleValueToFieldDescriptionAsync(_fieldDescription, newFldVal, true))
					{
						FieldValueId = newFldVal.Id;
						return true;
					}
				}
			}
			return false;
		}
		#endregion while open methods
	}
}