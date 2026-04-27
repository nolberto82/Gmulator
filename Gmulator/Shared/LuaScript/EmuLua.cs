using ImGuiNET;
using NLua;
using System;
using System.Collections.Generic;
using System.Text;
using static Gmulator.Shared.LuaScript.LuaManager;
using Type = Gmulator.Shared.LuaScript.LuaManager.Type;

namespace Gmulator.Shared.LuaScript;

public partial class EmuLua
{
    private NLua.Lua _state;

    public static List<LuaMemCallback> MemCallbacks { get; private set; }
    public static List<LuaEventCallback> EventCallbacks { get; private set; }

    public enum Type
    {
        Read, Write, Exec, Frame
    }

    public EmuLua(NLua.Lua state)
    {
        _state = state;
        MemCallbacks = [];
        EventCallbacks = [];

        //if (MemCallbacks.Count != 0 || EventCallbacks.Count != 0)
        //    RemoveCallbacks();

        _state.NewTable("emu");
        _state.RegisterFunction("emu.memcallback",this,typeof(EmuLua).GetMethod("AddMemCallback"));
        _state.RegisterFunction("emu.eventcallback", this, typeof(EmuLua).GetMethod("AddEventCallback"));
        _state.RegisterFunction("emu.log", this, typeof(EmuLua).GetMethod("AddMemCallback"));

        LuaRegistrationHelper.Enumeration<Type>(_state);
    }

    public void AddMemCallback(Action<int> func, Type type, int start, int end = -1)
    {
        if (type == Type.Exec)
            MemCallbacks.Add(new(func, type, start, end));
    }

    public void AddEventCallback(Action func, Type type)
    {
        if (type == Type.Frame)
            EventCallbacks.Add(new(func, type));
    }

    public class LuaMemCallback(Action<int> Action, Type Type, int StartAddr, int EndAddr = -1)
    {
        public Action<int> Action { get; } = Action;
        public Type Type { get; } = Type;
        public int StartAddr { get; } = StartAddr;
        public int EndAddr { get; } = EndAddr;
    }

    public class LuaEventCallback(Action Action, Type Type)
    {
        public Action Action { get; } = Action;
        public Type Type { get; } = Type;
    }
}
