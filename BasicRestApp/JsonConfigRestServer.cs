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

using Newtonsoft.Json;
using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace Ktos.RestServer
{
    /// <summary>
    /// A simple, JSON-file configured REST-like server allowing access to files 
    /// and commands.
    /// </summary>
    class JsonConfigRestServer: IDisposable
    {
        /// <summary>
        /// Json configuration for the server
        /// </summary>
        private JsonConfig config;

        /// <summary>
        /// A private instance of BasicRestServer we're building upon
        /// </summary>
        private BasicRestServer server;

        /// <summary>
        /// Defines if "verbose" mode should be used
        /// </summary>
        public bool IsVerbose { get; set; }

        /// <summary>
        /// Application entry point, parsing command-line arguments and starting
        /// server.
        /// </summary>
        /// <param name="args"></param>
        public JsonConfigRestServer(string configFilePath)
        {
            configureServer(configFilePath);
            this.IsVerbose = false;
        }

        /// <summary>
        /// Starts the server
        /// </summary>
        public void Start()
        {
            if (this.IsVerbose)
                Console.WriteLine("Server listening on {0}:{1}", config.Host, config.Port);

            server.Start(config.Host, config.Port);
        }

        /// <summary>
        /// Reading configuration data and setting up the server
        /// </summary>
        private void configureServer(string configFilePath)
        {
            try
            {
                var configText = File.ReadAllText(configFilePath);
                config = JsonConvert.DeserializeObject<JsonConfig>(configText);

                server = new BasicRestServer(config.MaxThreads);

                foreach (var item in config.Routes)
                {
                    server.AddRoute(item.Method, item.Path, Show);
                }
                server.AddRoute("GET", "/(.*)$", Show404);
                server.AddRoute("POST", "/(.*)$", Show404);
                server.AddRoute("PUT", "/(.*)$", Show404);
                server.AddRoute("DELETE", "/(.*)$", Show404);
            }
            catch (FileNotFoundException)
            {
                Console.Error.WriteLine("Error: config file not found");
                Environment.Exit(1);
            }
        }

        /// <summary>
        /// Handling user request and writing response
        /// </summary>
        /// <param name="req">User request data</param>
        /// <param name="res">Response data</param>
        private void Show(System.Net.HttpListenerRequest req, System.Net.HttpListenerResponse res)
        {
            // find route we're on
            var myRoute = config.Routes.Find(x => { Regex r = new Regex(x.Path); return r.IsMatch(req.Url.AbsolutePath) && req.HttpMethod == x.Method; });

            if (this.IsVerbose)
                Console.WriteLine("A new request from {0}: {1} {2}", req.RemoteEndPoint.Address, req.HttpMethod, req.Url.AbsolutePath);

            // if response type is defined, set it
            if (myRoute.Response != null && myRoute.Response.Type != null)
                res.ContentType = myRoute.Response.Type;
            else
                res.ContentType = "text/plain";

            try
            {
                // if file is set in route config
                if (myRoute.File != null)
                {
                    // find file name if using "regex" method
                    string fileName = myRoute.File;
                    if (myRoute.File == "regex")
                    {
                        Regex r = new Regex(myRoute.Path);
                        var m = r.Match(req.Url.AbsolutePath);
                        fileName = m.Groups["regex"].Value;
                    }

                    // what HTTP method user is requesting
                    switch (myRoute.Method)
                    {
                        case "GET":
                            {
                                // gets a file
                                writeFileToStream(res.OutputStream, fileName);
                                break;
                            }

                        case "POST":
                            {
                                // POST only updates files, not creates a file
                                // so we're throwing 400 Bad Request if such file not exists
                                if (!File.Exists(fileName))
                                {
                                    writeBadRequest(res, "Use PUT to create a new file");
                                    break;
                                }

                                // what to update if there is no body?
                                if (req.HasEntityBody)
                                {
                                    updateFile(req.InputStream, fileName);
                                    writeOk(res, "");
                                }
                                else
                                    writeBadRequest(res, "No body in request");

                                break;
                            }
                        case "PUT":
                            {
                                // creating a file
                                if (req.HasEntityBody)
                                {
                                    updateFile(req.InputStream, fileName);
                                    writeOk(res, "");
                                }
                                else
                                    writeBadRequest(res, "No body in request");


                                break;
                            }
                        case "DELETE":
                            {
                                // deleting a file
                                File.Delete(fileName);
                                writeOk(res, "File deleted");

                                break;
                            }

                        default:
                            {
                                writeBadRequest(res);

                                break;
                            }
                    }

                }
                else if (myRoute.Command != null)
                {
                    // if not file is configured, command is configured?

                    // find regex
                    string command = myRoute.Command;
                    if (myRoute.Command != null && myRoute.Command == "regex")
                    {
                        Regex r = new Regex(myRoute.Path);
                        var m = r.Match(req.Url.AbsolutePath);
                        command = m.Groups["regex"].Value;
                    }

                    switch (myRoute.Method)
                    {
                        case "GET":
                            {
                                // gets a result from command, based on running shell, so there could
                                // be piping and everything
                                startShell(res, String.Format("\"{0}\"", command));

                                break;
                            }

                        case "POST":
                            {
                                // sends a HTTP body to command and get a result
                                string body = String.Empty;
                                if (req.HasEntityBody)
                                {
                                    var bodyLength = req.InputStream.Length;
                                    var buffer = new byte[bodyLength];
                                    req.InputStream.Read(buffer, 0, (int)bodyLength);
                                    body = Encoding.ASCII.GetString(buffer);
                                }

                                startShell(res, String.Format("echo \"{1}\" | {0}", command, body));

                                break;
                            }

                        default:
                            {
                                // PUT and DELETE are not supported
                                writeBadRequest(res);                                
                                break;
                            }
                    }
                }

                // setting up Response code (if configured) and there wasn't error
                if (res.StatusCode == 200)
                {
                    if (myRoute.Response != null && myRoute.Response.Status != null)
                    {
                        res.StatusCode = myRoute.Response.Code;
                        res.StatusDescription = myRoute.Response.Status;
                    }
                }
            }
            catch (Exception ex)
            {
                res.StatusCode = 500;
                res.StatusDescription = "Internal Server Error";
                res.ContentType = "text/plain";
                res.OutputStream.WriteString("Internal Server Error: " + ex.Message);

                Console.Error.WriteLine("Error: " + ex.Message);
            }
        }

        /// <summary>
        /// Writes a file to stream
        /// </summary>
        /// <param name="s">Stream to write file to (in here: Response stream)</param>
        /// <param name="fileName">A file name to be written into stream</param>
        private void writeFileToStream(Stream s, string fileName)
        {
            var data = File.ReadAllBytes(fileName);
            s.Write(data, 0, data.Length);
        }

        /// <summary>
        /// Updates a file with data from stream
        /// </summary>
        /// <param name="s">A stream of data (in here: Request body)</param>
        /// <param name="fileName">A file name to be written into</param>
        private void updateFile(Stream s, string fileName)
        {
            MemoryStream ms = new MemoryStream();
            s.CopyTo(ms);
            var bodyLength = ms.Length;
            var buffer = new byte[bodyLength];
            ms.Seek(0, SeekOrigin.Begin);
            ms.Read(buffer, 0, (int)bodyLength);
            File.WriteAllBytes(fileName, buffer);
        }

        /// <summary>
        /// Showing 400 Bad Request message
        /// </summary>
        /// <param name="res">A Response to write request</param>
        private void writeBadRequest(System.Net.HttpListenerResponse res)
        {
            writeBadRequest(res, "");
        }

        /// <summary>
        /// Showing 400 Bad Request message
        /// </summary>
        /// <param name="res">A Response to write request</param>
        /// <param name="message">Additional message to show</param>
        private void writeBadRequest(System.Net.HttpListenerResponse res, string message)
        {
            res.StatusCode = 400;
            res.StatusDescription = "Bad Request";
            res.OutputStream.WriteString("Bad Request");
            res.OutputStream.WriteString(message);
        }

        /// <summary>
        /// Sends regular 200 OK response with an additional message
        /// </summary>
        /// <param name="res"></param>
        /// <param name="message"></param>
        private void writeOk(System.Net.HttpListenerResponse res, string message)
        {
            res.StatusCode = 200;
            res.StatusDescription = "OK";
            res.OutputStream.WriteString(message);
        }

        /// <summary>
        /// Starts a shell for the current platform, and sends there a command, and writes a response
        /// </summary>
        /// <param name="res">A Response to be written into</param>
        /// <param name="parameters">Parameters for shell - command name, piping and so on</param>
        private void startShell(System.Net.HttpListenerResponse res, string parameters)
        {
            ProcessStartInfo psi;
            if (Environment.OSVersion.Platform == PlatformID.Win32NT)
            {
                psi = new ProcessStartInfo("cmd.exe", String.Format("/c {0}", parameters));
            }
            else if (Environment.OSVersion.Platform == PlatformID.Unix)
            {
                psi = new ProcessStartInfo("sh", String.Format("{0}", parameters));
            }
            else
            {
                throw new PlatformNotSupportedException();
            }

            psi.RedirectStandardOutput = true;
            psi.RedirectStandardError = true;
            psi.UseShellExecute = false;

            Process p = Process.Start(psi);
            p.WaitForExit();

            var error = p.StandardError.ReadToEnd();
            var output = p.StandardOutput.ReadToEnd();
            res.OutputStream.WriteString(output);
            res.OutputStream.WriteString(error);
        }

        /// <summary>
        /// Shows internal 404 message (if not configured in config file)
        /// </summary>
        /// <param name="req">Request object</param>
        /// <param name="res">Response object</param>
        private void Show404(System.Net.HttpListenerRequest req, System.Net.HttpListenerResponse res)
        {
            res.StatusCode = 404;
            res.StatusDescription = "Not Found";

            res.OutputStream.WriteString("Not found!");
        }

        /// <summary>
        /// Stopping the server
        /// </summary>
        public void Dispose()
        {
            server.Dispose();
        }
    }
}
