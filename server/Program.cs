using LanguageServer.Infrastructure.JsonDotNet;
using LanguageServer.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VSCodeLanguageServer;

namespace VSCodeLanguageServer
{
    class Program
    {
       private const Boolean testing = false;
        static void Main(string[] args)
        {
         if (testing)
         {
            ParseIB parse1, parse2;
            string filePath = "d:\\artifacts\\vscode_hood\\installs\\stdtest\\source\\project\\dummy_project\\dummy_foreground.ib";

            using (FileStream fs = new FileStream(filePath, FileMode.Open, FileAccess.Read))
            {
               parse1 = ParseIB.Parse(filePath, fs);
            }
            filePath = "D:\\artifacts\\vscode_hood\\installs\\stdtest\\source\\configurator\\exportfiles\\close.ib";
            using (FileStream fs = new FileStream(filePath, FileMode.Open, FileAccess.Read))
            {
               parse2 = ParseIB.Parse(filePath, fs);
            }
            Console.WriteLine($"parse1: {parse1.ParseItems.Count}, parse2: {parse2.ParseItems.Count}");
         }
         else
         {
            Console.OutputEncoding = new UTF8Encoding(); // UTF8N for non-Windows platform
            var app = new App(Console.OpenStandardInput(), Console.OpenStandardOutput());
            Logger.Instance.Attach(app);
            try
            {
               app.Listen().Wait();
            }
            catch (AggregateException ex)
            {
               Console.Error.WriteLine(ex.InnerExceptions[0]);
               Environment.Exit(-1);
            }
         }

      }
    }
}
