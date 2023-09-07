namespace ArtNetSharp
{
    public static class Constants
    {
        public const ushort MAX_UNIVERSE = 32768;
        public const ushort ARTNET_PORT = 0x1936;
        public const ushort PROTOCOL_VERSION = 14;
        internal const ushort OEM_CODE = 0x08b0;
        internal const ushort ESTA_MANUFACTURER_CODE = 0x02B0;
        internal const ushort DEFAULT_OEM_CODE = 0xffff;
        internal const ushort DEFAULT_ESTA_MANUFACTURER_CODE = 0xffff;

        public const byte DMX_STARTCODE = 0;
        public const byte RDM_STARTCODE = 0xCC;
    }
}