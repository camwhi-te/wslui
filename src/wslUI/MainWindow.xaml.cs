using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using wslUI.ViewModels;

namespace wslUI;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContextChanged += (_, _) =>
        {
            if (DataContext is not MainViewModel vm) return;
            ApplyLogRow(vm);
            vm.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(MainViewModel.LogVisible)) ApplyLogRow(vm);
            };
        };
        IsVisibleChanged += (_, _) =>
        {
            if (DataContext is MainViewModel vm) vm.IsWindowVisible = IsVisible;
        };
        StateChanged += (_, _) =>
        {
            if (DataContext is MainViewModel vm) vm.IsWindowVisible = WindowState != WindowState.Minimized && IsVisible;
        };
    }

    private void ApplyLogRow(MainViewModel vm)
    {
        if (vm.LogVisible)
        {
            LogRow.Height = new GridLength(Math.Clamp(vm.Settings.LogHeight, 90, 600));
            LogRow.MinHeight = 90;
        }
        else
        {
            if (LogRow.ActualHeight > 0) vm.Settings.LogHeight = LogRow.ActualHeight;
            LogRow.MinHeight = 0;
            LogRow.Height = GridLength.Auto;
        }
    }

    private void LogBox_TextChanged(object sender, TextChangedEventArgs e) => LogBox.ScrollToEnd();

    protected override void OnClosing(CancelEventArgs e)
    {
        if (DataContext is MainViewModel vm)
        {
            if (vm.LogVisible) vm.Settings.LogHeight = LogRow.ActualHeight;
            vm.Settings.Save();
            if (!App.Current.IsExiting && vm.Settings.CloseToTray)
            {
                e.Cancel = true;
                Hide();
                App.Current.ShowTrayHint();
                return;
            }
        }
        base.OnClosing(e);
        if (!App.Current.IsExiting) App.Current.ExitApp();
    }
}
