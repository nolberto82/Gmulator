using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Gmulator.Core.Snes
{
    public interface ICpu
    {
        byte Read(int a) { return 0; }
        void Write(int a, int v) { }
        void Reset() { }
    }
}
