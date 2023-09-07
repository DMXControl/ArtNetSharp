using ArtNetSharp.Communication;
using RDMSharp;
using RDMSharp.ParameterWrapper;

namespace ArtNetTests.Mocks
{
    internal class ControllerInstanceMock : ControllerInstance
    {
        public override RDMUID UID => new RDMUID((ushort)EManufacturer.DMXControlProjects_eV, 12314);
    }
}
