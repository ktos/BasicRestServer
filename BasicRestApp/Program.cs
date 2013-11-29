#region License
/*
 * BasicRestServer
 *
 * Copyright (C) Marcin Badurowicz 2013
 *
 *
 * Permission is hereby granted, free of charge, to any person obtaining
 * a copy of this software and associated documentation files
 * (the "Software"), to deal in the Software without restriction,
 * including without limitation the rights to use, copy, modify, merge,
 * publish, distribute, sublicense, and/or sell copies of the Software,
 * and to permit persons to whom the Software is furnished to do so,
 * subject to the following conditions:
 * 
 * The above copyright notice and this permission notice shall be
 * included in all copies or substantial portions of the Software.
 * 
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
 * EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES
 * OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
 * NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS
 * BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN
 * ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN
 * CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
 * SOFTWARE. 
 */
#endregion

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Ktos.BasicRestServer;
using System.Text.RegularExpressions;

namespace Ktos.BasicRestServer
{
    class Program
    {
        static void Main(string[] args)
        {
            BasicRestServer hs = new BasicRestServer(5);
            
            hs.AddRoute("GET", "/test$", ProcessTest);
            hs.AddRoute("GET", "/(.*)$", ProcessEverything);
            
            hs.Start("localhost", 8000);
        }

        public static void ProcessEverything(System.Net.HttpListenerRequest req, System.Net.HttpListenerResponse res)
        {
            res.StatusCode = 404;
            res.StatusDescription = "Not Found";

            res.OutputStream.WriteString("Not found!");
        }

        public static void ProcessTest(System.Net.HttpListenerRequest req, System.Net.HttpListenerResponse res)
        {                        
            res.OutputStream.WriteString("Hello, world.");            
        }
    }
}
