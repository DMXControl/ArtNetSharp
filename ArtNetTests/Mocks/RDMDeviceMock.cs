using ArtNetSharp;
using RDMSharp;

namespace ArtNetTests.Mocks
{
    public class RDMDeviceMock : AbstractRDMDevice<RDMDeviceModelMock>
    {
        internal static ControllerInstanceMock Controller = ArtNet.Instance.Instances.OfType<ControllerInstanceMock>().First();
        public RDMDeviceMock(RDMUID uid) : base(uid)
        {
#if DEBUG
            if (uid.Manufacturer == RDMSharp.ParameterWrapper.EManufacturer.DMXControlProjects_eV)
                return;
#endif
            Controller.RDMMessageReceived += Controller_RDMMessageReceived;
        }

        protected override async Task SendRDMMessage(RDMMessage rdmMessage)
        {
#if DEBUG
            if (rdmMessage.DestUID.Manufacturer == RDMSharp.ParameterWrapper.EManufacturer.DMXControlProjects_eV)
                return;
#endif
            await Controller.SendArtRDM(rdmMessage);
        }

        private async void Controller_RDMMessageReceived(object? sender, RDMMessage e)
        {
#if DEBUG
            if (e.SourceUID.Manufacturer == RDMSharp.ParameterWrapper.EManufacturer.DMXControlProjects_eV)
                return;
#endif
            await ReceiveRDMMessage(e);
        }
    }
}
