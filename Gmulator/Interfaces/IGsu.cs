using System;
using System.Collections.Generic;
using System.Text;

namespace Gmulator.Interfaces;

public interface IGsu
{
    int ReadGsu(int addr);
    void WriteGsu(int addr, int value);
    List<RegisterInfo> GetMisc();
}
