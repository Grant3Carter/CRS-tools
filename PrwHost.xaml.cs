using System.Windows;

namespace CRS
{
    /// <summary>
    /// Interaction logic for Window1.xaml
    /// </summary>
    public partial class PrwHost : Window
    {
        public PrwHost()
        {
            InitializeComponent();
        }


        private void PrwHost_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
           MainWindow.Prw = null;
        }
    }
}
