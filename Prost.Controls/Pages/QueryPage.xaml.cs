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
    /// Interaction logic for QueryPage.xaml
    /// </summary>
    public partial class QueryPage : Page
    {
        
        public QueryPage()
        {
            InitializeComponent();

            
        }

        private void btn_Query_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                DBManager.MariaClient mariaDB = new DBManager.MariaClient("", "", "", "", "");
                if (mariaDB.Connect())
                {
                    dg_Query.ItemsSource = mariaDB.Execute("select code from tb_stock_price_day where code='000020'").DefaultView;
                }
                //dg_Query.ItemsSource = mariaDB.Execute(tb_Query.Text).DefaultView;
            }
            catch (Exception exception)
            {
                StockLog.Logger.LOG.WriteLog("Exception", exception.ToString());
            }
        }
    }
}
