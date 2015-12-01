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
		//private void OnCategoryListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
		//{
		//	Task upd = VM?.UpdateCurrentCategoryAsync((sender as ListView)?.SelectedItem as Category);
		//}
		private void OnCategoryListView_ItemClick(object sender, ItemClickEventArgs e)
		{
			Task upd = VM?.SetCurrentCategoryAsync(e.ClickedItem as Category);
		}

		//private async void OnUnassignedFieldDescriptionsListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
		//{
		//	var fldDsc = (sender as ListView)?.SelectedItem as FieldDescription;
		//	var vm = VM;
		//	if (fldDsc != null && vm != null)
		//	{
		//		await vm.UpdateCurrentFieldDescriptionAsync(fldDsc);
		//		AssignedLV.DeselectRange(new ItemIndexRange(AssignedLV.SelectedIndex, 1));
		//	}
		//}
		private async void OnUnassignedFieldDescriptionsListView_ItemClick(object sender, ItemClickEventArgs e)
		{
			var vm = VM;
			if (vm != null)
			{
				await vm.SetCurrentFieldDescriptionAsync(e.ClickedItem as FieldDescription);
				AssignedLV.DeselectRange(new ItemIndexRange(AssignedLV.SelectedIndex, 1));
			}
		}

		//private async void OnAssignedFieldDescriptionsListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
		//{
		//	var fldDsc = (sender as ListView)?.SelectedItem as FieldDescription;
		//	var vm = VM;
		//	if (fldDsc != null && vm != null)
		//	{
		//		await vm.UpdateCurrentFieldDescriptionAsync(fldDsc);
		//		UnassignedLV.DeselectRange(new ItemIndexRange(UnassignedLV.SelectedIndex, 1));
		//	}
		//}
		private async void OnAssignedFieldDescriptionsListView_ItemClick(object sender, ItemClickEventArgs e)
		{
			var vm = VM;
			if (vm != null)
			{
				await vm.SetCurrentFieldDescriptionAsync(e.ClickedItem as FieldDescription);
				UnassignedLV.DeselectRange(new ItemIndexRange(UnassignedLV.SelectedIndex, 1));
			}
		}

		private void OnAssignFieldToCurrentCat_Click(object sender, RoutedEventArgs e)
		{
			Task ass = VM?.AssignFieldDescriptionToCurrentCategoryAsync((sender as FrameworkElement)?.DataContext as FieldDescription);
		}
		private void OnUnassignFieldFromCurrentCategory_Click(object sender, RoutedEventArgs e)
		{
			Task una = VM?.UnassignFieldDescriptionFromCurrentCategoryAsync((sender as FrameworkElement)?.DataContext as FieldDescription);
		}

		private void OnAddField_Click(object sender, RoutedEventArgs e)
		{
			Task add = VM?.AddFieldDescriptionAsync();
		}

		private void OnDeleteField_Click(object sender, RoutedEventArgs e)
		{
			Task del = VM?.RemoveFieldDescriptionAsync((sender as FrameworkElement)?.DataContext as FieldDescription);
		}

		private void OnAddFieldValue_Click(object sender, RoutedEventArgs e)
		{
			Task add = VM?.AddPossibleValueToCurrentFieldDescriptionAsync();
		}
		private void OnDeletePossibleValue_Click(object sender, RoutedEventArgs e)
		{
			Task del = VM?.RemovePossibleValueFromCurrentFieldDescriptionAsync((sender as FrameworkElement)?.DataContext as FieldValue);
		}

		private void OnAddCategory_Click(object sender, RoutedEventArgs e)
		{
			Task add = VM?.AddCategoryAsync();
		}

		private void OnDeleteCategory_Click(object sender, RoutedEventArgs e)
		{
			Task del = VM?.RemoveCategoryAsync((sender as FrameworkElement)?.DataContext as Category);
		}

		private void OnUnassignFieldFromCat_Loaded(object sender, RoutedEventArgs e)
		{
			var fldDsc = (sender as FrameworkElement)?.DataContext as FieldDescription;
			string currCatId = MetaBriefcase.OpenInstance?.CurrentCategory?.Id;

			if (fldDsc != null && !string.IsNullOrEmpty(currCatId) && (fldDsc.JustAssignedToCats?.Contains(currCatId) == true || SettingsVM.GetIsElevated()))
				(sender as FrameworkElement).Visibility = Visibility.Visible;
			else
				(sender as FrameworkElement).Visibility = Visibility.Collapsed;
		}
	}
}
