using System.Threading.Tasks;
using UniFiler10.Data.Model;
using UniFiler10.Data.Runtime;
using Utilz.Data;

namespace UniFiler10.ViewModels
{
	public sealed class BinderContentVM : OpenableObservableDisposableData
	{
		#region properties
		private Binder _binder = null;
		public Binder Binder { get { return _binder; } }

		private RuntimeData _runtimeData = null;
		public RuntimeData RuntimeData { get { return _runtimeData; } }
		#endregion properties


		#region lifecycle
		public BinderContentVM() { }
		protected override async Task OpenMayOverrideAsync(object args = null)
		{
			var briefcase = Briefcase.GetCreateInstance();
			await briefcase.OpenAsync().ConfigureAwait(false);
			await briefcase.OpenCurrentBinderAsync().ConfigureAwait(false);

			_binder = briefcase.CurrentBinder;
			if (_binder != null)
			{
				await _binder.OpenCurrentFolderAsync();
			}
			RaisePropertyChanged_UI(nameof(Binder));

			_runtimeData = RuntimeData.Instance;
			RaisePropertyChanged_UI(nameof(RuntimeData));
		}
		#endregion lifecycle


		#region user actions
		public Task SetCurrentFolderAsync(string folderId)
		{
			return _binder?.OpenFolderAsync(folderId) ?? Task.CompletedTask;
		}
		#endregion user actions
	}
}