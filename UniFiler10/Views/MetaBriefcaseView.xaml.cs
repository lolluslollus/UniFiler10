using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using UniFiler10.Data.Metadata;
using UniFiler10.ViewModels;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

// The User Control item template is documented at http://go.microsoft.com/fwlink/?LinkId=234236

namespace UniFiler10.Views
{
	public sealed partial class MetaBriefcaseView : UserControl
	{
		public SettingsVM VM
		{
			get { return (SettingsVM)GetValue(VMProperty); }
			set { SetValue(VMProperty, value); }
		}
		public static readonly DependencyProperty VMProperty =
			DependencyProperty.Register("VM", typeof(SettingsVM), typeof(MetaBriefcaseView), new PropertyMetadata(null));


		public MetaBriefcaseView()
		{
			InitializeComponent();
		}
		private void OnCategoryListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			VM?.UpdateCurrentCategory((sender as ListView)?.SelectedItem as Category);
		}
		private void OnUnassignedFieldDescriptionsListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			var fldDsc = (sender as ListView)?.SelectedItem as FieldDescription;

			if (fldDsc != null)
			{
				VM?.UpdateCurrentFieldDescription(fldDsc);
				AssignedLV.DeselectRange(new ItemIndexRange(AssignedLV.SelectedIndex, 1));
			}
		}
		private void OnAssignedFieldDescriptionsListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			var fldDsc = (sender as ListView)?.SelectedItem as FieldDescription;
			if (fldDsc != null)
			{
				VM?.UpdateCurrentFieldDescription(fldDsc);
				UnassignedLV.DeselectRange(new ItemIndexRange(UnassignedLV.SelectedIndex, 1));
			}
		}

		private void OnAssignFieldToCat_Click(object sender, RoutedEventArgs e)
		{
			var fldDsc = (sender as FrameworkElement)?.DataContext as FieldDescription;
			VM?.AssignFieldDescriptionToCategory(fldDsc, VM?.MetaBriefcase?.CurrentCategory);
		}
		private void OnUnassignFieldFromCat_Click(object sender, RoutedEventArgs e)
		{
			var fldDsc = (sender as FrameworkElement)?.DataContext as FieldDescription;
			VM?.UnassignFieldDescriptionFromCategory(fldDsc, VM?.MetaBriefcase?.CurrentCategory);
		}

		private void OnAddField_Click(object sender, RoutedEventArgs e)
		{
			Task add = VM?.AddFieldDescriptionAsync();
		}

		private void OnDeleteField_Click(object sender, RoutedEventArgs e)
		{
			var fldDsc = (sender as FrameworkElement)?.DataContext as FieldDescription;
			Task del = VM?.RemoveFieldDescriptionAsync(fldDsc);
		}

		private void OnAddFieldValue_Click(object sender, RoutedEventArgs e)
		{
			VM?.AddPossibleValueToFieldDescription();
		}
		private void OnDeletePossibleValue_Click(object sender, RoutedEventArgs e)
		{
			var fldVal = (sender as FrameworkElement)?.DataContext as FieldValue;
			VM?.RemovePossibleValueFromFieldDescription(fldVal);
		}

		private void OnAddCategory_Click(object sender, RoutedEventArgs e)
		{
			Task add = VM.AddCategoryAsync();
		}

		private async void OnDeleteCategory_Click(object sender, RoutedEventArgs e)
		{
			var cat = (sender as FrameworkElement)?.DataContext as Category;
			if (VM != null) await VM.RemoveCategoryAsync(cat).ConfigureAwait(false);
		}

		private void OnUnassignFieldFromCat_Loaded(object sender, RoutedEventArgs e)
		{
			var fldDsc = (sender as FrameworkElement)?.DataContext as FieldDescription;
			string currCatId = MetaBriefcase.OpenInstance?.CurrentCategory?.Id;

			if (fldDsc != null && !string.IsNullOrEmpty(currCatId) && fldDsc.JustAssignedToCats?.Contains(currCatId) == true)
				(sender as FrameworkElement).Visibility = Visibility.Visible;
			else
				(sender as FrameworkElement).Visibility = Visibility.Collapsed;
		}
	}
}
