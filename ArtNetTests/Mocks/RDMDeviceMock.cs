using ArtNetSharp;
using ArtNetSharp.Misc;
using org.dmxc.wkdt.Light.RDM;
using RDMSharp;

namespace ArtNetTests.Mocks
{
    public class RDMDeviceMock : AbstractRDMDevice
    {
        private readonly ArtNet artnet;
        internal ControllerInstanceMock Controller => artnet.Instances.OfType<ControllerInstanceMock>().First();
        public RDMDeviceMock(UID uid, ArtNet _artnet) : base(uid)
        {
            artnet = _artnet;
#if DEBUG
            if (uid.ManufacturerID == (ushort)RDMSharp.ParameterWrapper.EManufacturer.DMXControlProjects_eV)
                return;
#endif
            Controller.ResponderRDMMessageReceived += Controller_RDMMessageReceived;
        }

        protected override async Task SendRDMMessage(RDMMessage rdmMessage)
        {
#if DEBUG
            if (rdmMessage.DestUID.ManufacturerID == (ushort)RDMSharp.ParameterWrapper.EManufacturer.DMXControlProjects_eV)
                return;
#endif
            await Controller.SendArtRDM(rdmMessage);
        }

        private async void Controller_RDMMessageReceived(object? sender, ResponderRDMMessageReceivedEventArgs e)
        {
#if DEBUG
            if (e.Response.SourceUID.ManufacturerID == (ushort)RDMSharp.ParameterWrapper.EManufacturer.DMXControlProjects_eV)
                return;
#endif
            await ReceiveRDMMessage(e.Response);
        }
    }
}
