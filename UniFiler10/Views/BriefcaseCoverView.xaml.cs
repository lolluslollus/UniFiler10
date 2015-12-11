using Microsoft.CSharp.RuntimeBinder;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using UniFiler10.Controlz;
using UniFiler10.ViewModels;
using Utilz;
using Windows.ApplicationModel.Resources;
using Windows.Devices.Sensors;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Foundation.Metadata;
using Windows.Graphics.Display;
using Windows.Phone.UI.Input;
using Windows.UI.Core;
using Windows.UI.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Navigation;

// The User Control item template is documented at http://go.microsoft.com/fwlink/?LinkId=234236

namespace UniFiler10.Views
{
	public sealed partial class BriefcaseCoverView : ObservableControl, IAnimationStarter
	{
		#region events
		public event EventHandler GoToBinderContentRequested;
		public event EventHandler GoToSettingsRequested;
		#endregion events


		#region properties
		public BriefcaseVM VM
		{
			get { return (BriefcaseVM)GetValue(VMProperty); }
			set { SetValue(VMProperty, value); }
		}
		public static readonly DependencyProperty VMProperty =
			DependencyProperty.Register("VM", typeof(BriefcaseVM), typeof(BriefcaseCoverView), new PropertyMetadata(null));
		//private BriefcaseVM _vm = null;
		//public BriefcaseVM VM { get { return _vm; } set { _vm = value; RaisePropertyChanged_UI(); } }
		#endregion properties


		#region construct, dispose, open, close
		public BriefcaseCoverView()
		{
			InitializeComponent();
		}
		#endregion construct, dispose, open, close


		#region event handlers
		private async void OnBinderPreviews_ItemClick(object sender, ItemClickEventArgs e)
		{
			var vm = VM;
			if (vm != null)
			{
				if (await vm.SetCurrentBinderAsync(e?.ClickedItem?.ToString()))
				{
					GoToBinderContentRequested?.Invoke(this, EventArgs.Empty);
				}
			}
		}

		private void OnAddBinderStep0_Tapped(object sender, TappedRoutedEventArgs e)
		{
			VM?.AddDbStep0();
		}
		private void OnAddBinderStep1_Tapped(object sender, TappedRoutedEventArgs e)
		{
			Task add = VM?.AddDbStep1Async();
		}

		private async void OnBackupButton_Tapped(object sender, TappedRoutedEventArgs e)
		{
			var vm = VM;
			if (vm != null)
			{
				bool isOk = await vm.BackupDbAsync((sender as FrameworkElement)?.DataContext as string);
				if (isOk)
				{
					StartAnimation((int)Animations.Success);
				}
				else
				{
					StartAnimation((int)Animations.Failure);
				}
			}
		}

		private void OnRestoreButton_Tapped(object sender, TappedRoutedEventArgs e)
		{
			Task restore = VM?.ImportDbAsync();
		}

		private void OnDeleteButton_Tapped(object sender, TappedRoutedEventArgs e)
		{
			Task delete = VM?.DeleteDbAsync((sender as FrameworkElement)?.DataContext as string);
		}
		private void OnSettingsButton_Tapped(object sender, TappedRoutedEventArgs e)
		{
			GoToSettingsRequested?.Invoke(this, EventArgs.Empty);
		}
		#endregion event handlers


		#region animations
		public enum Animations { Updating = 0, Success = 1, Failure = 2 }

		public void StartAnimation(int whichAnimation)
		{
			Task start = RunInUiThreadAsync(delegate
			{
				if ((Animations)whichAnimation == Animations.Updating) UpdatingStoryboard.Begin();
				else if ((Animations)whichAnimation == Animations.Success) SuccessStoryboard.Begin();
				else if ((Animations)whichAnimation == Animations.Failure) FailureStoryboard.Begin();
			});
		}
		public void EndAnimation(int whichAnimation)
		{
			Task end = RunInUiThreadAsync(delegate
			{
				Storyboard sb = null;
				if ((Animations)whichAnimation == Animations.Updating) sb = UpdatingStoryboard;
				else if ((Animations)whichAnimation == Animations.Success) sb = SuccessStoryboard;
				else if ((Animations)whichAnimation == Animations.Failure) sb = FailureStoryboard;

				sb?.SkipToFill();
				sb?.Stop();
			});
		}
		#endregion animations
	}
}