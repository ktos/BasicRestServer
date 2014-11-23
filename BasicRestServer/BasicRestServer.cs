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
using System.Net;
using System.Text.RegularExpressions;
using System.Threading;

namespace Ktos.BasicRestServer
{
    /// <summary>
    /// A delegate which is processing a request matching given pattern
    /// </summary>
    /// <param name="req">Request object</param>
    /// <param name="res">Response object. You can write to Response OutputStream.</param>
    public delegate void ProcessRequest(HttpListenerRequest req, HttpListenerResponse res);

    /// <summary>
    /// A very simple REST-like HTTP server based upon HttpListener class
    /// </summary>
    public class BasicRestServer : IDisposable
    {
        /// <summary>
        /// Internal HttpListener
        /// </summary>
        private readonly HttpListener _listener;

        /// <summary>
        /// A listener Thread
        /// </summary>
        private readonly Thread _listenerThread;

        /// <summary>
        /// List of worker threads
        /// </summary>
        private readonly Thread[] _workers;

        /// <summary>
        /// Reset event
        /// </summary>
        private readonly ManualResetEvent _stop, _ready;

        /// <summary>
        /// Client queue
        /// </summary>
        private Queue<HttpListenerContext> _queue;

        /// <summary>
        /// Routes for GET method
        /// </summary>
        private Dictionary<string, ProcessRequest> routesGet;

        /// <summary>
        /// Routes for POST method
        /// </summary>
        private Dictionary<string, ProcessRequest> routesPost;

        /// <summary>
        /// Routes for PUT method
        /// </summary>
        private Dictionary<string, ProcessRequest> routesPut;

        /// <summary>
        /// Routes for DELETE method
        /// </summary>
        private Dictionary<string, ProcessRequest> routesDelete;

        /// <summary>
        /// A constructor
        /// </summary>
        /// <param name="maxThreads">Maximum number of worker threads</param>
        public BasicRestServer(int maxThreads)
        {
            _workers = new Thread[maxThreads];
            _queue = new Queue<HttpListenerContext>();
            _stop = new ManualResetEvent(false);
            _ready = new ManualResetEvent(false);
            _listener = new HttpListener();
            _listenerThread = new Thread(HandleRequests);

            routesGet = new Dictionary<string, ProcessRequest>();
            routesPost = new Dictionary<string, ProcessRequest>();
            routesPut = new Dictionary<string, ProcessRequest>();
            routesDelete = new Dictionary<string, ProcessRequest>();
        }

        /// <summary>
        /// Adds a route - a specific delegate, a HTTP method and a regular 
        /// expression to which AbsolutePath have to match to run this delegate
        /// </summary>
        /// <param name="method">A HTTP method - GET, POST, PUT or DELETE (case sensitive)</param>
        /// <param name="pathRegExp">Absolute URI path regular expression to be matched for run</param>
        /// <param name="processor">A delegate which will be run if path and method matches</param>
        public void AddRoute(string method, string pathRegExp, ProcessRequest processor)
        {
            switch (method)
            {
                case "GET":
                    {
                        routesGet.Add(pathRegExp, processor);
                        break;
                    }
                case "POST":
                    {
                        routesPost.Add(pathRegExp, processor);
                        break;
                    }
                case "PUT":
                    {
                        routesPut.Add(pathRegExp, processor);
                        break;
                    }
                case "DELETE":
                    {
                        routesDelete.Add(pathRegExp, processor);
                        break;
                    }
                default:
                    {
                        break;
                    }
            }
        }

        /// <summary>
        /// Starts listening on specified host and port
        /// </summary>
        /// <param name="host">A host to listen to. "localhost" does not need 
        /// elevated permission under Windows. You can use + or * to bind to
        /// every IP/host in the system</param>
        /// <param name="port"></param>
        public void Start(string host, int port)
        {
            _listener.Prefixes.Add(String.Format(@"http://{0}:{1}/", host, port));
            _listener.Start();
            _listenerThread.Start();

            for (int i = 0; i < _workers.Length; i++)
            {
                _workers[i] = new Thread(Worker);
                _workers[i].Start();
            }
        }

        /// <summary>
        /// Stopping the server
        /// </summary>
        public void Dispose()
        {
            Stop();
        }

        /// <summary>
        /// Stopping listening and closing workers
        /// </summary>
        public void Stop()
        {
            _stop.Set();
            _listenerThread.Join();
            foreach (Thread worker in _workers)
                worker.Join();
            _listener.Stop();
        }

        /// <summary>
        /// Handling a new request
        /// </summary>
        private void HandleRequests()
        {
            while (_listener.IsListening)
            {
                var context = _listener.BeginGetContext(ContextReady, null);

                if (0 == WaitHandle.WaitAny(new[] { _stop, context.AsyncWaitHandle }))
                    return;
            }
        }

        /// <summary>
        /// Handling a new request, part 2
        /// </summary>
        /// <param name="ar"></param>
        private void ContextReady(IAsyncResult ar)
        {
            try
            {
                lock (_queue)
                {
                    _queue.Enqueue(_listener.EndGetContext(ar));
                    _ready.Set();
                }
            }
            catch
            {
                return;
            }
        }

        /// <summary>
        /// A worker method used for worker threads
        /// </summary>
        private void Worker()
        {
            WaitHandle[] wait = new[] { _ready, _stop };
            while (0 == WaitHandle.WaitAny(wait))
            {
                HttpListenerContext context;
                lock (_queue)
                {
                    if (_queue.Count > 0)
                        context = _queue.Dequeue();
                    else
                    {
                        _ready.Reset();
                        continue;
                    }
                }

                try
                {
                    processRequest(context);
                }
                catch (Exception e)
                {
                    Console.Error.WriteLine(e);
                }
            }
        }

        /// <summary>
        /// Finding a delegate to be run, based on method and path from routes
        /// </summary>
        /// <param name="obj"></param>
        protected virtual void processRequest(System.Net.HttpListenerContext obj)
        {
            ProcessRequest result;

            switch (obj.Request.HttpMethod)
            {
                case "GET":
                    {
                        result = routesGet.FirstOrDefault(findRoute(obj)).Value;
                        break;
                    }

                case "POST":
                    {
                        result = routesPost.FirstOrDefault(findRoute(obj)).Value;
                        break;
                    }

                case "PUT":
                    {
                        result = routesPut.FirstOrDefault(findRoute(obj)).Value;
                        break;
                    }

                case "DELETE":
                    {
                        result = routesDelete.FirstOrDefault(findRoute(obj)).Value;
                        break;
                    }

                default:
                    {
                        result = null;
                        break;
                    }
            }

            if (result == null)
            {
                this.showNotFound(obj);
            }
            else
            {
                result(obj.Request, obj.Response);
            }            

            obj.Response.Close();
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

        /// <summary>
        /// Internal comparation for finding a match in routes
        /// </summary>
        /// <param name="obj">Absolute path from HttpListenerContext</param>
        /// <returns>Returns if specified predicate's RegExp matches path</returns>
        private static Func<KeyValuePair<string, ProcessRequest>, bool> findRoute(System.Net.HttpListenerContext obj)
        {
            return x => { Regex r = new Regex(x.Key); return r.IsMatch(obj.Request.Url.AbsolutePath); };
        }
    }
}
