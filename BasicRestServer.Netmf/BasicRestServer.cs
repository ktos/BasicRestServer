using System;
using Microsoft.SPOT;
using System.Net;
using System.Net.Sockets;
using System.Collections;
using System.Threading;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace Ktos.RestServer
{
    public delegate void ProcessRequest(HttpListenerRequest req, HttpListenerResponse res);

    public class BasicRestServer
    {
        private ArrayList routes;

        const int BUFFER_SIZE = 1024;        

        public BasicRestServer()
        {
            routes = new ArrayList();
            m_responseQueue = new Queue();
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
                lock (m_responseQueue)
                {
                    context = (HttpListenerContext)m_responseQueue.Dequeue();
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
                Route x = routes[i] as Route;
                if (context.Request.HttpMethod.ToUpper() == x.Method.ToUpper())
                {
                    Regex r = new Regex(x.Uri);
                    if (r.IsMatch(context.Request.Url.AbsoluteUri))
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
        protected virtual void showNotFound(System.Net.HttpListenerContext obj)
        {
            obj.Response.ContentType = "text/html";
            obj.Response.StatusCode = 404;
            obj.Response.StatusDescription = "Not Found";
            obj.Response.OutputStream.WriteString("<h1>Requested Document Not Found</h1>");
        }

        Queue m_responseQueue;
        HttpListener listener;

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
                    lock (m_responseQueue)
                    {
                        m_responseQueue.Enqueue(context);
                    }

                    Thread th = new Thread(new ThreadStart(handleRequestThread));
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
