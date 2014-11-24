#region License
/*
 * BasicRestServer
 *
 * Copyright (C) Marcin Badurowicz 2014
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
using System.Collections;
using System.Net;
using System.Net.Sockets;
using System.Text.RegularExpressions;
using System.Threading;
using Ktos;

namespace BasicRestServer.Netmf
{
    public delegate void ProcessRequest(HttpListenerRequest req, HttpListenerResponse res);

    public class BasicRestServer
    {
        private ArrayList routes;

        public BasicRestServer()
        {
            routes = new ArrayList();
            responseQueue = new Queue();
        }

        public void AddRoute(string method, string pathRegExp, ProcessRequest processor)
        {
            routes.Add(new Route() { Method = method, Uri = pathRegExp, Handler = processor });
        }

        private void handleRequestThread()
        {
            HttpListenerContext context = null;

            try
            {
                lock (responseQueue)
                {
                    context = (HttpListenerContext)responseQueue.Dequeue();
                }

                if (context != null)
                {
                    runHandler(context);
                }
            }
            catch (SocketException)
            {
            }
            finally
            {
                if (context != null)
                {
                    context.Close();
                }
            }
        }

        private void runHandler(HttpListenerContext context)
        {
            for (int i = 0; i < routes.Count; i++)
            {
                Route x = (Route)routes[i];
                if (x.Method != null && context.Request.HttpMethod.ToUpper() == x.Method.ToUpper())
                {
                    Regex r = new Regex(x.Uri);
                    if (r.IsMatch(context.Request.RawUrl))
                    {
                        x.Handler(context.Request, context.Response);
                        return;
                    }
                }
            }

            showNotFound(context);
        }

        /// <summary>
        /// Showing Not Found message. This method may be overwritten in descendant classes.
        /// </summary>
        /// <param name="obj">Listener context</param>
        protected virtual void showNotFound(HttpListenerContext obj)
        {
            obj.Response.ContentType = "text/html";
            obj.Response.StatusCode = 404;
            obj.Response.StatusDescription = "Not Found";
            obj.Response.OutputStream.WriteString("<h1>Requested Document Not Found</h1>");
        }

        private Queue responseQueue;
        private HttpListener listener;

        public void Start(string prefix, int port)
        {
            listener = new HttpListener(prefix, port);

            while (true)
            {
                try
                {
                    if (!listener.IsListening)
                    {
                        listener.Start();
                    }

                    HttpListenerContext context = listener.GetContext();
                    lock (responseQueue)
                    {
                        responseQueue.Enqueue(context);
                    }

                    Thread th = new Thread(handleRequestThread);
                    th.Start();
                }
                catch (InvalidOperationException)
                {
                    listener.Stop();
                    Thread.Sleep(200);
                }
                catch (ObjectDisposedException)
                {
                    listener.Start();
                }
                catch
                {
                    Thread.Sleep(200);
                }
            }
        }

        public void Stop()
        {
            listener.Stop();
        }        
    }
}
