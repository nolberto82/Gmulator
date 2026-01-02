using System;
using System.Collections.Generic;
using System.Text;

namespace Gmulator.Interfaces
{
    internal interface ISaveState
    {
        void Save(BinaryWriter bw);
        void Load(BinaryReader br);
    }
}
