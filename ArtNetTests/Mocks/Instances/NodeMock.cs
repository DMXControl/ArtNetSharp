using ArtNetSharp;
using ArtNetSharp.Communication;

namespace ArtNetTests.Mocks.Instances
{
    internal class NodeMock : NodeInstance
    {
        public NodeMock(ArtNet _artnet) : base(_artnet)
        {
        }

        protected override bool SendArtData => true;
        protected override string UrlProduct => "https://github.com/DMXControl/ArtNetSharp";
        protected override string UrlSupport => "https://dmxcontrol-projects.org";
    }
}
