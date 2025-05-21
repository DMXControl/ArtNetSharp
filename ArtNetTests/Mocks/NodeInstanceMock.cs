using ArtNetSharp;
using ArtNetSharp.Communication;

namespace ArtNetTests.Mocks
{
    internal class NodeInstanceMock : NodeInstance
    {
        private readonly ushort _oemProductCode;
        public override ushort OEMProductCode
        {
            get { return this._oemProductCode; }
        }
        public override ushort ESTAManufacturerCode => (ushort)Tools.ParseDotNetMajorVersion();

        public NodeInstanceMock(ArtNet artnet, ushort oemProductCode = Constants.DEFAULT_OEM_CODE) : base(artnet)
        {
            _oemProductCode = oemProductCode;
        }
    }
}
