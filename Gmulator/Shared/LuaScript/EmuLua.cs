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
    private readonly Lua _state;

    public static List<LuaMemCallback> MemCallbacks { get; private set; }
    public static List<LuaEventCallback> EventCallbacks { get; private set; }

    public enum Type
    {
        Read, Write, Exec, Frame
    }

    public enum CpuType
    {
        None, Gb, Nes, Snes, Sa1
    }

    public enum MemType
    {
        None, GbDebug, NesDebug, SnesDebug, Sa1Debug
    }

    public EmuLua(Lua state)
    {
        _state = state;
        MemCallbacks = [];
        EventCallbacks = [];

        //if (MemCallbacks.Count != 0 || EventCallbacks.Count != 0)
        //    RemoveCallbacks();

        _state.NewTable("emu");
        _state.RegisterFunction("emu.memcallback", this, typeof(EmuLua).GetMethod("AddMemCallback"));
        _state.RegisterFunction("emu.eventcallback", this, typeof(EmuLua).GetMethod("AddEventCallback"));
        _state.RegisterFunction("emu.log", this, typeof(EmuLua).GetMethod("Log"));

        LuaRegistrationHelper.Enumeration<Type>(_state);
        LuaRegistrationHelper.Enumeration<CpuType>(_state);
        LuaRegistrationHelper.Enumeration<MemType>(_state);
    }

    public static void AddMemCallback(Action<int> func, Type type, int start, int end = -1, CpuType cpuType = CpuType.None, MemType memType = MemType.None)
    {
        if (type == Type.Exec)
            MemCallbacks.Add(new(func, type, start, end, cpuType, memType));
    }

    public static void AddEventCallback(Action func, Type type)
    {
        if (type == Type.Frame)
            EventCallbacks.Add(new(func, type));
    }

    public static void Log(object msg)
    {
        if (msg != null)
        {
            ImGui.SetWindowPos(new(0, 0), ImGuiCond.Once);
            ImGui.SetNextWindowSize(new(200, 0), ImGuiCond.Once);
            if (ImGui.Begin("log"))
            {
                ImGui.Text($"{msg}");
            }
            ImGui.End();
        }
    }

    public class LuaMemCallback(Action<int> Action, Type Type, int StartAddr, int EndAddr = -1, CpuType cpuType = default, MemType memType = default)
    {
        public Action<int> Action { get; } = Action;
        public Type Type { get; } = Type;
        public int StartAddr { get; } = StartAddr;
        public int EndAddr { get; } = EndAddr;
        public CpuType CpuType { get; } = cpuType;
        public MemType MemType { get; } = memType;
    }

    public class LuaEventCallback(Action Action, Type Type)
    {
        public Action Action { get; } = Action;
        public Type Type { get; } = Type;
    }
}
