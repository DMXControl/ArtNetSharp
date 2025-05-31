using ArtNetSharp;
using ArtNetSharp.Communication;
using org.dmxc.wkdt.Light.RDM;

namespace ArtNetTests.Mocks
{
    internal class ControllerInstanceMock : ControllerInstance
    {
        private readonly ushort _oemProductCode;
        public override ushort OEMProductCode
        {
            get { return this._oemProductCode; }
        }
        public override ushort ESTAManufacturerCode => (ushort)Tools.ParseDotNetMajorVersion();
        public override UID UID => new UID(0x02b0, 12314);

        public ControllerInstanceMock(ArtNet artnet, ushort oemProductCode = Constants.DEFAULT_OEM_CODE) : base(artnet)
        {
            _oemProductCode = oemProductCode;
        }
    }
}
