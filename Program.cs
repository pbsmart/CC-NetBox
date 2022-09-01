using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace CC_NetBox
{
    class Program
    {
        static void Main(string[] args)
        {
            HttpServer server = new HttpServer();
            server.OnEventInfo += Server_OnEventInfo;
            Random random = new Random();

            server.start(GetPort());

            try
            {
               var ip = server.listener?.Prefixes?.ToList().Find(x => x.Contains("192.168"));
                Process.Start("explorer.exe", ip);
            }
            catch
            {

            }



            Console.WriteLine();
            Console.WriteLine("输入 C 退出程序");

            while (true)
            {
                var result = Console.ReadLine().ToLower();
                if (result == "c")
                {
                    break;
                }
            }
            server.stop();
        }

        static int GetPort()
        {
            IPGlobalProperties ipGlobalProperties=IPGlobalProperties.GetIPGlobalProperties();
            var list= ipGlobalProperties.GetActiveTcpListeners().ToList().Select(x=>x.Port).ToList();
            list.Sort();
            return list[list.Count-1] + 1;
        }

        private static void Server_OnEventInfo(object sender, string info)
        {
            Console.WriteLine(info);
        }
    }
    public class HttpServer
    {
        // HTTP服务
        public HttpListener listener = new HttpListener();
        private Thread ThreadListener = null;

        // web根目录
        private string WebPath = AppDomain.CurrentDomain.BaseDirectory + "\\";

        // 自定义POST处理
        public delegate string EventDo(object sender, HttpListenerRequest request);
        public event EventDo OnEventDo;

        // 消息事件
        public delegate void Log(object sender, string info);
        public event Log OnEventInfo;

        // 最后错误信息
        public string LastErrorInfo = "";

        // 是否调试
        public bool IsDebug = false;

        public HttpServer()
        {
        }

        // 触发消息
        private void showInfo(string str)
        {
            OnEventInfo(this, str);
        }

        // Http服务器启动
        public bool start(int port)
        {
            try
            {
                if (listener.IsListening)
                {
                    return true;
                }

                // 使用本机IP地址监听
                IPAddress[] ipList = Dns.GetHostAddresses(Dns.GetHostName());

                listener.IgnoreWriteExceptions = true;
                listener.Prefixes.Clear();
                string host = "http://127.0.0.1:" + port.ToString() + "/";
                listener.Prefixes.Add(host);
                foreach (IPAddress ip in ipList)
                {
                    if (ip.AddressFamily.Equals(AddressFamily.InterNetwork))
                    {
                        host = "http://" + ip.ToString() + ":" + port.ToString() + "/";
                        if (!listener.Prefixes.Contains(host))
                        {
                            listener.Prefixes.Add(host);
                        }
                    }
                }

                listener.Start();
            }
            catch (Exception ex)
            {
                if (listener.IsListening)
                {
                    listener.Stop();
                }

                LastErrorInfo = "ERROR:HttpServer 启动失败:" + ex.Message;
                showInfo(LastErrorInfo);
                return false;
            }

            ThreadListener = new Thread(() =>
            {
                while (listener.IsListening)
                {
                    try
                    {
                        HttpListenerContext request = listener.GetContext();

                        // 从线程池从开一个新的线程去处理请求
                        ThreadPool.QueueUserWorkItem(processRequest, request);
                    }
                    catch
                    { }
                }
            });
            ThreadListener.Start();
            foreach (var item in listener.Prefixes)
            {
                showInfo("HttpServer:" + item + " 已启动");

            }

            return true;
        }

        // 停止
        public void stop()
        {
            if (listener.IsListening)
            {
                ThreadListener.Abort();
                listener.Stop();
                ThreadListener = null;
            }
        }

        // 是否在运行
        public bool isRun()
        {
            return listener.IsListening;
        }

        // 处理用户请求
        private void processRequest(object listenerContext)
        {

            var context = (HttpListenerContext)listenerContext;
            try
            {
                string filename = context.Request.Url.AbsolutePath.Trim('/');
                if (filename == "")
                {
                    filename = "index.html";
                }

                if (IsDebug)
                {
                    showInfo(context.Request.Url.ToString());
                }

                string[] ext_list = filename.Split('.');
                string ext = "";
                if (ext_list.Length > 1)
                {
                    ext = ext_list[ext_list.Length - 1];
                }

                string path = Path.Combine(WebPath, filename);
                byte[] msg = new byte[0];

                context.Response.StatusCode = (int)HttpStatusCode.OK;

                string expires = DateTime.Now.AddYears(10).ToString("r");

                // 浏览器缓存
                switch (ext)
                {
                    case "html":
                    case "htm":
                        context.Response.ContentType = "text/html";
                        break;
                    case "jpg":
                    case "jpeg":
                    case "jpe":
                        context.Response.ContentType = "image/jpeg";
                        context.Response.AddHeader("cache-control", "max-age=315360000, immutable");
                        context.Response.AddHeader("expires", expires);
                        break;
                    case "png":
                        context.Response.ContentType = "image/png";
                        context.Response.AddHeader("cache-control", "max-age=315360000, immutable");
                        context.Response.AddHeader("expires", expires);
                        break;
                    case "gif":
                        context.Response.ContentType = "image/gif";
                        context.Response.AddHeader("cache-control", "max-age=315360000, immutable");
                        context.Response.AddHeader("expires", expires);
                        break;
                    case "js":
                        context.Response.ContentType = "application/x-javascript";
                        context.Response.AddHeader("cache-control", "max-age=315360000, immutable");
                        context.Response.AddHeader("expires", expires);
                        break;
                    case "css":
                        context.Response.ContentType = "text/css";
                        context.Response.AddHeader("cache-control", "max-age=315360000, immutable");
                        context.Response.AddHeader("expires", expires);
                        break;
                    case "ico":
                        context.Response.ContentType = "application/x-ico";
                        context.Response.AddHeader("cache-control", "max-age=315360000, immutable");
                        context.Response.AddHeader("expires", expires);
                        break;
                    case "txt":
                        context.Response.ContentType = "text/plain";
                        break;
                    case "do":
                        context.Response.AddHeader("Access-Control-Allow-Origin", "*");
                        context.Response.ContentType = "text/plain;charset=utf-8";
                        string strDo = OnEventDo(this, context.Request);
                        msg = Encoding.UTF8.GetBytes(strDo);
                        break;
                    default:
                        context.Response.ContentType = "";
                        context.Response.AddHeader("cache-control", "max-age=315360000, immutable");
                        context.Response.AddHeader("expires", expires);
                        break;
                }

                if (msg.Length == 0)
                {
                    showInfo(path);
                    if (!File.Exists(path))
                    {
                        context.Response.ContentType = "text/html";
                        context.Response.StatusCode = (int)HttpStatusCode.NotFound;
                        if (File.Exists(WebPath + "error.html"))
                        {
                            msg = File.ReadAllBytes(WebPath + "error.html");
                        }
                        else
                        {
                            msg = Encoding.Default.GetBytes("404");
                        }
                    }
                    else
                    {
                        msg = File.ReadAllBytes(path);
                    }
                }

                context.Response.ContentLength64 = msg.Length;
                using (Stream s = context.Response.OutputStream)
                {
                    s.Write(msg, 0, msg.Length);
                }

                msg = new byte[0];
                GC.Collect();
            }
            catch (Exception ex)
            {
                showInfo("ERROR:" + ex.Message);
            }
        }
    }
}
