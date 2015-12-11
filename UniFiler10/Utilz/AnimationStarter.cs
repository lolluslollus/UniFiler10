using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.ApplicationModel.Core;
using Windows.UI.Core;
using Windows.UI.Xaml.Media.Animation;

namespace Utilz
{
	public class AnimationStarter
	{
		private Storyboard[] _animations = null;
		public AnimationStarter(params Storyboard[] animations)
		{
			if (animations == null) throw new ArgumentNullException("AnimationStarter: null parameter animations passed to ctor");
			_animations = animations;
		}
		public void StartAnimation(int whichAnimation)
		{
			Task start = RunInUiThreadAsync(delegate
			{
				if (_animations.GetUpperBound(0) >= whichAnimation)
				{
					Storyboard sb = _animations[whichAnimation];
					sb?.Begin();
				}
			});
		}
		public void EndAnimation(int whichAnimation)
		{
			Task end = RunInUiThreadAsync(delegate
			{
				if (_animations.GetUpperBound(0) >= whichAnimation)
				{
					Storyboard sb = _animations[whichAnimation];
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
