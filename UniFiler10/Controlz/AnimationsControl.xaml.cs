using System;
using System.Threading.Tasks;
using Utilz;
using Utilz.Controlz;
using Windows.ApplicationModel.Core;
using Windows.UI.Core;
using Windows.UI.Xaml.Media.Animation;

// The User Control item template is documented at http://go.microsoft.com/fwlink/?LinkId=234236

namespace UniFiler10.Controlz
{
	public sealed partial class AnimationsControl : OpenableObservableControl
	{
		#region properties
		private AnimationStarter _animationStarter = null;
		public AnimationStarter AnimationStarter { get { return _animationStarter; } }
		#endregion properties

		public AnimationsControl()
		{
			InitializeComponent();
			_animationStarter = new AnimationStarter(new Storyboard[] { UpdatingStoryboard, SuccessStoryboard, FailureStoryboard });
		}

		protected override Task CloseMayOverrideAsync()
		{
			_animationStarter.EndAllAnimations();
			return Task.CompletedTask;
		}
	}

	public class AnimationStarter
	{
		public enum Animations { Updating, Success, Failure }
		private Storyboard[] _animations = null;

		internal AnimationStarter(params Storyboard[] animations)
		{
			if (animations == null) throw new ArgumentNullException("AnimationStarter: null parameter animations passed to ctor");
			_animations = animations;
		}
		public void StartAnimation(Animations whichAnimation)
		{
			Task start = RunInUiThreadAsync(delegate
			{
				if (_animations.GetUpperBound(0) >= (int)whichAnimation && (int)whichAnimation >= 0)
				{
					// Storyboard sb = _animations[whichAnimation];
					Storyboard sb = _animations[(int)whichAnimation];
					sb?.Begin();
				}
			});
		}
		public void EndAnimation(Animations whichAnimation)
		{
			Task end = RunInUiThreadAsync(delegate
			{
				if (_animations.GetUpperBound(0) >= (int)whichAnimation && (int)whichAnimation >= 0)
				{
					//Storyboard sb = _animations[whichAnimation];
					Storyboard sb = _animations[(int)whichAnimation];
					sb?.SkipToFill();
					sb?.Stop();
				}
			});
		}
		public void EndAllAnimations()
		{
			Task end = RunInUiThreadAsync(delegate
			{
				foreach (var anim in _animations)
				{
					anim.SkipToFill();
					anim.Stop();
				}
			});
		}
		private async Task RunInUiThreadAsync(DispatchedHandler action)
		{
			try
			{
				if (CoreApplication.MainView.CoreWindow.Dispatcher.HasThreadAccess)
				{
					action();
				}
				else
				{
					await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, action).AsTask().ConfigureAwait(false);
				}
			}
			catch (Exception ex)
			{
				Logger.Add_TPL(ex.ToString(), Logger.PersistentDataLogFilename);
			}
		}
	}
}