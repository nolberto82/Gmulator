using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Gmulator.Shared
{
    public interface IJoypad
    {
        bool[] Buttons { get; set; }
        int Read(int min, int max);
    }
}
