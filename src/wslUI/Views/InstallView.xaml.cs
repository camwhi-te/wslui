using System.Windows;
using System.Windows.Controls;
using wslUI.ViewModels;

namespace wslUI.Views;

public partial class InstallView : UserControl
{
    public InstallView() => InitializeComponent();

    private void Pwd_PasswordChanged(object sender, RoutedEventArgs e)
    {
        if (DataContext is InstallPageViewModel vm) vm.Password = Pwd.Password;
    }
}
