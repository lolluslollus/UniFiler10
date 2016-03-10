using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using Utilz;
using Utilz.Data;

namespace UniFiler10.Data.Metadata
{
	[DataContract]
	public sealed class Category : ObservableData //, IDisposable
	{
		#region events
		public static event EventHandler NameChanged;
		#endregion events


		#region properties
		private static readonly string DEFAULT_ID = string.Empty;
		private string _id = DEFAULT_ID;
		private readonly object _propLocker = new object();
		[IgnoreDataMember]
		private object PropLocker { get { return _propLocker ?? new object(); /*for serialisation*/ } }

		[DataMember]
		public string Id { get { return _id; } set { _id = value; } }

		private string _name = string.Empty;
		[DataMember]
		public string Name { get { lock (PropLocker) { return _name; } } set { lock (PropLocker) { _name = value; } RaisePropertyChanged_UI(); NameChanged?.Invoke(this, EventArgs.Empty); } }

		private volatile bool _isCustom = false;
		[DataMember]
		public bool IsCustom { get { return _isCustom; } private set { _isCustom = value; RaisePropertyChanged_UI(); } }

		private volatile bool _isJustAdded = false;
		[IgnoreDataMember]
		public bool IsJustAdded { get { return _isJustAdded; } private set { _isJustAdded = value; RaisePropertyChanged_UI(); } }

		private SwitchableObservableCollection<FieldDescription> _fieldDescriptions = new SwitchableObservableCollection<FieldDescription>();
		[IgnoreDataMember]
		public SwitchableObservableCollection<FieldDescription> FieldDescriptions { get { return _fieldDescriptions; } }

		// we cannot make this readonly because it is serialised. we only use the setter for serialising.
		private SwitchableObservableCollection<string> _fieldDescriptionIds = new SwitchableObservableCollection<string>();
		[DataMember]
		public SwitchableObservableCollection<string> FieldDescriptionIds { get { return _fieldDescriptionIds; } set { _fieldDescriptionIds = value; } }
		#endregion properties

		public static void Copy(Category source, ref Category target, IList<FieldDescription> allFldDscs)
		{
			if (source != null && target != null)
			{
				target._fieldDescriptionIds.ReplaceAll(source._fieldDescriptionIds);
				CopyFldDscs(source, ref target, allFldDscs);
				//// populate FieldDescriptions
				//List<FieldDescription> newFldDscs = new List<FieldDescription>();
				//foreach (var fldDscId in source._fieldDescriptionIds)
				//{
				//	var newFldDsc = allFldDscs.FirstOrDefault(fd => fd.Id == fldDscId);
				//	if (newFldDsc != null) newFldDscs.Add(newFldDsc);
				//}
				//target.FieldDescriptions.ReplaceAll(newFldDscs);

				target.Id = source.Id;
				target.IsCustom = source.IsCustom;
				// target.IsJustAdded = source.IsJustAdded; // no!
				target.Name = source.Name;
			}
		}
		public static void CopyFldDscs(Category source, ref Category target, IList<FieldDescription> allFldDscs)
		{
			if (source != null && target != null)
			{
				// populate FieldDescriptions
				List<FieldDescription> newFldDscs = new List<FieldDescription>();
				foreach (var fldDscId in source._fieldDescriptionIds)
				{
					var newFldDsc = allFldDscs.FirstOrDefault(fd => fd.Id == fldDscId);
					if (newFldDsc != null) newFldDscs.Add(newFldDsc);
				}
				if (target.FieldDescriptions == null) target._fieldDescriptions = new SwitchableObservableCollection<FieldDescription>();
				target.FieldDescriptions.ReplaceAll(newFldDscs);
			}
		}
		public static void Copy(SwitchableObservableCollection<Category> source, ref SwitchableObservableCollection<Category> target, IList<FieldDescription> allFldDscs)
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

		public Category(string name, bool isCustom, bool isJustAdded) : this()
		{
			Name = name;
			IsCustom = isCustom;
			IsJustAdded = isJustAdded;
		}

		//public void Dispose()
		//{
		//	if (_isDisposed) return;
		//	_isDisposed = true;

		//	_fieldDescriptions?.Dispose();
		//	_fieldDescriptionIds?.Dispose();
		//}

		//private volatile bool _isDisposed = false;
		//[IgnoreDataMember]
		//public bool IsDisposed { get { return _isDisposed; } }
		#endregion ctor and dispose


		internal bool AddFieldDescription(FieldDescription newFldDsc)
		{
			if (newFldDsc != null && FieldDescriptions.All(fds => fds.Caption != newFldDsc.Caption && fds.Id != newFldDsc.Id))
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
