using System.Windows;
using System.Windows.Controls;
using wslUI.ViewModels;

namespace wslUI.Views;

public partial class ConsoleView : UserControl
{
    public ConsoleView() => InitializeComponent();

    private void OutputBox_TextChanged(object sender, TextChangedEventArgs e) => OutputBox.ScrollToEnd();

    private void UseDefault_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is ConsolePageViewModel vm) vm.ClearDistro();
    }
}
