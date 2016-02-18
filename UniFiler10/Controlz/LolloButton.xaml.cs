using UniFiler10.Data.Runtime;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;

// The User Control item template is documented at http://go.microsoft.com/fwlink/?LinkId=234236

namespace UniFiler10.Controlz
{
	public sealed partial class LolloButton : UserControl
	{
		public string Uid
		{
			get { return (string)GetValue(UidProperty); }
			set { SetValue(UidProperty, value); }
		}
		public static readonly DependencyProperty UidProperty =
			DependencyProperty.Register("Uid", typeof(string), typeof(LolloButton), new PropertyMetadata("", OnUidChanged));
		private static void OnUidChanged(DependencyObject obj, DependencyPropertyChangedEventArgs args)
		{
			if (args.NewValue != args.OldValue)
			{
				var instance = obj as LolloButton;
				if (instance != null)
				{
					instance.MyTextBlockSmall.Text = instance.MyTextBlockLarge.Text = RuntimeData.GetText(instance.Uid);
				}
			}
		}

		public Symbol Symbol
		{
			get { return (Symbol)GetValue(SymbolProperty); }
			set { SetValue(SymbolProperty, value); }
		}
		public static readonly DependencyProperty SymbolProperty =
			DependencyProperty.Register("Symbol", typeof(Symbol), typeof(LolloButton), new PropertyMetadata(default(Symbol), OnSymbolChanged));
		private static void OnSymbolChanged(DependencyObject obj, DependencyPropertyChangedEventArgs args)
		{
			if (args.NewValue != args.OldValue)
			{
				var instance = obj as LolloButton;
				if (instance != null)
				{
					instance.MySymbolIconSmall.Symbol = instance.MySymbolIconLarge.Symbol = instance.Symbol;
				}
			}
		}

		public LolloButton()
		{
			InitializeComponent();
		}

		private void OnMyButton_Tapped(object sender, TappedRoutedEventArgs e)
		{
			// see if it propagates
		}
	}
}
