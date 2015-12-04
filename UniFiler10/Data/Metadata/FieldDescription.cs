﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using Utilz;

namespace UniFiler10.Data.Metadata
{
	[DataContract]
	public sealed class FieldDescription : ObservableData, IDisposable //, IEqualityComparer<FieldDescription>
	{
		private static readonly string DEFAULT_ID = string.Empty;

		#region properties
		private string _id = DEFAULT_ID;
		[DataMember]
		public string Id { get { return _id; } set { _id = value; RaisePropertyChanged_UI(); } }

		private bool _isCustom = false;
		[DataMember]
		public bool IsCustom { get { return _isCustom; } set { _isCustom = value; RaisePropertyChanged_UI(); } }

		private bool _isJustAdded = false;
		[IgnoreDataMember]
		public bool IsJustAdded { get { return _isJustAdded; } set { _isJustAdded = value; RaisePropertyChanged_UI(); } }

		private List<string> _justAssignedToCats = new List<string>();
		[IgnoreDataMember]
		public List<string> JustAssignedToCats { get { return _justAssignedToCats; } private set { _justAssignedToCats = value; RaisePropertyChanged_UI(); } }

		private bool _isAnyValueAllowed = false;
		/// <summary>
		/// If this is true, possible values can be added anywhere, 
		/// ie they can be added outside the metadata classes.
		/// </summary>
		[DataMember]
		public bool IsAnyValueAllowed { get { return _isAnyValueAllowed; } set { _isAnyValueAllowed = value; RaisePropertyChanged_UI(); } }

		private string _caption = string.Empty;
		[DataMember]
		public string Caption { get { return _caption; } set { _caption = value; RaisePropertyChanged_UI(); } }

		private SwitchableObservableCollection<FieldValue> _possibleValues = new SwitchableObservableCollection<FieldValue>();
		[DataMember]
		public SwitchableObservableCollection<FieldValue> PossibleValues { get { return _possibleValues; } private set { _possibleValues = value; RaisePropertyChanged_UI(); } }

		public enum FieldTypez { str, dat, dec, boo, nil };
		private FieldTypez _typez = FieldTypez.str;
		[DataMember]
		public FieldTypez Typez
		{
			get { return _typez; }
			set
			{
				if (_typez != value && value != FieldTypez.nil)
				{
					_typez = value;
					RaisePropertyChanged_UI();
				}
			}
		}
		#endregion properties


		#region ctor and dispose
		public FieldDescription()
		{
			Id = Guid.NewGuid().ToString();
		}
		public void Dispose()
		{
			if (_isDisposed) return;
			_isDisposed = true;

			_possibleValues?.Dispose();
			_possibleValues = null;
		}

		private bool _isDisposed = false;
		[IgnoreDataMember]
		public bool IsDisposed { get { return _isDisposed; } private set { if (_isDisposed != value) { _isDisposed = value; } } }
		#endregion ctor and dispose


		public static void Copy(FieldDescription source, ref FieldDescription target)
		{
			if (source != null && target != null)
			{
				target.Caption = source._caption;
				target.Id = source._id;
				target.IsCustom = source._isCustom;
				// target.IsJustAdded = source._isJustAdded; // we don't actually want this
				// target.JustAssignedToCats = source._justAssignedToCats; // we don't actually want this
				target.IsAnyValueAllowed = source._isAnyValueAllowed;
				FieldValue.Copy(source._possibleValues, target.PossibleValues);
				target.Typez = source._typez;
			}
		}
		public static void Copy(SwitchableObservableCollection<FieldDescription> source, ref SwitchableObservableCollection<FieldDescription> target)
		{
			if (source != null && target != null)
			{
				target.IsObserving = false;
				target.Clear();
				foreach (var sourceRecord in source)
				{
					var targetRecord = new FieldDescription();
					Copy(sourceRecord, ref targetRecord);
					target.Add(targetRecord);
				}
				target.IsObserving = true;
			}
		}

		public bool AddPossibleValue(FieldValue newValue)
		{
			if (newValue != null && !string.IsNullOrWhiteSpace(newValue.Vaalue) && !_possibleValues.Any(pv => pv.Vaalue == newValue.Vaalue || pv.Id == newValue.Id))
			{
				_possibleValues.Add(newValue);
				return true;
			}
			return false;
		}
		public FieldValue GetValueFromPossibleValues(string newValue)
		{
			if (string.IsNullOrEmpty(newValue)) return FieldValue.Empty;
			else return _possibleValues.FirstOrDefault(pv => pv.Vaalue == newValue);
		}
		public bool RemovePossibleValue(FieldValue removedValue)
		{
			if (removedValue != null) return _possibleValues.Remove(removedValue);
			else return false;
		}

		public static bool Check(FieldDescription fldDsc)
		{
			return fldDsc != null && fldDsc.Id != DEFAULT_ID && fldDsc.PossibleValues != null && !string.IsNullOrWhiteSpace(fldDsc.Caption);
		}

		public void AddToJustAssignedToCats(Category cat)
		{
			if (cat?.Id != null && _justAssignedToCats != null && !_justAssignedToCats.Contains(cat.Id))
			{
				_justAssignedToCats.Add(cat.Id);
				RaisePropertyChanged_UI(nameof(JustAssignedToCats)); // in case someone wants to bind to it
			}
		}
		public void RemoveFromJustAssignedToCats(Category cat)
		{
			if (cat?.Id != null && _justAssignedToCats != null && _justAssignedToCats.Contains(cat.Id))
			{
				_justAssignedToCats.Remove(cat.Id);
				RaisePropertyChanged_UI(nameof(JustAssignedToCats)); // in case someone wants to bind to it
			}
		}

		//public class EqComparer : IEqualityComparer<FieldDescription>
		//{
		//    bool IEqualityComparer<FieldDescription>.Equals(FieldDescription x, FieldDescription y)
		//    {
		//        return x.Id == y.Id;
		//    }

		//    int IEqualityComparer<FieldDescription>.GetHashCode(FieldDescription obj)
		//    {
		//        return obj.GetHashCode();
		//    }
		//}
	}
}