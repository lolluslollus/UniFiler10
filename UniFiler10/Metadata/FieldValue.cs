using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using Utilz;

namespace UniFiler10.Data.Metadata
{
    [DataContract]
    public sealed class FieldValue : ObservableData
    {
        #region properties
        private static readonly string DEFAULT_ID = string.Empty;

        private string _id = DEFAULT_ID;
        [DataMember]
        public string Id { get { return _id; } set { _id = value; RaisePropertyChanged_UI(); } }

        private string _vaalue = string.Empty;
        [DataMember]
        public string Vaalue { get { return _vaalue; } set { _vaalue = value; RaisePropertyChanged_UI(); } }

        private bool _isCustom = false;
        [DataMember]
        public bool IsCustom { get { return _isCustom; } set { _isCustom = value; RaisePropertyChanged_UI(); } }

        private bool _isJustAdded = false;
        [IgnoreDataMember]
        public bool IsJustAdded { get { return _isJustAdded; } set { _isJustAdded = value; RaisePropertyChanged_UI(); } }
        #endregion properties

        public FieldValue()
        {
            Id = Guid.NewGuid().ToString();
        }

        public static void Copy(FieldValue source, ref FieldValue target)
        {
            if (source != null && target != null)
            {
                target.Id = source.Id;
                target.Vaalue = source.Vaalue;
                target.IsCustom = source.IsCustom;
                // target.IsJustAdded = source.IsJustAdded; // we don't actually need this
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
