using ArtNetSharp;
using ArtNetSharp.Communication;
using RDMSharp;
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
        public override RDMUID UID => new RDMUID((ushort)EManufacturer.DMXControlProjects_eV, 12314);

        public ControllerInstanceMock(ushort oemProductCode = Constants.DEFAULT_OEM_CODE) : base()
        {
            _oemProductCode = oemProductCode;
        }
    }
}
