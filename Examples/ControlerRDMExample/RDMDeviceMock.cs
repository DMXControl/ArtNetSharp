using ArtNetSharp;
using ArtNetSharp.Communication;
using RDMSharp;
using RDMSharp.ParameterWrapper;

namespace ControlerRDMExample
{
    public abstract class AbstractRDMDeviceGeneratedMock : AbstractGeneratedRDMDevice
    {
        internal static ControllerInstance Controller = ArtNet.Instance.Instances.OfType<ControllerInstance>().First();
        public AbstractRDMDeviceGeneratedMock(RDMUID uid, ERDM_Parameter[] parameters, string manufacturer = null) : base(uid, parameters, manufacturer)
        {
            Controller.ControllerRDMMessageReceived += Controller_ControllerRDMMessageReceived;
        }

        protected override async Task SendRDMMessage(RDMMessage rdmMessage)
        {
            if (this.UID == rdmMessage.DestUID || this.UID == rdmMessage.SourceUID)
            {
                Console.WriteLine("S:" + rdmMessage);
                await Controller.SendArtRDM(rdmMessage);
            }
        }

        private async void Controller_ControllerRDMMessageReceived(object? sender, ArtNetSharp.Misc.ControllerRDMMessageReceivedEventArgs e)
        {
            if (e.Handled)
                return;
            if (e.Request.DestUID.IsBroadcast || this.UID == e.Request.DestUID || this.UID == e.Request.SourceUID)
            {
                RDMMessage response = null;
                try
                {
                    response = processRequestMessage(e.Request);
                }
                catch (Exception ex)
                {
                }
                if (response != null)
                    e.SetResponse(response);

                Console.WriteLine($"Request:{Environment.NewLine}{e.Request}");
                if (e.Handled)
                    Console.WriteLine($"Response:{Environment.NewLine}{e.Response}");
            }
        }
    }
    public class TestRDMDevice : AbstractRDMDeviceGeneratedMock
    {
        public override EManufacturer ManufacturerID => (EManufacturer)0x9fff;
        public override ushort DeviceModelID => 20;
        public override ERDM_ProductCategoryCoarse ProductCategoryCoarse => ERDM_ProductCategoryCoarse.CONTROL;
        public override ERDM_ProductCategoryFine ProductCategoryFine => ERDM_ProductCategoryFine.DATA_CONVERSION;
        public override uint SoftwareVersionID => 0x1234;
        public override string DeviceModelDescription => "Test Model Description";
        public override bool SupportDMXAddress => true;

        private static GeneratedPersonality[] PERSONALITYS = [new GeneratedPersonality(1, 5, "5CH RGB"), new GeneratedPersonality(2, 8, "8CH RGBAWY"), new GeneratedPersonality(3, 9, "9CH RGB 16-Bit")];
        public override GeneratedPersonality[] Personalities => PERSONALITYS;
        public TestRDMDevice(RDMUID uid) : base(uid, [ERDM_Parameter.IDENTIFY_DEVICE, ERDM_Parameter.BOOT_SOFTWARE_VERSION_LABEL], "Dummy Manufacturer 9FFF")
        {
            this.DeviceLabel = "Dummy Device 1";
            this.TrySetParameter(ERDM_Parameter.IDENTIFY_DEVICE, false);
            this.TrySetParameter(ERDM_Parameter.BOOT_SOFTWARE_VERSION_LABEL, $"Dummy Software");
        }
    }
}
