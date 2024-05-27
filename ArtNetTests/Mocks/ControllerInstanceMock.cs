using ArtNetSharp;
using ArtNetSharp.Communication;
using org.dmxc.wkdt.Light.RDM;
using RDMSharp.ParameterWrapper;

namespace ArtNetTests.Mocks
{
    internal class ControllerInstanceMock : ControllerInstance
    {
        private readonly ushort _oemProductCode;
        public override ushort OEMProductCode
        {
            get { return this._oemProductCode; }
        }
        public override UID UID => new UID((ushort)EManufacturer.DMXControlProjects_eV, 12314);

        public ControllerInstanceMock(ArtNet artnet, ushort oemProductCode = Constants.DEFAULT_OEM_CODE) : base(artnet)
        {
            _oemProductCode = oemProductCode;
        }
    }
}
