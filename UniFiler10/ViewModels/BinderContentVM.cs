using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using UniFiler10.Data.Metadata;
using UniFiler10.Data.Model;
using UniFiler10.Data.Runtime;
using Utilz;
using Utilz.Data;
using Windows.Storage;

namespace UniFiler10.ViewModels
{
	public sealed class BinderContentVM : OpenableObservableDisposableData
	{
		#region properties
		private Binder _binder = null;
		public Binder Binder { get { return _binder; } private set { _binder = value; RaisePropertyChanged_UI(); } }

		private RuntimeData _runtimeData = null;
		public RuntimeData RuntimeData { get { return _runtimeData; } private set { _runtimeData = value; RaisePropertyChanged_UI(); } }
		#endregion properties


		#region lifecycle
		public BinderContentVM() { }
		protected async override Task OpenMayOverrideAsync()
		{
			var briefcase = Briefcase.GetCreateInstance();
			await briefcase.OpenAsync();
			await briefcase.OpenCurrentBinderAsync();

			_binder = briefcase.CurrentBinder;
			if (_binder != null)
			{
				await _binder.OpenAsync();
				await _binder.OpenCurrentFolderAsync();
			}
			RaisePropertyChanged_UI(nameof(Binder));

			RuntimeData = RuntimeData.Instance;
		}

		protected override Task CloseMayOverrideAsync()
		{
			// briefcase and other data model classes cannot be destroyed by view models. Only app.xaml may do so.
			_binder = null;

			return Task.CompletedTask;
		}
		#endregion lifecycle


		#region user actions
		public Task SetCurrentFolderAsync(string folderId)
		{
			return _binder?.OpenFolderAsync(folderId);
		}
		#endregion user actions
	}
}