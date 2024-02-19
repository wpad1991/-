using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Prost
{
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            Thread.Sleep(5000);
            StockLog.Logger.LOG.WriteLog("System", "Set Process");
            IsExistProcess();

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            ProstForm form = new ProstForm();
            form.ExitEvent += Form_ExitEvent;
            form.RestartEvent += Form_RestartEvent;
            Application.Run(form);
            //else
            //{
            //    StockLog.Logger.LOG.WriteLog("System", "alreay process on...");
            //    Form_RestartEvent();
            //}
        }

        private static void Form_ExitEvent()
        {
            //StockLog.Logger.LOG.WriteLog("System", GetStackTrade());
            StockLog.Logger.LOG.WriteLog("System", "Invoke Exit");
            StockData.Singleton.Store.QueryLogSend("System", "Excute Exit Event.");
            Application.Exit();
        }

        private static void Form_RestartEvent()
        {
            //StockLog.Logger.LOG.WriteLog("System", GetStackTrade());
            StockData.Singleton.Store.QueryLogSend("System", "Excute Restart Event.");
            StockLog.Logger.LOG.WriteLog("System", "Invoke Restart");
            Application.Restart();
        }

        static void IsExistProcess() 
        {
            foreach (Process process in Process.GetProcessesByName(Process.GetCurrentProcess().ProcessName))
            {
                Console.WriteLine("----------");
                Console.WriteLine(process.ProcessName);
                Console.WriteLine(Process.GetCurrentProcess().ProcessName);


                if (process.Id == Process.GetCurrentProcess().Id) 
                {
                    continue;
                }

                if (process.ProcessName == Process.GetCurrentProcess().ProcessName)
                {
                    process.Kill();
                }
                 
            }
        }

        static string GetStackTrade()
        {
            StringBuilder sb = new StringBuilder();

            int count = new StackTrace().FrameCount;
            StackTrace stacktrace = new StackTrace();
            MethodBase method = null;

            for (int i = 0; i < count; i++)
            {
                method = stacktrace.GetFrame(i).GetMethod();
                sb.Append(i);
                sb.Append(": ");
                sb.Append(method.Name);
                sb.Append("\n");
            }

            return sb.ToString();
        }
    }
}
