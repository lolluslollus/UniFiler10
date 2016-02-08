using System;
using System.Threading.Tasks;
using UniFiler10.ViewModels;
using UniFiler10.Views;
using Utilz.Data;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Utilz
{
	public class UserConfirmationPopup : ObservableData
	{
		private const int DELAY = 50;
		private static UserConfirmationPopup _instance = null;
		private static readonly object _locker = new object();
		public static UserConfirmationPopup GetInstance()
		{
			lock (_locker)
			{
				if (_instance == null) _instance = new UserConfirmationPopup();
				return _instance;
			}
		}
		private UserConfirmationPopup() { }

		private bool _isHasUserAnswered = false;


		public async Task<Tuple<bool, bool>> GetUserConfirmationBeforeDeletingBinderAsync()
		{
			var result = new Tuple<bool, bool>(false, false);
			Flyout dialog = null;
			ConfirmationBeforeDeletingBinder dialogContent = null;

			await RunInUiThreadAsync(delegate
			{
				dialog = new Flyout();
				dialogContent = new ConfirmationBeforeDeletingBinder();

				dialog.Closed += OnYesNoDialog_Closed;

				dialog.Content = dialogContent;
				dialogContent.UserAnswered += OnYesNoDialogContent_UserAnswered;

				_isHasUserAnswered = false;
				dialog.ShowAt(Window.Current.Content as FrameworkElement);
			}).ConfigureAwait(false);

			while (!_isHasUserAnswered)
			{
				await Task.Delay(DELAY).ConfigureAwait(false);
			}

			await RunInUiThreadAsync(delegate
			{
				dialog.Closed -= OnYesNoDialog_Closed;
				dialogContent.UserAnswered -= OnYesNoDialogContent_UserAnswered;
				dialog.Hide();
				result = new Tuple<bool, bool>(dialogContent.YesNo, dialogContent.IsHasUserInteracted);
			}).ConfigureAwait(false);

			return result;
		}

		private void OnYesNoDialog_Closed(object sender, object e)
		{
			_isHasUserAnswered = true;
		}

		private void OnYesNoDialogContent_UserAnswered(object sender, bool e)
		{
			_isHasUserAnswered = true;
		}


		public async Task<Tuple<BriefcaseVM.ImportBinderOperations, bool>> GetUserConfirmationBeforeImportingBinderAsync()
		{
			var result = new Tuple<BriefcaseVM.ImportBinderOperations, bool>(BriefcaseVM.ImportBinderOperations.Cancel, false);
			Flyout dialog = null;
			ConfirmationBeforeImportingBinder dialogContent = null;

			await RunInUiThreadAsync(delegate
			{
				dialog = new Flyout();
				dialogContent = new ConfirmationBeforeImportingBinder();

				dialog.Closed += OnThreeBtnDialog_Closed;

				dialog.Content = dialogContent;
				dialogContent.UserAnswered += OnThreeBtnDialogContent_UserAnswered;

				_isHasUserAnswered = false;
				dialog.ShowAt(Window.Current.Content as FrameworkElement);
			}).ConfigureAwait(false);

			while (!_isHasUserAnswered)
			{
				await Task.Delay(DELAY).ConfigureAwait(false);
			}

			await RunInUiThreadAsync(delegate
			{
				dialog.Closed -= OnThreeBtnDialog_Closed;
				dialogContent.UserAnswered -= OnThreeBtnDialogContent_UserAnswered;
				dialog.Hide();
				result = new Tuple<BriefcaseVM.ImportBinderOperations, bool>(dialogContent.Operation, dialogContent.IsHasUserInteracted);
			}).ConfigureAwait(false);

			return result;
		}

		private void OnThreeBtnDialog_Closed(object sender, object e)
		{
			_isHasUserAnswered = true;
		}

		private void OnThreeBtnDialogContent_UserAnswered(object sender, BriefcaseVM.ImportBinderOperations e)
		{
			_isHasUserAnswered = true;
		}

	}
}