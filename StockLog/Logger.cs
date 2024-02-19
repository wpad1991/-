using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Xml;
using log4net;
using log4net.Config;

namespace StockLog
{

    public delegate void MessageChangedHandler(string group, string log);

    public class Logger
    {
        static Dictionary<string, Dictionary<string, ILog>> LogDic = new Dictionary<string, Dictionary<string, ILog>>();

        public List<string> logFilter = new List<string>();
        public event MessageChangedHandler MessageChanged = null;
        static Logger logger;

        static string defaultFolder = "LogData";

        static string logPath = "";

        static string fullPath = "";

        static int logSaveDay = 7;

        public static Logger LOG
        {
            get { return logger; }
        }

        public static string LogDirectory
        {
            get { return logPath; }
            set
            {
                logPath = value;
            }
        } 

        public static int LogSaveDay { get => logSaveDay; set => logSaveDay = value; }

        public static List<string> LogFilter 
        { 
            get 
            {
                return LogFilter; 
            }
        }

        static Logger()
        {
            logger = new Logger();
            Configuration();

            ThreadPool.QueueUserWorkItem(_ => {
                while (true)
                {
                    RemoveLog();
                    Thread.Sleep(1000 * 60 * 60);
                }
            });
        }

        public static void Configuration()
        {
            if (logPath == "")
            {
                fullPath = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory) + Path.DirectorySeparatorChar + defaultFolder + Path.DirectorySeparatorChar;
            }
            else
            {
                fullPath = Path.Combine(logPath, defaultFolder);
            }
        }

        /// <summary>
        /// Open Xml File and Add LogDic Dictionary
        /// </summary>
        /// <param name="logName">Use Index String</param>
        /// <param name="filePath">FilePath</param>
        /// <param name="fileName">FileName</param>
        /// <returns>bool</returns>
        public bool OpenXml(string logName, string filePath, string fileName)
        {

            if (LogDic.ContainsKey(logName))
            {
                return false;
            }

            XmlNode xmlNode = ReadXmlFileToNode(filePath, fileName);

            if (xmlNode != null)
            {
                Dictionary<string, ILog> logbuf = new Dictionary<string, ILog>();
                log4net.Config.XmlConfigurator.Configure(new System.IO.FileInfo(filePath + Path.DirectorySeparatorChar + fileName));
                foreach (XmlNode node in xmlNode.SelectNodes("logger"))
                {
                    ILog iLog = LogManager.GetLogger(node.Attributes["name"].Value);
                    iLog.Info(">>>>>>>>>> Log Write Start <<<<<<<<<<");
                    logbuf.Add(iLog.Logger.Name, iLog);
                }

                LogDic.Add(logName, logbuf);

            }
            else
            {
                return false;
            }


            return true;
        }
        /// <summary>
        /// Add LogDic Dictionary
        /// </summary>
        /// <param name="logName"></param>
        /// <param name="root"></param>
        /// <returns></returns>
        public bool AddRootXmlNode(string logName, XmlNode root)
        {
            try
            {
                if (logName == "" || root == null)
                {
                    return false;
                }

                XmlDocument xmlDoc = new XmlDocument();

                XmlElement element = null;

                xmlDoc.LoadXml(root.OuterXml);

                element = xmlDoc.DocumentElement;
                Dictionary<string, ILog> logbuf = new Dictionary<string, ILog>();
                if (LogDic.ContainsKey(logName))
                {
                    logbuf.Clear();

                    logbuf = LogDic[logName];
                }

                log4net.Config.XmlConfigurator.Configure(element);

                foreach (XmlNode node in root.SelectNodes("logger"))
                {
                    ILog iLog = LogManager.GetLogger(node.Attributes["name"].Value);
                    iLog.Info(">>>>>>>>>> Log Write Start <<<<<<<<<<");

                    if (!logbuf.ContainsKey(iLog.Logger.Name))
                    {
                        logbuf.Add(iLog.Logger.Name, iLog);
                    }
                }
                if (LogDic.ContainsKey(logName))
                {
                    LogDic[logName] = logbuf;
                }
                else
                {
                    LogDic.Add(logName, logbuf);
                }
            }
            catch (Exception except)
            {
                Console.WriteLine(except.ToString());
                return false;
            }


            return true;
        }
        /// <summary>
        /// Add LogDic Dictionary
        /// </summary>
        /// <param name="logName"></param>
        /// <param name="root"></param>
        /// <returns></returns>
        public bool AddRootXmlNode(string logName, XmlElement root)
        {
            try
            {
                if (logName == "" || root == null)
                {
                    return false;
                }

                Dictionary<string, ILog> logbuf = new Dictionary<string, ILog>();
                log4net.Config.XmlConfigurator.Configure(root);
                foreach (XmlNode node in root.SelectNodes("logger"))
                {
                    ILog iLog = LogManager.GetLogger(node.Attributes["name"].Value);
                    iLog.Info("Log Write Start <<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<");
                    logbuf.Add(iLog.Logger.Name, iLog);
                }

                LogDic.Add(logName, logbuf);
            }
            catch (Exception except)
            {
                Console.WriteLine(except.ToString());
                return false;
            }


            return true;
        }
        /// <summary>
        /// Read XML and return Node
        /// </summary>
        /// <param name="filePath">filePath</param>
        /// <param name="fileName">fileName</param>
        /// <returns></returns>
        public XmlNode ReadXmlFileToNode(string filePath, string fileName)
        {

            XmlDocument xmlDoc = new XmlDocument();
            XmlNode xmlNode = null;

            DirectoryInfo dir = new DirectoryInfo(filePath);

            if (dir.Exists == true)
            {

                FileInfo[] files = dir.GetFiles("*.xml");

                foreach (FileInfo file in files)
                {
                    if (file.Name == fileName)
                    {
                        xmlDoc.Load(filePath + Path.DirectorySeparatorChar + fileName);

                        xmlNode = xmlDoc.SelectSingleNode("log4net");

                    }
                }
            }

            return xmlNode;
        }
        /// <summary>
        /// GetDefaultNode
        /// </summary>
        /// <returns></returns>
        public XmlNode GetDefaultNode(string logGroup, string appenderName, string loggerName)
        {
            XmlDocument xmlDoc = new XmlDocument();
            XmlAttribute attr = null;
            XmlNode xmlNode = null;
            xmlNode = xmlDoc.CreateXmlDeclaration("1.0", "UTF-8", null);
            xmlDoc.AppendChild(xmlNode);

            xmlNode = xmlDoc.CreateElement("log4net");

            attr = xmlDoc.CreateAttribute(@"xmlns:xsi");
            attr.Value = "http://www.w3.org/2001/XMLSchema-instance";
            xmlNode.Attributes.Append(attr);

            attr = xmlDoc.CreateAttribute(@"xmlns:xsd");
            attr.Value = "http://www.w3.org/2001/XMLSchema";
            xmlNode.Attributes.Append(attr);


            xmlDoc.AppendChild(xmlNode);

            xmlNode.Attributes.Append(attr);


            xmlDoc.AppendChild(xmlNode);

            xmlDoc.ChildNodes[1].AppendChild(GetDefaultAppnderNode(xmlDoc, logGroup, appenderName));

            xmlDoc.ChildNodes[1].AppendChild(GetDefaultLoggerNode(xmlDoc, appenderName, loggerName));

            return xmlNode;
        }

        public XmlNode GetDefaultAppnderNode(XmlDocument xmlDoc, string logGroup, string appenderName)
        {
            XmlAttribute attr = null;
            XmlNode xmlNode = null;
            XmlNode xmlNodechild = null;
            XmlNode xmlNodechild2 = null;


            //appender
            xmlNode = xmlDoc.CreateElement("appender");

            attr = xmlDoc.CreateAttribute("name");
            attr.Value = appenderName;
            xmlNode.Attributes.Append(attr);

            attr = xmlDoc.CreateAttribute("type");
            attr.Value = "log4net.Appender.RollingFileAppender";
            xmlNode.Attributes.Append(attr);


            //file
            xmlNodechild = xmlDoc.CreateElement("file");
            attr = xmlDoc.CreateAttribute("value");
            attr.Value = fullPath + appenderName + Path.DirectorySeparatorChar;
            System.Diagnostics.Trace.WriteLine(attr.Value);

            xmlNodechild.Attributes.Append(attr);
            xmlNode.AppendChild(xmlNodechild);


            //datePattern
            xmlNodechild = xmlDoc.CreateElement("datePattern");
            attr = xmlDoc.CreateAttribute("value");
            attr.Value = "yyyy-MM-dd'_" + appenderName + ".log'";
            xmlNodechild.Attributes.Append(attr);
            xmlNode.AppendChild(xmlNodechild);


            //staticLogFileName
            xmlNodechild = xmlDoc.CreateElement("staticLogFileName");
            attr = xmlDoc.CreateAttribute("value");
            attr.Value = "False";
            xmlNodechild.Attributes.Append(attr);
            xmlNode.AppendChild(xmlNodechild);


            //appendToFile
            xmlNodechild = xmlDoc.CreateElement("appendToFile");
            attr = xmlDoc.CreateAttribute("value");
            attr.Value = "true";
            xmlNodechild.Attributes.Append(attr);
            xmlNode.AppendChild(xmlNodechild);


            //rollingStyle
            xmlNodechild = xmlDoc.CreateElement("rollingStyle");
            attr = xmlDoc.CreateAttribute("value");
            attr.Value = "Composite";
            xmlNodechild.Attributes.Append(attr);
            xmlNode.AppendChild(xmlNodechild);


            //maxSizeRollBackups
            xmlNodechild = xmlDoc.CreateElement("maxSizeRollBackups");
            attr = xmlDoc.CreateAttribute("value");
            attr.Value = "100";
            xmlNodechild.Attributes.Append(attr);
            xmlNode.AppendChild(xmlNodechild);

            //maximumFileSize
            xmlNodechild = xmlDoc.CreateElement("maximumFileSize");
            attr = xmlDoc.CreateAttribute("value");
            attr.Value = "10MB";
            xmlNodechild.Attributes.Append(attr);
            xmlNode.AppendChild(xmlNodechild);

            //layout
            xmlNodechild = xmlDoc.CreateElement("layout");
            attr = xmlDoc.CreateAttribute("type");
            attr.Value = "log4net.Layout.PatternLayout";
            xmlNodechild.Attributes.Append(attr);

            xmlNodechild2 = xmlDoc.CreateElement("conversionPattern");
            attr = xmlDoc.CreateAttribute("value");
            attr.Value = @"%date{HH:mm:ss,fff} 	 %message%newline";
            xmlNodechild2.Attributes.Append(attr);
            xmlNodechild.AppendChild(xmlNodechild2);

            xmlNode.AppendChild(xmlNodechild);


            return xmlNode;
        }

        public XmlNode GetDefaultLoggerNode(XmlDocument xmlDoc, string refName, string loggerName)
        {
            XmlAttribute attr = null;
            XmlNode xmlNode = null;
            XmlNode xmlNodechild = null;


            //Logger
            xmlNode = xmlDoc.CreateElement("logger");

            attr = xmlDoc.CreateAttribute("name");
            attr.Value = loggerName;
            xmlNode.Attributes.Append(attr);

            //level
            xmlNodechild = xmlDoc.CreateElement("level");
            attr = xmlDoc.CreateAttribute("value");
            attr.Value = "Info";
            xmlNodechild.Attributes.Append(attr);

            xmlNode.AppendChild(xmlNodechild);

            //appender-ref
            xmlNodechild = xmlDoc.CreateElement("appender-ref");
            attr = xmlDoc.CreateAttribute("ref");
            attr.Value = refName;
            xmlNodechild.Attributes.Append(attr);
            xmlNode.AppendChild(xmlNodechild);



            return xmlNode;
        }

        public bool WriteLog(string logName, string msg)
        {
            lock (this)
            {
                if (logFilter.Contains(logName))
                {
                    if (MessageChanged != null)
                    {
                        MessageChanged(logName, msg);
                    }
                }

                if (LogDic.ContainsKey(logName))
                {
                    if (LogDic[logName].ContainsKey(logName))
                    {
                        LogDic[logName][logName].Info(msg);
                    }
                    else
                    {

                        if (LogDic[logName].ContainsKey(logName))
                        {
                            WriteLog(logName, msg);
                        }

                        AddRootXmlNode(logName, GetDefaultNode(logName, logName, logName));
                        LogDic[logName][logName].Info(msg);
                    }
                }
                else
                {
                    lock (this)
                    {
                        if (LogDic.ContainsKey(logName))
                        {
                            WriteLog(logName, msg);
                        }

                        AddRootXmlNode(logName, GetDefaultNode(logName, logName, logName));

                        if (LogDic.ContainsKey(logName))
                        {
                            if (LogDic[logName].ContainsKey(logName))
                            {
                                LogDic[logName][logName].Info(msg);
                            }
                            else
                            {
                                System.Diagnostics.Trace.WriteLine("[StockLogger] : LogDic[logName].ContainsKey(logName) Not Contain");

                                return false;
                            }
                        }
                        else
                        {
                            System.Diagnostics.Trace.WriteLine("[StockLogger] : Exception Log Write : " + logName);
                        }
                    }

                    return false;
                }
            }
            return true;
        }

        public static void RemoveLog()
        {
            try
            {
                DirectoryInfo di = new DirectoryInfo(fullPath);

                if (di != null)
                {
                    if (di.Exists)
                    {
                        foreach (DirectoryInfo dInfo in di.GetDirectories())
                        {
                            List<FileInfo> fileInfos = dInfo.EnumerateFiles().ToList();
                            for (int i = 0; i < fileInfos.Count; i++)
                            {
                                if ((DateTime.Now - fileInfos[i].CreationTime).TotalDays > logSaveDay)
                                {
                                    if (fileInfos[i].Name.ToUpper().Contains(".LOG"))
                                    {
                                        StockLog.Logger.LOG.WriteLog("DeleteLog", fileInfos[i].Name);
                                        fileInfos[i].Delete();
                                    }
                                }
                            }

                            //dInfo.EnumerateDirectories().ToList().ForEach(d => d.Delete(true));
                            //Directory.Delete(dInfo.FullName, true);
                            //dInfo.Delete(true);
                        }
                    }
                }
            }
            catch (Exception except)
            {
                StockLog.Logger.LOG.WriteLog("Exception", except.ToString());
            }
        }
    }
}
