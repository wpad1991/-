using Microsoft.Win32;
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
    /// Interaction logic for BackTest.xaml
    /// </summary>
    public partial class BackTest : Page
    {
        public event ButtonClickDelegate buttonClicked = null;
        //public Prost.PythonManager.PYM pym = new PythonManager.PYM();
        string recentFile = "";

        public BackTest()
        {
            InitializeComponent();

            //pym.DefaultFolder = System.Windows.Forms.Application.StartupPath + "\\py";
            //pym.Init();
        }


        private void Button_Click(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.Button btn)
            {
                buttonClicked?.Invoke(this.Title, btn.Tag + "");
            }
        }

        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            //pym.ExcuteTest(tb_python.Text);
        }

        private void Button_Click_2(object sender, RoutedEventArgs e)
        {
            //System.Windows.Forms.OpenFileDialog openFileDialog = new System.Windows.Forms.OpenFileDialog();
            //openFileDialog.InitialDirectory = pym.DefaultFolder;

            //if (!System.IO.Directory.Exists(pym.DefaultFolder))
            //{
            //    System.IO.Directory.CreateDirectory(pym.DefaultFolder);
            //}


            //if (openFileDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            //{
            //    try
            //    {
            //        tb_python.Text = System.IO.File.ReadAllText(openFileDialog.FileName);
            //        recentFile = openFileDialog.FileName;
            //    }
            //    catch (Exception except)
            //    {
            //        StockLog.Logger.LOG.WriteLog("Exception", except.ToString());
            //    }
            //}
        }

        private void Button_Click_3(object sender, RoutedEventArgs e)
        {
            //try
            //{
            //    tb_python.Text = System.IO.File.ReadAllText(recentFile);
            //}
            //catch (Exception except)
            //{
            //    StockLog.Logger.LOG.WriteLog("Exception", except.ToString());
            //}
        }
    }
}
