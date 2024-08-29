namespace ArtNetSharp
{
    public enum EBackgroundQueuePolicy : byte
    {
        STATUS_NONE = 0,
        STATUS_ADVISORY = 1,
        STATUS_WARNING = 2,
        STATUS_ERROR = 3,
        DISABLED = 4,
        //Value 5-250 manufacturer defined.
        RESERVED_251 = 251,
        RESERVED_252 = 252,
        RESERVED_253 = 253,
        RESERVED_254 = 254,
        RESERVED_255 = 255
    }
}