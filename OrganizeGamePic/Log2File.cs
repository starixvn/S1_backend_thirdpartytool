using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Log2File1
{
    public class Log2File
    {
        private string LogFolder = "";
        public Log2File(string filePath)
        {
            LogFolder = $@"{filePath}\Logs";
        }
        public void SetPath(string filePath)
        {
            LogFolder = $@"{filePath}\Logs";
        }
        public void Info(string InfoContent, string PrefixName)
        {
            var filePath = $@"{LogFolder}\{PrefixName}_Info_{DateTime.Now:yyyyMMdd}.log";
            //Task.Run(() =>
            //{
            Directory.CreateDirectory(LogFolder);
            StreamWriter writer = new StreamWriter(filePath, append: true);
            writer.WriteLine($"{DateTime.Now:yyyy/MM/dd HH:mm:ss}\r\n  Thread:{Thread.CurrentThread.ManagedThreadId}\r\n{InfoContent}");
            writer.Flush();
            writer.Close();


            //});
        }
        public void Error(string ErrorContent, string PrefixName)
        {
            var filePath = $@"{LogFolder}\{PrefixName}_Err_{DateTime.Now:yyyyMMdd}.log";
            //Task.Run(() =>
            //{
            Directory.CreateDirectory(LogFolder);
            StreamWriter writer = new StreamWriter(filePath, append: true);
            writer.WriteLine($"{DateTime.Now:yyyy/MM/dd HH:mm:ss}\r\n  Thread:{Thread.CurrentThread.ManagedThreadId}\r\n{ErrorContent}");
            writer.Flush();
            writer.Close();
            //});
        }

    }
}
