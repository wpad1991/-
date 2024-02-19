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
using System.Windows.Threading;

namespace Prost.Controls
{
    /// <summary>
    /// Interaction logic for UserControl1.xaml
    /// </summary>
    public partial class LogBox : Grid
    {
        StringBuilder qwe = new StringBuilder();

        StockData.DataStruct.FixedSizedQueue<string> LogEventQueue = new StockData.DataStruct.FixedSizedQueue<string>(200);

        public int MaxSize { get => LogEventQueue.Size; set => LogEventQueue.Size = value; }

        public string Filter
        {
            get { return lb_Title.Content + ""; }
            set { lb_Title.Content = value; }
        }

        public LogBox()
        {
            InitializeComponent();

            StockLog.Logger.LOG.MessageChanged += LOG_MessageChanged;
        }


        private void LOG_MessageChanged(string group, string msg)
        {
            Dispatcher.BeginInvoke(DispatcherPriority.Normal, new Action(delegate
            {
                if (lb_Title.Content + "" == group)
                {
                    LogEventQueue.Enqueue(msg);
                    TextBoxAdd();
                }
            }));
        }

        private void TextBoxAdd()
        {
            StringBuilder text = new StringBuilder();

            string[] logList = LogEventQueue.ToArray();

            if (logList != null)
            {
                for (int i = logList.Length - 1; i >= 0; i--)
                {
                    text.AppendLine(logList[i]);
                }
            }

            tb_Main.Text = text.ToString();

        }

    }
}
