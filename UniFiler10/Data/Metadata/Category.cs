using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using Utilz;
using Utilz.Data;

namespace UniFiler10.Data.Metadata
{
	[DataContract]
	public sealed class Category : ObservableData, IDisposable
	{
		private static readonly string DEFAULT_ID = string.Empty;

		#region properties
		private string _id = DEFAULT_ID;
		[DataMember]
		public string Id { get { return _id; } set { _id = value; RaisePropertyChanged_UI(); } }

		private string _name = string.Empty;
		[DataMember]
		public string Name { get { return _name; } set { _name = value; RaisePropertyChanged_UI(); } }

		private bool _isCustom = false;
		[DataMember]
		public bool IsCustom { get { return _isCustom; } set { _isCustom = value; RaisePropertyChanged_UI(); } }

		private bool _isJustAdded = false;
		[IgnoreDataMember]
		public bool IsJustAdded { get { return _isJustAdded; } set { _isJustAdded = value; RaisePropertyChanged_UI(); } }

		private readonly SwitchableObservableDisposableCollection<FieldDescription> _fieldDescriptions = new SwitchableObservableDisposableCollection<FieldDescription>();
		[IgnoreDataMember]
		public SwitchableObservableDisposableCollection<FieldDescription> FieldDescriptions { get { return _fieldDescriptions; } /*set { _fieldDescriptions = value; RaisePropertyChanged_UI(); }*/ }

		// we cannot make this readonly because it is serialised. we only use the setter for serialising.
		private SwitchableObservableDisposableCollection<string> _fieldDescriptionIds = new SwitchableObservableDisposableCollection<string>();
		[DataMember]
		public SwitchableObservableDisposableCollection<string> FieldDescriptionIds { get { return _fieldDescriptionIds; } set { _fieldDescriptionIds = value; RaisePropertyChanged_UI(); } }
		#endregion properties

		public static void Copy(Category source, ref Category target, IList<FieldDescription> allFldDscs)
		{
			if (source != null && target != null)
			{
				target._fieldDescriptionIds.ReplaceAll(source._fieldDescriptionIds);
				// populate FieldDescriptions
				List<FieldDescription> newFldDscs = new List<FieldDescription>();
				foreach (var fldDscId in source._fieldDescriptionIds)
				{
					var newFldDsc = allFldDscs.FirstOrDefault(fd => fd.Id == fldDscId);
					if (newFldDsc != null) newFldDscs.Add(newFldDsc);
				}
				target.FieldDescriptions.ReplaceAll(newFldDscs);

				target.Id = source.Id;
				target.IsCustom = source.IsCustom;
				// target.IsJustAdded = source.IsJustAdded; // we don't actually need this
				target.Name = source.Name;
			}
		}
		public static void Copy(SwitchableObservableDisposableCollection<Category> source, ref SwitchableObservableDisposableCollection<Category> target, IList<FieldDescription> allFldDscs)
		{
			if (source != null && target != null)
			{
				target.IsObserving = false;
				target.Clear();
				foreach (var sourceRecord in source)
				{
					var targetRecord = new Category();
					Copy(sourceRecord, ref targetRecord, allFldDscs);
					target.Add(targetRecord);
				}
				target.IsObserving = true;
			}
		}

		#region ctor and dispose
		public Category()
		{
			Id = Guid.NewGuid().ToString();
		}
		public void Dispose()
		{
			if (_isDisposed) return;
			_isDisposed = true;

			_fieldDescriptions?.Dispose();
			//_fieldDescriptions = null;

			_fieldDescriptionIds?.Dispose();
			_fieldDescriptionIds = null;
		}

		private volatile bool _isDisposed = false;
		[IgnoreDataMember]
		public bool IsDisposed { get { return _isDisposed; } private set { if (_isDisposed != value) { _isDisposed = value; } } }
		#endregion ctor and dispose


		internal bool AddFieldDescription(FieldDescription newFldDsc)
		{
			if (newFldDsc != null && !FieldDescriptions.Any(fds => fds.Caption == newFldDsc.Caption || fds.Id == newFldDsc.Id))
			{
				_fieldDescriptions.Add(newFldDsc);
				_fieldDescriptionIds.Add(newFldDsc.Id);
				newFldDsc.AddToJustAssignedToCats(this);
				return true;
			}
			return false;
		}

		internal bool RemoveFieldDescription(FieldDescription fdToBeRemoved)
		{
			if (fdToBeRemoved != null)
			{
				fdToBeRemoved.RemoveFromJustAssignedToCats(this);
				bool isOk = _fieldDescriptions.Remove(fdToBeRemoved) & _fieldDescriptionIds.Remove(fdToBeRemoved.Id);
				return isOk;
			}
			return false;
		}

		public static bool Check(Category cat)
		{
			return cat != null && cat.Id != DEFAULT_ID && cat.FieldDescriptions != null && cat.FieldDescriptionIds != null && !string.IsNullOrWhiteSpace(cat.Name);
		}

		//public class EqComparer : IEqualityComparer<Category>
		//{
		//    bool IEqualityComparer<Category>.Equals(Category x, Category y)
		//    {
		//        return x.Id == y.Id;
		//    }

		//    int IEqualityComparer<Category>.GetHashCode(Category obj)
		//    {
		//        return obj.GetHashCode();
		//    }
		//}
	}
}
