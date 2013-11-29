using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Ktos.BasicRestServer
{
    public delegate void ProcessRequest(HttpListenerRequest req, HttpListenerResponse res);

    public class BasicRestServer : IDisposable
    {
        private readonly HttpListener _listener;
        private readonly Thread _listenerThread;
        private readonly Thread[] _workers;
        private readonly ManualResetEvent _stop, _ready;
        private Queue<HttpListenerContext> _queue;

        private Dictionary<string, ProcessRequest> routesGet;
        private Dictionary<string, ProcessRequest> routesPost;
        private Dictionary<string, ProcessRequest> routesPut;
        private Dictionary<string, ProcessRequest> routesDelete;

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

        public void AddRoute(string method, string pathRegExp, ProcessRequest r)
        {
            switch (method)
            {
                case "GET":
                    {
                        routesGet.Add(pathRegExp, r);
                        break;
                    }
                case "POST":
                    {
                        routesPost.Add(pathRegExp, r);
                        break;
                    }
                case "PUT":
                    {
                        routesPut.Add(pathRegExp, r);
                        break;
                    }
                case "DELETE":
                    {
                        routesDelete.Add(pathRegExp, r);
                        break;
                    }
                default:
                    {
                        break;
                    }
            }
        }

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

        public void Dispose()
        {
            Stop();
        }

        public void Stop()
        {
            _stop.Set();
            _listenerThread.Join();
            foreach (Thread worker in _workers)
                worker.Join();
            _listener.Stop();
        }

        private void HandleRequests()
        {
            while (_listener.IsListening)
            {
                var context = _listener.BeginGetContext(ContextReady, null);

                if (0 == WaitHandle.WaitAny(new[] { _stop, context.AsyncWaitHandle }))
                    return;
            }
        }

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

        private void processRequest(System.Net.HttpListenerContext obj)
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
                obj.Response.OutputStream.WriteString(String.Format("{0} {1}", obj.Request.HttpMethod, obj.Request.Url.AbsolutePath));
            }
            else
            {
                result(obj.Request, obj.Response);
            }            

            obj.Response.Close();
        }

        private static Func<KeyValuePair<string, ProcessRequest>, bool> findRoute(System.Net.HttpListenerContext obj)
        {
            return x => { Regex r = new Regex(x.Key); return r.IsMatch(obj.Request.Url.AbsolutePath); };
        }
    }
}
