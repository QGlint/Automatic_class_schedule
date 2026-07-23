using System.Windows;
using System.Windows.Input;
using Automatic_class_schedule.Models;
using Automatic_class_schedule.ViewModels;

namespace Automatic_class_schedule;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainViewModel();
    }

    private void LessonCard_MouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }

        if (sender is not FrameworkElement element || element.DataContext is not ScheduleEntry entry)
        {
            return;
        }

        DragDrop.DoDragDrop(element, entry, DragDropEffects.Move);
    }

    private void PeriodBlock_Drop(object sender, DragEventArgs e)
    {
        if (sender is not FrameworkElement element || element.DataContext is not ScheduleCellViewModel cell)
        {
            return;
        }

        if (DataContext is not MainViewModel viewModel || !e.Data.GetDataPresent(typeof(ScheduleEntry)))
        {
            return;
        }

        if (e.Data.GetData(typeof(ScheduleEntry)) is ScheduleEntry entry)
        {
            viewModel.MoveEntry(entry, cell.DayIndex, cell.PeriodIndex);
            e.Effects = DragDropEffects.Move;
            e.Handled = true;
        }
    }

    private void LessonCard_Drop(object sender, DragEventArgs e)
    {
        if (sender is not FrameworkElement element || element.DataContext is not ScheduleEntry target)
        {
            return;
        }

        if (DataContext is not MainViewModel viewModel || !e.Data.GetDataPresent(typeof(ScheduleEntry)))
        {
            return;
        }

        if (e.Data.GetData(typeof(ScheduleEntry)) is ScheduleEntry source && source.Id != target.Id)
        {
            viewModel.SwapEntries(source, target);
            e.Effects = DragDropEffects.Move;
            e.Handled = true;
        }
    }
}
