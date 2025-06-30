u8 = mem.readbyte
u16 = mem.readword

function display_stats()
    local v =
    {
        "Powerup   : " .. string.format("%X",u8(0x7e0019)),
    }
    gui.drawwin("info", v)
end

function emu.update()
    display_stats()
end