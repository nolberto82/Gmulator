using ImGuiNET;
using Raylib_cs;
using rlImGui_cs;
using System.Numerics;
using Color = Raylib_cs.Color;
using Rectangle = Raylib_cs.Rectangle;

namespace Gmulator.Ui;
public class GraphicsWindow
{
    private static readonly Texture2D[] TilesTex;
    private static Texture2D SpritesTex;
    private static Texture2D NametableTex;
    private static uint[][] TileBuffer;
    private static uint[] SpriteBuffer;
    private static uint[] MapBuffer;

    public static void Init()
    {
        SpriteBuffer = new uint[256 * 256 * 4];

        MapBuffer = new uint[256 * 256 * 4];
        TileBuffer = new uint[2][];
        TileBuffer[0] = new uint[128 * 256 * 4];
        TileBuffer[1] = new uint[128 * 256 * 4];

        //NametableTex = Texture.CreateTexture(MapBuffer, 256, 256);
        //SpritesTex = Texture.CreateTexture(SpriteBuffer, 256, 256);
        //TilesTex = new Texture2D[2];
        //TilesTex[0] = Texture.CreateTexture(TileBuffer[0], 128, 256);
        //TilesTex[1] = Texture.CreateTexture(TileBuffer[1], 128, 256);
    }

    public static void RenderPpuDebug(ImFontPtr consolas, uint frame)
    {
        //RenderNametable(ref MapBuffer);
        //if ((frame % 2) == 0)
        {
            //Texture.Update(NametableTex, MapBuffer);
            rlImGui.Image(NametableTex);
        }
    }

    public static void DrawSprite(bool CGB)
    {

    }

    public static void Unload()
    {
        Raylib.UnloadTexture(SpritesTex);
        Raylib.UnloadTexture(NametableTex);
        Raylib.UnloadTexture(TilesTex[0]);
        Raylib.UnloadTexture(TilesTex[1]);
    }
}
