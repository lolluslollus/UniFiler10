using System.Threading.Tasks;
using UniFiler10.Data.Model;
using Utilz.Data;

namespace UniFiler10.ViewModels
{
	public class BriefcaseContentVM : OpenableObservableDisposableData
	{
		private Briefcase _briefcase = null;
		public Briefcase Briefcase { get { return _briefcase; } }

		public BriefcaseContentVM() { }
		protected override async Task OpenMayOverrideAsync(object args = null)
		{
			_briefcase = Briefcase.GetCreateInstance();
			await _briefcase.OpenAsync();
			await _briefcase.OpenCurrentBinderAsync();
			RaisePropertyChanged_UI(nameof(Briefcase)); // notify UI once briefcase is open
		}

		public async Task OpenBinderAsync(string dbName)
		{
			var bf = _briefcase;
			if (bf == null) return;
			await bf.OpenBinderAsync(dbName).ConfigureAwait(false);
		}
		public Task CloseBinderAsync()
		{
			return _briefcase?.CloseCurrentBinderAsync() ?? Task.CompletedTask;
		}
	}
}
