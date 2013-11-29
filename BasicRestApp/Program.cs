using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Ktos.BasicRestServer;

namespace Ktos.BasicRestServer
{
    class Program
    {
        static void Main(string[] args)
        {            
            BasicRestServer hs = new BasicRestServer(5);
            hs.AddRoute("GET", "test.html", ProcessTest);

            hs.Start("localhost", 8000);
        }

        public static void ProcessTest(System.Net.HttpListenerRequest req, System.Net.HttpListenerResponse res)
        {
            res.OutputStream.WriteString("Hello, world!<br />");
            res.OutputStream.WriteString("How are you?");
        }
    }

}
