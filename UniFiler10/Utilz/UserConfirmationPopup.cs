using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UniFiler10.ViewModels;
using UniFiler10.Views;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Utilz
{
	public class UserConfirmationPopup
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


		public async Task<bool> GetUserConfirmationBeforeDeletingBinderAsync()
		{
			Flyout dialog = new Flyout();
			dialog.Closed += OnYesNoDialog_Closed;
			var dialogContent = new ConfirmationBeforeDeletingBinder();
			dialog.Content = dialogContent;
			dialogContent.UserAnswered += OnYesNoDialogContent_UserAnswered;

			dialog.ShowAt(Window.Current.Content as FrameworkElement);

			while (!_isHasUserAnswered)
			{
				await Task.Delay(DELAY);
			}

			dialog.Closed -= OnYesNoDialog_Closed;
			dialogContent.UserAnswered -= OnYesNoDialogContent_UserAnswered;
			dialog.Hide();
			_isHasUserAnswered = false;
			return dialogContent.YesNo;
		}

		private void OnYesNoDialog_Closed(object sender, object e)
		{
			_isHasUserAnswered = true;
		}

		private void OnYesNoDialogContent_UserAnswered(object sender, bool e)
		{
			_isHasUserAnswered = true;
		}


		public async Task<BriefcaseVM.ImportBinderOperations> GetUserConfirmationBeforeImportingBinderAsync()
		{
			Flyout dialog = new Flyout();
			dialog.Closed += OnThreeBtnDialog_Closed;
			var dialogContent = new ConfirmationBeforeImportingBinder();
			dialog.Content = dialogContent;
			dialogContent.UserAnswered += OnThreeBtnDialogContent_UserAnswered;

			dialog.ShowAt(Window.Current.Content as FrameworkElement);

			while (!_isHasUserAnswered)
			{
				await Task.Delay(DELAY);
			}

			dialog.Closed -= OnThreeBtnDialog_Closed;
			dialogContent.UserAnswered -= OnThreeBtnDialogContent_UserAnswered;
			dialog.Hide();
			_isHasUserAnswered = false;
			return dialogContent.Operation;
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
