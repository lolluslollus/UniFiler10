using SQLite;
using System.Linq;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using UniFiler10.Data.DB;
using UniFiler10.Data.Metadata;
using Utilz;
using Utilz.Data;

namespace UniFiler10.Data.Model
{
	[DataContract]
	public class DynamicField : DbBoundObservableData
	{
		#region lifecycle
		public DynamicField() { }
		public DynamicField(DBManager dbManager, string parentId, string fieldDescriptionId) : base()
		{
			DBManager = dbManager;
			ParentId = parentId;
			FieldDescriptionId = fieldDescriptionId;
		}
		protected override void Dispose(bool isDisposing)
		{
			base.Dispose(isDisposing);

			_dbManager = null;
		}
		#endregion lifecycle


		#region properties
		private readonly object _dbManagerLocker = new object();
		private DBManager _dbManager = null;
		[IgnoreDataMember]
		[Ignore]
		public DBManager DBManager { get { lock (_dbManagerLocker) { return _dbManager; } } set { lock (_dbManagerLocker) { _dbManager = value; } } }

		private volatile FieldValue _fieldValue = null;
		[IgnoreDataMember]
		[Ignore]
		public FieldValue FieldValue { get { return _fieldValue; } private set { if (_fieldValue != value) { _fieldValue = value; RaisePropertyChanged_UI(); } } }

		private volatile string _fieldValueId = DEFAULT_ID;
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

					Task upd = RunFunctionIfOpenAsyncA_MT(delegate
					{
						if (DBManager?.UpdateDynamicFields(this) == false)
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

		private volatile FieldDescription _fieldDescription = null;
		[IgnoreDataMember]
		[Ignore]
		public FieldDescription FieldDescription { get { return _fieldDescription; } private set { if (_fieldDescription != value) { _fieldDescription = value; RaisePropertyChanged_UI(); } } }

		private volatile string _fieldDescriptionId = DEFAULT_ID;
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

					Task upd = RunFunctionIfOpenAsyncA_MT(delegate
					{
						if (DBManager?.UpdateDynamicFields(this) == false)
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
			if (mbf?.FieldDescriptions != null && !string.IsNullOrEmpty(_fieldDescriptionId))
			{
				FieldDescription = mbf.FieldDescriptions.FirstOrDefault(fldDsc => fldDsc.Id == _fieldDescriptionId);
			}
			else
			{
				FieldDescription = null;
			}

			if (string.IsNullOrEmpty(_fieldValueId) || _fieldDescription?.PossibleValues == null)
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
			return DBManager?.UpdateDynamicFields(this) == true;
		}

		private bool IsValueAllowed()
		{
			var fd = _fieldDescription;
			var fv = _fieldValue;

			if (fd != null && fv != null)
				return string.IsNullOrWhiteSpace(fv.Vaalue) || fd.IsAnyValueAllowed || fd.PossibleValues.Any(a => a.Vaalue == fv.Vaalue);
			else if (fd != null && fv == null)
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
			return RunFunctionIfOpenAsyncTB(async delegate
			{
				var fd = _fieldDescription;
				if (fd == null) return false;
				var fv = _fieldValue;

				bool isOk = false;

				if (fv != null && fv.Vaalue != newValue)
				{
					string oldValue = fv.Vaalue;
					isOk = await TrySetFieldValueId(fd, newValue);
					if (!isOk) fv.Vaalue = oldValue;
				}
				else if (fv == null)
				{
					isOk = await TrySetFieldValueId(fd, newValue);
				}

				return isOk;
			});
		}

		private async Task<bool> TrySetFieldValueId(FieldDescription fd, string newValue)
		{
			if (fd == null) return false;
			var bc = Briefcase.GetCurrentInstance();

			var availableFldVal = fd.GetValueFromPossibleValues(newValue);
			if (availableFldVal != null)
			{
				FieldValueId = availableFldVal.Id;
				return true;
			}
			if (fd.IsAnyValueAllowed && bc != null /*&& !bc.IsWantAndCannotUseOneDrive*/)
			{
				var mb = MetaBriefcase.OpenInstance;
				if (mb != null)
				{
					var newFldVal = new FieldValue(newValue, true, true);
					// LOLLO NOTE save metaBriefcase, in case there is a crash before the next Suspend.
					// This problem actually affects all XML-based stuff, because they only save on closing.
					// We only take extra care of MetaBriefcase because Briefcase and Binder do not save critical data.
					// The DB, instead, saves at once. If there is a crash between the DB and the XML being saved, the next startup will have corrupt data.
					if (await mb.AddPossibleValueToFieldDescriptionAsync(fd, newFldVal, true))
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