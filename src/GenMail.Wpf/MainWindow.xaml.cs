using System.Windows;
using GenMail.Wpf.ViewModels;

namespace GenMail.Wpf;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainViewModel();
    }
}
