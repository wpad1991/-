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
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    /// 

    public delegate void ButtonClickDelegate(string group, string command);

    public partial class MainWindow : Window
    {
        public AdministratorPage administratorPage = new AdministratorPage();
        public MessagePage messagePage = new MessagePage();
        public TablePage tablePage = new TablePage();
        public QueryPage queryPage = new QueryPage();
        public InfoPage infoPage = new InfoPage();
        public BackTest backTestPage = new BackTest();

        public MainWindow()
        {
            InitializeComponent();

            this.Loaded += MainWindow_Loaded;

            main_Frame.Navigated += Main_Frame_Navigated;
            main_Frame.NavigationUIVisibility = NavigationUIVisibility.Hidden;
        }

        private void Main_Frame_Navigated(object sender, NavigationEventArgs e)
        {
            //Button_MainView.Tag = "0";
            //Button_EventView.Tag = "0";
            //Button_AlarmView.Tag = "0";
            //Button_TackView.Tag = "0";
            //Button_AlarmEdit.Tag = "0";
            //Button_Config.Tag = "0";

            //if (object.Equals(Main_Frame.Content, mainView))
            //{
            //    Button_MainView.Tag = "1";
            //}
            //else if (object.Equals(Main_Frame.Content, eventView))
            //{
            //    Button_EventView.Tag = "1";
            //}
            //else if (object.Equals(Main_Frame.Content, alarmView))
            //{
            //    Button_AlarmView.Tag = "1";
            //}
            //else if (object.Equals(Main_Frame.Content, tackView))
            //{
            //    Button_TackView.Tag = "1";
            //}
            //else if (object.Equals(Main_Frame.Content, alarmEdit))
            //{
            //    Button_AlarmEdit.Tag = "1";
            //}
            //else if (object.Equals(Main_Frame.Content, config))
            //{
            //    Button_Config.Tag = "1";
            //}
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            main_Frame.Navigate(messagePage);
        }

        private void btn_Navi_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btnObject)
            {
                switch (Grid.GetRow(btnObject)) {
                    case 0:
                        main_Frame.Navigate(messagePage);
                        break;
                    case 1:
                        main_Frame.Navigate(tablePage);
                        break;
                    case 2:
                        main_Frame.Navigate(queryPage);
                        break;
                    case 3:
                        main_Frame.Navigate(administratorPage);
                        break;
                    case 4:
                        main_Frame.Navigate(infoPage);
                        break;
                    case 5:
                        main_Frame.Navigate(backTestPage);
                        break;
                }
            }
        }
    }
}
