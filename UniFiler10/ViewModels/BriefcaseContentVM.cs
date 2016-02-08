using System;
using System.Threading.Tasks;
using UniFiler10.Data.Model;
using Utilz;
using Utilz.Data;

namespace UniFiler10.ViewModels
{
	public class BriefcaseContentVM : OpenableObservableDisposableData
	{
		private Briefcase _briefcase = null;
		public Briefcase Briefcase { get { return _briefcase; } private set { _briefcase = value; RaisePropertyChanged_UI(); } }

		public BriefcaseContentVM() { }
		protected override async Task OpenMayOverrideAsync()
		{
			_briefcase = Briefcase.GetCreateInstance();
			await _briefcase.OpenAsync();
			await _briefcase.OpenCurrentBinderAsync();

			RaisePropertyChanged_UI(nameof(Briefcase)); // notify UI once briefcase is open
		}
		protected override Task CloseMayOverrideAsync()
		{
			// briefcase and other data model classes cannot be destroyed by view models. Only app.xaml may do so.
			_briefcase = null;
			return Task.CompletedTask;
		}

		public async Task OpenBinderAsync(string dbName)
		{
			var bf = _briefcase;
			if (bf != null)
			{
				await bf.OpenBinderAsync(dbName).ConfigureAwait(false);
			}
		}
		public Task CloseBinderAsync()
		{
			return _briefcase?.CloseCurrentBinderAsync();
		}
	}
}
