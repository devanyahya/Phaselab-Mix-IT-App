// MainWindow.xaml.cs
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace MixItControllerApp
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            // Panggilan ini harus ada, jangan dihapus atau dibuat manual
            InitializeComponent();
            
            DataContext = new MainViewModel();
        }

        // Method ini harus ada untuk menangani event dari XAML
        private void Slider_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is MainViewModel vm)
            {
                vm.PausePolling();
            }
        }

        // Method ini juga harus ada untuk menangani event dari XAML
        private async void Slider_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is MainViewModel vm && sender is Slider slider && slider.DataContext is MixerChannel channel)
            {
                await vm.EndUiInteraction(channel);
            }
        }
    }
}