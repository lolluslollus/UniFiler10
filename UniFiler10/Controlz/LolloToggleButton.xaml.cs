using UniFiler10.Data.Runtime;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

// The User Control item template is documented at http://go.microsoft.com/fwlink/?LinkId=234236

namespace UniFiler10.Controlz
{
	public sealed partial class LolloToggleButton : UserControl
	{
		public bool IsChecked
		{
			get { return (bool)GetValue(IsCheckedProperty); }
			set { SetValue(IsCheckedProperty, value); }
		}
		public static readonly DependencyProperty IsCheckedProperty =
			DependencyProperty.Register("IsChecked", typeof(bool), typeof(LolloToggleButton), new PropertyMetadata(false, OnIsCheckedChanged));


		public string Uid
		{
			get { return (string)GetValue(UidProperty); }
			set { SetValue(UidProperty, value); }
		}
		public static readonly DependencyProperty UidProperty =
			DependencyProperty.Register("Uid", typeof(string), typeof(LolloToggleButton), new PropertyMetadata("", OnUidChanged));

		public Symbol Symbol
		{
			get { return (Symbol)GetValue(SymbolProperty); }
			set { SetValue(SymbolProperty, value); }
		}
		public static readonly DependencyProperty SymbolProperty =
			DependencyProperty.Register("Symbol", typeof(Symbol), typeof(LolloToggleButton), new PropertyMetadata(default(Symbol), OnSymbolChanged));


		public string CheckedUid
		{
			get { return (string)GetValue(CheckedUidProperty); }
			set { SetValue(CheckedUidProperty, value); }
		}
		public static readonly DependencyProperty CheckedUidProperty =
			DependencyProperty.Register("CheckedUid", typeof(string), typeof(LolloToggleButton), new PropertyMetadata("", OnUidChanged));

		public string UncheckedUid
		{
			get { return (string)GetValue(UncheckedUidProperty); }
			set { SetValue(UncheckedUidProperty, value); }
		}
		public static readonly DependencyProperty UncheckedUidProperty =
			DependencyProperty.Register("UncheckedUid", typeof(string), typeof(LolloToggleButton), new PropertyMetadata("", OnUidChanged));


		public Symbol CheckedSymbol
		{
			get { return (Symbol)GetValue(CheckedSymbolProperty); }
			set { SetValue(CheckedSymbolProperty, value); }
		}
		public static readonly DependencyProperty CheckedSymbolProperty =
			DependencyProperty.Register("CheckedSymbol", typeof(Symbol), typeof(LolloToggleButton), new PropertyMetadata(default(Symbol), OnSymbolChanged));

		public Symbol UncheckedSymbol
		{
			get { return (Symbol)GetValue(UncheckedSymbolProperty); }
			set { SetValue(UncheckedSymbolProperty, value); }
		}
		public static readonly DependencyProperty UncheckedSymbolProperty =
			DependencyProperty.Register("UncheckedSymbol", typeof(Symbol), typeof(LolloToggleButton), new PropertyMetadata(default(Symbol), OnSymbolChanged));


		public LolloToggleButton()
		{
			InitializeComponent();
		}

		private void UpdateSymbol()
		{
			if (CheckedSymbol == UncheckedSymbol)
			{
				MySymbolIconSmall.Symbol = MySymbolIconLarge.Symbol = Symbol;
			}
			else
			{
				if (IsChecked) MySymbolIconSmall.Symbol = MySymbolIconLarge.Symbol = CheckedSymbol;
				else MySymbolIconSmall.Symbol = MySymbolIconLarge.Symbol = UncheckedSymbol;
			}
		}
		private void UpdateText()
		{
			if (CheckedUid == UncheckedUid)
			{
				MyTextBlockSmall.Text = MyTextBlockLarge.Text = RuntimeData.GetText(Uid);
			}
			else
			{
				if (IsChecked) MyTextBlockSmall.Text = MyTextBlockLarge.Text = RuntimeData.GetText(CheckedUid);
				else MyTextBlockSmall.Text = MyTextBlockLarge.Text = RuntimeData.GetText(UncheckedUid);
			}
		}

		private static void OnSymbolChanged(DependencyObject obj, DependencyPropertyChangedEventArgs args)
		{
			if (args.NewValue != args.OldValue)
			{
				(obj as LolloToggleButton)?.UpdateSymbol();
			}
		}

		private static void OnUidChanged(DependencyObject obj, DependencyPropertyChangedEventArgs args)
		{
			if (args.NewValue != args.OldValue)
			{
				(obj as LolloToggleButton)?.UpdateText();
			}
		}

		private static void OnIsCheckedChanged(DependencyObject obj, DependencyPropertyChangedEventArgs args)
		{
			if (args.NewValue != args.OldValue)
			{
				(obj as LolloToggleButton)?.UpdateSymbol();
				(obj as LolloToggleButton)?.UpdateText();
			}
		}
	}
}