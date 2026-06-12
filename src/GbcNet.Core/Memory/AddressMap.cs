namespace GbcNet.Core.Memory;

/// <summary>
/// Defines the 16-bit Game Boy memory map ranges.
/// </summary>
internal static class AddressMap
{
    /// <summary>
    /// First address of the cartridge ROM window.
    /// </summary>
    public const ushort RomStart = 0x0000;

    /// <summary>
    /// Address where the boot ROM hands execution to the cartridge entry point.
    /// </summary>
    public const ushort CartridgeEntryPointStart = 0x0100;

    /// <summary>
    /// Last address of the cartridge ROM window.
    /// </summary>
    public const ushort RomEnd = 0x7FFF;

    /// <summary>
    /// First address of video RAM.
    /// </summary>
    public const ushort VideoRamStart = 0x8000;

    /// <summary>
    /// Last address of video RAM.
    /// </summary>
    public const ushort VideoRamEnd = 0x9FFF;

    /// <summary>
    /// First address of cartridge external RAM.
    /// </summary>
    public const ushort ExternalRamStart = 0xA000;

    /// <summary>
    /// Last address of cartridge external RAM.
    /// </summary>
    public const ushort ExternalRamEnd = 0xBFFF;

    /// <summary>
    /// Size of the CPU-visible cartridge external RAM window.
    /// </summary>
    public const int ExternalRamWindowSize = 8 * 1024;

    /// <summary>
    /// First address of work RAM.
    /// </summary>
    public const ushort WorkRamStart = 0xC000;

    /// <summary>
    /// Last address of WRAM bank 0, fixed on all models.
    /// </summary>
    public const ushort WorkRamFixedBankEnd = 0xCFFF;

    /// <summary>
    /// First address of the switchable WRAM bank window.
    /// </summary>
    public const ushort WorkRamSwitchableBankStart = 0xD000;

    /// <summary>
    /// Last address of directly addressable work RAM.
    /// </summary>
    public const ushort WorkRamEnd = 0xDFFF;

    /// <summary>
    /// First address of echo RAM, mirroring work RAM at C000-DDFF.
    /// </summary>
    public const ushort EchoRamStart = 0xE000;

    /// <summary>
    /// Last address of echo RAM.
    /// </summary>
    public const ushort EchoRamEnd = 0xFDFF;

    /// <summary>
    /// First address of object attribute memory.
    /// </summary>
    public const ushort ObjectAttributeMemoryStart = 0xFE00;

    /// <summary>
    /// Last address of object attribute memory.
    /// </summary>
    public const ushort ObjectAttributeMemoryEnd = 0xFE9F;

    /// <summary>
    /// First address of the prohibited not-usable range.
    /// </summary>
    public const ushort NotUsableStart = 0xFEA0;

    /// <summary>
    /// Last address of the prohibited not-usable range.
    /// </summary>
    public const ushort NotUsableEnd = 0xFEFF;

    /// <summary>
    /// First address of I/O registers.
    /// </summary>
    public const ushort IoRegistersStart = 0xFF00;

    /// <summary>
    /// Address of P1/JOYP, the joypad selection and input register.
    /// </summary>
    public const ushort JoypadRegister = 0xFF00;

    /// <summary>
    /// Address of SB, the serial transfer data register.
    /// </summary>
    public const ushort SerialTransferDataRegister = 0xFF01;

    /// <summary>
    /// Address of SC, the serial transfer control register.
    /// </summary>
    public const ushort SerialTransferControlRegister = 0xFF02;

    /// <summary>
    /// Address of DIV, the CPU-visible high byte of the internal divider counter.
    /// </summary>
    public const ushort DividerRegister = 0xFF04;

    /// <summary>
    /// Address of TIMA, the programmable timer counter.
    /// </summary>
    public const ushort TimerCounterRegister = 0xFF05;

    /// <summary>
    /// Address of TMA, the value reloaded into TIMA after overflow.
    /// </summary>
    public const ushort TimerModuloRegister = 0xFF06;

    /// <summary>
    /// Address of TAC, the timer enable and clock select register.
    /// </summary>
    public const ushort TimerControlRegister = 0xFF07;

    /// <summary>
    /// Address of the interrupt flag register.
    /// </summary>
    public const ushort InterruptFlagRegister = 0xFF0F;

    /// <summary>
    /// Last address of I/O registers.
    /// </summary>
    public const ushort IoRegistersEnd = 0xFF7F;

    /// <summary>
    /// Address of LCDC, the LCD control register.
    /// </summary>
    public const ushort LcdControlRegister = 0xFF40;

    /// <summary>
    /// Address of STAT, the LCD status register.
    /// </summary>
    public const ushort LcdStatusRegister = 0xFF41;

    /// <summary>
    /// Address of SCY, the background viewport Y position register.
    /// </summary>
    public const ushort ScrollYRegister = 0xFF42;

    /// <summary>
    /// Address of SCX, the background viewport X position register.
    /// </summary>
    public const ushort ScrollXRegister = 0xFF43;

    /// <summary>
    /// Address of LY, the LCD Y coordinate register.
    /// </summary>
    public const ushort LcdYCoordinateRegister = 0xFF44;

    /// <summary>
    /// Address of LYC, the LY compare register.
    /// </summary>
    public const ushort LcdYCompareRegister = 0xFF45;

    /// <summary>
    /// Address of DMA, the OAM DMA source and start register.
    /// </summary>
    public const ushort DmaRegister = 0xFF46;

    /// <summary>
    /// Address of BGP, the DMG background palette register.
    /// </summary>
    public const ushort BackgroundPaletteRegister = 0xFF47;

    /// <summary>
    /// Address of OBP0, the DMG object palette 0 register.
    /// </summary>
    public const ushort ObjectPalette0Register = 0xFF48;

    /// <summary>
    /// Address of OBP1, the DMG object palette 1 register.
    /// </summary>
    public const ushort ObjectPalette1Register = 0xFF49;

    /// <summary>
    /// Address of WY, the window Y position register.
    /// </summary>
    public const ushort WindowYRegister = 0xFF4A;

    /// <summary>
    /// Address of WX, the window X position plus seven register.
    /// </summary>
    public const ushort WindowXRegister = 0xFF4B;

    /// <summary>
    /// Address of KEY1, the CGB speed switch preparation register.
    /// </summary>
    public const ushort Key1Register = 0xFF4D;

    /// <summary>
    /// Address of VBK, the CGB VRAM bank selection register.
    /// </summary>
    public const ushort VideoRamBankRegister = 0xFF4F;

    /// <summary>
    /// Address of HDMA1, the CGB VRAM DMA source high register.
    /// </summary>
    public const ushort VideoRamDmaSourceHighRegister = 0xFF51;

    /// <summary>
    /// Address of HDMA2, the CGB VRAM DMA source low register.
    /// </summary>
    public const ushort VideoRamDmaSourceLowRegister = 0xFF52;

    /// <summary>
    /// Address of HDMA3, the CGB VRAM DMA destination high register.
    /// </summary>
    public const ushort VideoRamDmaDestinationHighRegister = 0xFF53;

    /// <summary>
    /// Address of HDMA4, the CGB VRAM DMA destination low register.
    /// </summary>
    public const ushort VideoRamDmaDestinationLowRegister = 0xFF54;

    /// <summary>
    /// Address of HDMA5, the CGB VRAM DMA length, mode, and start register.
    /// </summary>
    public const ushort VideoRamDmaLengthModeStartRegister = 0xFF55;

    /// <summary>
    /// Address of BCPS/BGPI, the CGB background palette index register.
    /// </summary>
    public const ushort BackgroundPaletteIndexRegister = 0xFF68;

    /// <summary>
    /// Address of BCPD/BGPD, the CGB background palette data register.
    /// </summary>
    public const ushort BackgroundPaletteDataRegister = 0xFF69;

    /// <summary>
    /// Address of OCPS/OBPI, the CGB object palette index register.
    /// </summary>
    public const ushort ObjectPaletteIndexRegister = 0xFF6A;

    /// <summary>
    /// Address of OCPD/OBPD, the CGB object palette data register.
    /// </summary>
    public const ushort ObjectPaletteDataRegister = 0xFF6B;

    /// <summary>
    /// Address of OPRI, the CGB object priority mode register.
    /// </summary>
    public const ushort ObjectPriorityModeRegister = 0xFF6C;

    /// <summary>
    /// Address of the first CGB undocumented read/write register.
    /// </summary>
    public const ushort CgbUndocumentedRegisterFf72 = 0xFF72;

    /// <summary>
    /// Address of the second CGB undocumented read/write register.
    /// </summary>
    public const ushort CgbUndocumentedRegisterFf73 = 0xFF73;

    /// <summary>
    /// Address of the CGB register that stays unmapped after boot on tested CGB hardware.
    /// </summary>
    public const ushort CgbUndocumentedRegisterFf74 = 0xFF74;

    /// <summary>
    /// Address of the CGB undocumented register with writable bits 4-6.
    /// </summary>
    public const ushort CgbUndocumentedRegisterFf75 = 0xFF75;

    /// <summary>
    /// Address of PCM12, the CGB read-only digital output register for audio channels 1 and 2.
    /// </summary>
    public const ushort AudioPcm12Register = 0xFF76;

    /// <summary>
    /// Address of PCM34, the CGB read-only digital output register for audio channels 3 and 4.
    /// </summary>
    public const ushort AudioPcm34Register = 0xFF77;

    /// <summary>
    /// Address of SVBK, the CGB WRAM bank selection register.
    /// </summary>
    public const ushort WorkRamBankRegister = 0xFF70;

    /// <summary>
    /// First address of high RAM.
    /// </summary>
    public const ushort HighRamStart = 0xFF80;

    /// <summary>
    /// Last address of high RAM.
    /// </summary>
    public const ushort HighRamEnd = 0xFFFE;

    /// <summary>
    /// Address of the interrupt enable register.
    /// </summary>
    public const ushort InterruptEnableRegister = 0xFFFF;
}
