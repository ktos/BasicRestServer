using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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

namespace Ktos.RestServer
{
    /// <summary>
    /// A server configuration
    /// </summary>
    class JsonConfig
    {
        /// <summary>
        /// Host server is listening at
        /// </summary>
        public string Host { get; set; }

        /// <summary>
        /// Port server is listening at
        /// </summary>
        public int Port { get; set; }

        /// <summary>
        /// Maximum number of threads
        /// </summary>
        public int MaxThreads { get; set; }

        /// <summary>
        /// List of Routes configuration
        /// </summary>
        public List<Route> Routes { get; set; }

        /// <summary>
        /// Creates a new JsonConfig instance
        /// </summary>
        public JsonConfig()
        {
            Routes = new List<Route>();
        }
    }

    /// <summary>
    /// A configuration for route
    /// </summary>
    class Route
    {
        /// <summary>
        /// A HTTP method route is responding to
        /// </summary>
        public string Method { get; set; }

        /// <summary>
        /// An regex which is matching URIs route is responding to 
        /// </summary>
        public string Path { get; set; }

        /// <summary>
        /// A file to be read from or saved to (mutually exclusive with Command)
        /// </summary>
        public string File { get; set; }

        /// <summary>
        /// A command to be read from or echoed to  (mutually exclusive with Command)
        /// </summary>
        public string Command { get; set; }

        /// <summary>
        /// Response configuration
        /// </summary>
        public Response Response { get; set; }
    }

    /// <summary>
    /// Configuration for Response options in routing
    /// </summary>
    class Response
    {
        /// <summary>
        /// MIME type of response to be set
        /// </summary>
        public string Type { get; set; }

        /// <summary>
        /// Response code to be set
        /// </summary>
        public int Code { get; set; }

        /// <summary>
        /// Response code description message to be set
        /// </summary>
        public string Status { get; set; }
    }
}
