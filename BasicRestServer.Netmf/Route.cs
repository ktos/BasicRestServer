using System;
using Microsoft.SPOT;

namespace Ktos.RestServer
{
    class Route
    {
        public string Method { get; set; }
        public string Uri { get; set; }
        public ProcessRequest Handler { get; set; }
    }
}
