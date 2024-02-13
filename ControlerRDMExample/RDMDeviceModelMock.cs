using ArtNetSharp;
using ArtNetSharp.Communication;
using RDMSharp;

namespace ControlerRDMExample
{
    public class RDMDeviceModelMock : AbstractRDMDeviceModel
    {
        internal static ControllerInstance Controller = ArtNet.Instance.Instances.OfType<ControllerInstance>().First();
        public RDMDeviceModelMock(RDMUID uid, RDMDeviceInfo deviceInfo) : base(uid, deviceInfo)
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
