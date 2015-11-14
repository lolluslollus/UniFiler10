using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace UniFiler10.Controlz
{
	public class LolloComboBox : ComboBox
	{
		private void rrr()
		{
			this.FindName("ContentPresenter");
		}
		protected override Size MeasureOverride(Size availableSize)
		{
			var test = base.MeasureOverride(availableSize);
			var cp = FindName("ContentPresenter");
			return test;
		}
		protected override Size ArrangeOverride(Size finalSize)
		{
			var test = base.ArrangeOverride(finalSize);
			return new Size(48, test.Height);
			// return test;
		}
		protected override DependencyObject GetContainerForItemOverride()
		{
			var test = base.GetContainerForItemOverride();
			return test;
		}
		protected override void PrepareContainerForItemOverride(DependencyObject element, object item)
		{
			base.PrepareContainerForItemOverride(element, item);
		}
	}
}
