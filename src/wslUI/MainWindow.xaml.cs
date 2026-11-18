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
        Loaded += (_, _) =>
        {
            if (DataContext is MainViewModel vm) LogRow.Height = new GridLength(Math.Max(60, vm.Settings.LogHeight));
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

    private void LogBox_TextChanged(object sender, TextChangedEventArgs e) => LogBox.ScrollToEnd();

    protected override void OnClosing(CancelEventArgs e)
    {
        if (DataContext is MainViewModel vm)
        {
            vm.Settings.LogHeight = LogRow.ActualHeight;
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
