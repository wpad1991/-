using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace Prost.Controls.Pages
{
    /// <summary>
    /// Interaction logic for InforPage.xaml
    /// </summary>
    public partial class InfoPage : Page
    {
        public InfoPage()
        {
            InitializeComponent();
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            lb_mainacc.Content = StockData.Singleton.Store.Account.MainAccount;
            lb_id.Content = StockData.Singleton.Store.Account.ID;
            lb_name.Content = StockData.Singleton.Store.Account.Name;
        }
    }
}
