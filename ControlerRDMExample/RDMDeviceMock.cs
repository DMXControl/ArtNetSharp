using ArtNetSharp;
using ArtNetSharp.Communication;
using RDMSharp;

namespace ControlerRDMExample
{
    public abstract class AbstractRDMDeviceGeneratedMock : AbstractRDMDevice
    {
        internal static ControllerInstance Controller = ArtNet.Instance.Instances.OfType<ControllerInstance>().First();
        public override bool IsGenerated => true;
        public AbstractRDMDeviceGeneratedMock(RDMUID uid) : base(uid)
        {
            Controller.RDMMessageReceived += Controller_RDMMessageReceived;
        }

        protected override async Task SendRDMMessage(RDMMessage rdmMessage)
        {
            if (this.UID == rdmMessage.DestUID || this.UID == rdmMessage.SourceUID)
            {
                Console.WriteLine("S:" + rdmMessage);
                await Controller.SendArtRDM(rdmMessage);
            }
        }

        private async void Controller_RDMMessageReceived(object? sender, RDMMessage rdmMessage)
        {
            if (rdmMessage.DestUID.IsBroadcast || this.UID == rdmMessage.DestUID || this.UID == rdmMessage.SourceUID)
            {
                Console.WriteLine("R:" + rdmMessage);
                await ReceiveRDMMessage(rdmMessage);
            }
        }
    }
    public class TestRDMDevice : AbstractRDMDeviceGeneratedMock
    {
        public TestRDMDevice(RDMUID uid) : base(uid)
        {
            this.SetGeneratedParameterValue(ERDM_Parameter.DEVICE_INFO, new RDMDeviceInfo(dmx512StartAddress: 1, deviceModelId: 20, dmx512Footprint: 11, productCategoryCoarse: ERDM_ProductCategoryCoarse.CONTROL, productCategoryFine: ERDM_ProductCategoryFine.DATA_CONVERSION, softwareVersionId: 0x1234));
            this.SetGeneratedParameterValue(ERDM_Parameter.IDENTIFY_DEVICE, false);
            this.SetGeneratedParameterValue(ERDM_Parameter.DEVICE_MODEL_DESCRIPTION, "Test Model Description");
            this.SetGeneratedParameterValue(ERDM_Parameter.DEVICE_LABEL, $"Test Device {uid}");
            this.SetGeneratedParameterValue(ERDM_Parameter.MANUFACTURER_LABEL, $"Dummy Manufacturer");
            this.SetGeneratedParameterValue(ERDM_Parameter.BOOT_SOFTWARE_VERSION_LABEL, $"Dummy Software");
            this.SetGeneratedParameterValue(ERDM_Parameter.DMX_START_ADDRESS, (ushort)1);
        }
    }
}
