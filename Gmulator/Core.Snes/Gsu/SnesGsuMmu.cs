using Gmulator.Interfaces;
using System.Xml.Linq;

namespace Gmulator.Core.Snes.Gsu;

public class SnesGsuMmu : ISaveState
{
    private byte[] _ram;
    private string _gameName;
    private int _ramSize;
    private Timer _saveTimer;

    public void Reset(int size, string name)
    {
        _ram = new byte[size];
        _ramSize = size - 1;
        _gameName = name;
        LoadSram();
    }

    public int Read(int addr)
    {
        return _ram[addr & _ramSize];
    }

    public void Write(int addr, int value)
    {
        _ram[addr & _ramSize] = (byte)value;
        _saveTimer ??= new Timer(SaveSram, null, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5));
    }

    public void LoadSram()
    {
        var name = Path.GetFileNameWithoutExtension($"{_gameName}");
        if (File.Exists($"{SaveDirectory}/{name}.srm"))
        {
            var data = File.ReadAllBytes($"{SaveDirectory}/{name}.srm");
            if (data?.Length > 0)
                _ram = data;
        }
    }

    private void SaveSram(object state)
    {
        try
        {
            var name = Path.GetFileNameWithoutExtension($"{_gameName}");
            File.WriteAllBytes($"{SaveDirectory}/{name}.srm", _ram);
        }
        catch (IOException)
        {
            _saveTimer?.Dispose();
            _saveTimer = null;
        }
    }

    public void Save(BinaryWriter bw)
    {
        WriteArray(bw, _ram);
    }

    public void Load(BinaryReader br)
    {
        _ram = ReadArray<byte>(br, _ram.Length);
    }
}
