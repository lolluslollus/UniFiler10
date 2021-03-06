﻿using System;
using System.Runtime.Serialization;
using Utilz;
using Utilz.Data;

namespace UniFiler10.Data.Metadata
{
	[DataContract]
	public sealed class FieldValue : ObservableData
	{
		#region events
		public static event EventHandler VaalueChanged;
		#endregion events


		#region properties
		private static readonly string DEFAULT_ID = string.Empty;
		public static readonly FieldValue Empty = new FieldValue() { Id = DEFAULT_ID };
		private readonly object _propLocker = new object();
		[IgnoreDataMember]
		private object PropLocker { get { return _propLocker ?? new object(); /*for serialisation*/ } }


		private string _id = DEFAULT_ID;
		[DataMember]
		public string Id { get { return _id; } set { _id = value; } }

		private string _vaalue = string.Empty;
		[DataMember]
		public string Vaalue { get { lock (PropLocker) { return _vaalue; } } set { lock (PropLocker) { _vaalue = value; } RaisePropertyChanged_UI(); VaalueChanged?.Invoke(this, EventArgs.Empty); } }

		private volatile bool _isCustom = false;
		[DataMember]
		public bool IsCustom { get { return _isCustom; } private set { _isCustom = value; RaisePropertyChanged_UI(); } }

		private volatile bool _isJustAdded = false;
		[IgnoreDataMember]
		public bool IsJustAdded { get { return _isJustAdded; } private set { _isJustAdded = value; RaisePropertyChanged_UI(); } }
		#endregion properties

		public FieldValue()
		{
			Id = Guid.NewGuid().ToString();
		}
		public FieldValue(string vaalue, bool isCustom, bool isJustAdded) : this()
		{
			Vaalue = vaalue;
			IsCustom = isCustom;
			IsJustAdded = isJustAdded;
		}
		public static void Copy(FieldValue source, ref FieldValue target)
		{
			if (source != null && target != null)
			{
				target.Id = source.Id;
				target.Vaalue = source.Vaalue;
				target.IsCustom = source.IsCustom;
				// target.IsJustAdded = source.IsJustAdded; // we don't want this!
			}
		}
		public static void Copy(SwitchableObservableCollection<FieldValue> source, SwitchableObservableCollection<FieldValue> target)
		{
			if (source != null && target != null)
			{
				target.IsObserving = false;
				foreach (var sourceRecord in source)
				{
					var targetRecord = new FieldValue();
					Copy(sourceRecord, ref targetRecord);
					target.Add(targetRecord);
				}
				target.IsObserving = true;
			}
		}
	}
}