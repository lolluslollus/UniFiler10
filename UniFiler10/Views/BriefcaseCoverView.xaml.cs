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
using Windows.ApplicationModel.Core;
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
	public sealed partial class BriefcaseCoverView : OpenableObservableControl
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

		private AnimationStarter _animationStarter = null;
		#endregion properties


		#region construct, dispose, open, close
		public BriefcaseCoverView()
		{
			InitializeComponent();
			_animationStarter = AnimationsControl.AnimationStarter;
			// _animationStarter = new AnimationStarter(new Storyboard[] { UpdatingStoryboard, SuccessStoryboard, FailureStoryboard});
		}
		protected override Task OpenMayOverrideAsync()
		{
			return AnimationsControl.OpenAsync();
		}
		protected override Task CloseMayOverrideAsync()
		{
			//_animationStarter.EndAllAnimations();
			return AnimationsControl.CloseAsync();
			//;			return Task.CompletedTask;
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
				if (isOk) _animationStarter.StartAnimation(AnimationStarter.Animations.Success);
				else _animationStarter.StartAnimation(AnimationStarter.Animations.Failure);
			}
		}

		private async void OnImportButton_Tapped(object sender, TappedRoutedEventArgs e)
		{
			var vm = VM;
			if (vm != null)
			{
				bool isOk = await vm.ImportDbAsync();
				if (isOk) _animationStarter.StartAnimation(AnimationStarter.Animations.Success);
				else _animationStarter.StartAnimation(AnimationStarter.Animations.Failure);
			}
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
	}
}