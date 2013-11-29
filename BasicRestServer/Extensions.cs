using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ktos
{
    public static class Extensions
    {
        public static void WriteString(this System.IO.Stream s, string ss)
        {
            var r = Encoding.UTF8.GetBytes(ss);
            s.Write(r, 0, r.Length);
        }
    }
}
