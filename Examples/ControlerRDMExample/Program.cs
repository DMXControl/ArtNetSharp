using ArtNetSharp;
using ArtNetSharp.Communication;
using ControlerRDMExample;
using org.dmxc.wkdt.Light.RDM;
using System.Collections.Concurrent;

Console.WriteLine("Controller RDM Example!");

//Add Logging
//ArtNet.SetLoggerFectory(YOUR_LOGGER_FACTORY);

//Set Networkinterfaces
//var broadcastIp = new IPAddress(new byte[] { 2, 255, 255, 255 });
//ArtNet.Instance.NetworkClients.ToList().ForEach(ncb => ncb.Enabled = IPAddress.Equals(broadcastIp, ncb.BroadcastIpAddress));

// Create Instance
ControllerInstance controllerInstance = new ControllerInstance(ArtNet.Instance);
controllerInstance.Name = controllerInstance.ShortName = "Controller RDM Example";
ConcurrentDictionary<UID, TestRDMDevice> devices = new();


// Add Instance
ArtNet.Instance.AddInstance(controllerInstance);

// Configure Ports
for (ushort i = 1; i <= 1; i++)
{
    try
    {
        var outputConfig = new PortConfig((byte)i, new PortAddress((ushort)(i - 1)), true, false) { PortNumber = (byte)i, Type = EPortType.OutputFromArtNet, GoodOutput = new GoodOutput(outputStyle: GoodOutput.EOutputStyle.Continuous, isBeingOutputAsDMX: true), };
        outputConfig.AddAdditionalRdmUIDs(generateUIDs(i));
        controllerInstance.AddPortConfig(outputConfig);
        controllerInstance.AddPortConfig(new PortConfig((byte)(i + 4), new PortAddress((ushort)(i - 1)), false, true) { PortNumber = (byte)i, Type = EPortType.InputToArtNet | EPortType.ArtNet });
    }
    catch (Exception ex)
    {
        Console.WriteLine(ex.ToString());
    }
}

UID[] generateUIDs(ushort port)
{
    UID[] uids = new UID[5];
    for (int i = 0; i < uids.Length; i++)
    {
        var uid = new UID(0x9fff, (uint)(devices.Count + 1 + (1000 * port)));
        devices.TryAdd(uid, new TestRDMDevice(uid));
        uids[i] = uid;
    }
    return uids;
}
Console.ReadLine();