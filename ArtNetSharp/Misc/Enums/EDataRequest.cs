using System;

namespace ArtNetSharp
{
    [Flags]
    public enum EDataRequest : ushort
    {
        Poll = 0,
        UrlProduct = 1,
        UrlUserGuide = 2,
        UrlSupport = 3,
        UrlPersUdr = 4,
        UrlPersGdtf = 5,
        //0x8000- 0xffff DrManSpec Manufacturer specific use.
    }
}