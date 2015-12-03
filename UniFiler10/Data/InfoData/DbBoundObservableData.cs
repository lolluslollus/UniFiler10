using SQLite;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using UniFiler10.Data.DB;
using Utilz;
using Windows.ApplicationModel.Core;
using Windows.UI.Core;

namespace UniFiler10.Data.Model
{
	[DataContract]
	public abstract class DbBoundObservableData : OpenableObservableData
	{
		public static readonly string DEFAULT_ID = string.Empty;

		#region properties
		// SQLite does not like a private set here
		protected string _id = DEFAULT_ID;
		[DataMember]
		[PrimaryKey]
		public string Id
		{
			get { return _id; }
			set
			{
				string newValue = value ?? DEFAULT_ID; if (_id != newValue) { _id = newValue; RaisePropertyChanged_UI(); /*Task upd = UpdateDbAsync();*/ }
			}
		}

		public string _parentId = DEFAULT_ID;
		[DataMember]
		[Indexed(Unique = false)]
		public string ParentId
		{
			get { return _parentId; }
			set
			{
				string newValue = value ?? DEFAULT_ID;
				SetProperty(ref _parentId, newValue);
				// if (_parentId != newValue) { _parentId = newValue; RaisePropertyChanged_UI(); /*Task upd = UpdateDbAsync();*/ }
			}
		}
		// LOLLO the following are various experiments with SetProperty
		// Atomicity bugs maybe? Also try inserting a big delay and see what happens. A semaphore may fix it.
		//protected async void SetProperty1(object newValue, bool onlyIfDifferent = true, [CallerMemberName] string propertyName = "")
		//{
		//	string attributeName = '_' + propertyName[0].ToString().ToLower() + propertyName.Substring(1); // only works if naming conventions are respected
		//	var fieldInfo = GetType().GetField(attributeName);

		//	object oldValue = fieldInfo.GetValue(this);
		//	if (newValue != oldValue || !onlyIfDifferent)
		//	{
		//		fieldInfo.SetValue(this, newValue);

		//		await RunFunctionWhileOpenAsyncA_MT(async delegate
		//		{
		//			if (UpdateDbMustOverride() == false)
		//			{
		//				fieldInfo.SetValue(this, oldValue);
		//				await Logger.AddAsync(GetType().ToString() + "." + propertyName + " could not be set", Logger.ForegroundLogFilename).ConfigureAwait(false);
		//			}
		//		});
		//		RaisePropertyChanged_UI(propertyName);
		//	}
		//}

		protected void SetProperty<T>(ref T fldValue, T newValue, bool onlyIfDifferent = true, [CallerMemberName] string propertyName = "")
		{
			// LOLLO TODO if you stick to this, which seems the best, you can make the private sides of the properties private again
			T oldValue = fldValue;
			if (!newValue.Equals(oldValue) || !onlyIfDifferent)
			{
				fldValue = newValue;
				RaisePropertyChanged_UI(propertyName);

				Task db = RunFunctionWhileOpenAsyncA_MT(async delegate
				{
					if (UpdateDbMustOverride() == false)
					{
						//	string attributeName = '_' + propertyName[0].ToString().ToLower() + propertyName.Substring(1); // only works if naming conventions are respected
						//	GetType().GetField(attributeName)?.SetValue(this, oldValue);
						//	RaisePropertyChanged_UI(propertyName);
						await Logger.AddAsync(GetType().ToString() + "." + propertyName + " could not be set", Logger.ForegroundLogFilename).ConfigureAwait(false);
					}
				});
			}
		}

		//protected Task SetProperty4(ref object fldValue, object newValue, bool onlyIfDifferent = true, [CallerMemberName] string propertyName = "")
		//{
		//	object oldValue = fldValue;
		//	if (newValue != oldValue || !onlyIfDifferent)
		//	{
		//		fldValue = newValue;

		//		if (_isOpen && _isEnabled)
		//		{
		//			try
		//			{
		//				_isOpenSemaphore.Wait(); //.ConfigureAwait(false);
		//				if (_isOpen && _isEnabled)
		//				{
		//					if (UpdateDbMustOverride() == false)
		//					{
		//						fldValue = oldValue;
		//					}
		//				}
		//			}
		//			catch (Exception ex)
		//			{
		//				if (SemaphoreSlimSafeRelease.IsAlive(_isOpenSemaphore))
		//					Logger.Add_TPL(ex.ToString(), Logger.ForegroundLogFilename);
		//			}
		//			finally
		//			{
		//				SemaphoreSlimSafeRelease.TryRelease(_isOpenSemaphore);
		//			}
		//		}
		//		RaisePropertyChanged_UI(propertyName);
		//	}
		//	return Task.CompletedTask;
		//}
		#endregion properties

		#region construct and dispose
		public DbBoundObservableData() : base()
		{
			_id = Guid.NewGuid().ToString(); // LOLLO copying Id from a DBIndex assigned by the DB is tempting,
											 // but it fails because DbIndex is only set when the record is put into the DB,
											 // which may be too late. So we get a GUID in the constructor, hoping it doesn't get too slow.
		}
		#endregion construct and dispose

		public static bool AreEqual(IEnumerable<DbBoundObservableData> one, IEnumerable<DbBoundObservableData> two)
		{
			if (one != null && two != null && one.Count() == two.Count())
			{
				for (int i = 0; i < one.Count(); i++)
				{
					if (!(one.ElementAt(i).IsEqualTo(two.ElementAt(i)))) return false;
				}
				return true;
			}
			return false;
		}
		public bool IsEqualTo(DbBoundObservableData compTarget)
		{
			if (compTarget != null)
				return
					//DbIndex == that.DbIndex &&
					Id == compTarget.Id &&
					ParentId == compTarget.ParentId &&
					//IsUpdateDb == that.IsUpdateDb &&
					IsEqualToMustOverride(compTarget);
			else return false;
		}
		protected abstract bool IsEqualToMustOverride(DbBoundObservableData that);

		protected abstract bool CheckMeMustOverride();
		public static bool Check(DbBoundObservableData item)
		{
			if (item == null) return false;
			else return item.CheckMeMustOverride();
		}
		public static bool Check(IEnumerable<DbBoundObservableData> items)
		{
			if (items == null) return false;
			foreach (var item in items)
			{
				if (!item.CheckMeMustOverride()) return false;
			}
			return true;
		}

		protected abstract bool UpdateDbMustOverride();

	}
}
