using System;
using System.Threading.Tasks;
using UniFiler10.Data.Model;
using Windows.ApplicationModel.Resources;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.UI.Popups;

namespace UniFiler10.ViewModels
{
	public class BriefcaseContentVM : OpenableObservableData
	{
		private Briefcase _briefcase = null;
		public Briefcase Briefcase { get { return _briefcase; } private set { _briefcase = value; RaisePropertyChanged_UI(); } }

		public BriefcaseContentVM() { }
		protected override async Task OpenMayOverrideAsync()
		{
			_briefcase = Briefcase.GetOrCreateInstance();
			await _briefcase.OpenAsync();
			await _briefcase.OpenCurrentBinderAsync();

			RaisePropertyChanged_UI(nameof(Briefcase)); // notify UI once briefcase is open
		}
		protected override Task CloseMayOverrideAsync()
		{
			// briefcase and other data model classes cannot be destroyed by view models. Only app.xaml may do so.
			Briefcase = null;
			return Task.CompletedTask;
		}

		public async Task<bool> OpenBinderAsync(string dbName)
		{
			var bf = _briefcase;
			if (bf != null)
			{
				return await bf.OpenBinderAsync(dbName).ConfigureAwait(false);
			}
			return false;
		}
		public Task CloseBinderAsync()
		{
			return _briefcase?.CloseCurrentBinderAsync();
		}
	}
}
