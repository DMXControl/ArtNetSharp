using ArtNetSharp;
using RDMSharp;

namespace ArtNetTests.Mocks
{
    public class RDMDeviceMock : AbstractRDMDevice<RDMDeviceModelMock>
    {
        internal static ControllerInstanceMock Controller = ArtNet.Instance.Instances.OfType<ControllerInstanceMock>().FirstOrDefault();
        public RDMDeviceMock(RDMUID uid) : base(uid)
        {
            Controller.RDMMessageReceived += Controller_RDMMessageReceived;
        }

        protected override async Task SendRDMMessage(RDMMessage rdmMessage)
        {
            await Controller.SendArtRDM(rdmMessage);
        }

        private void Controller_RDMMessageReceived(object? sender, RDMMessage e)
        {
            ReceiveRDMMessage(e);
        }
    }
}
