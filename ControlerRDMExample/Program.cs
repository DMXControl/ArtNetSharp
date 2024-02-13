﻿using ArtNetSharp;
using ArtNetSharp.Communication;
using ControlerRDMExample;
using RDMSharp;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;

Console.WriteLine("Controller RDM Example!");

//Add Logging
//ArtNet.SetLoggerFectory(YOUR_LOGGER_FACTORY);

//Set Networkinterfaces
//var broadcastIp = new IPAddress(new byte[] { 2, 255, 255, 255 });
//ArtNet.Instance.NetworkClients.ToList().ForEach(ncb => ncb.Enabled = IPAddress.Equals(broadcastIp, ncb.BroadcastIpAddress));

// Create Instance
ControllerInstance controllerInstance = new ControllerInstance();
controllerInstance.Name = controllerInstance.ShortName = "Controller RDM Example";
ConcurrentDictionary<RDMUID, TestRDMDevice> devices = new();
controllerInstance.RDMMessageReceived += ControllerInstance_RDMMessageReceived;

void ControllerInstance_RDMMessageReceived(object? sender, RDMMessage e)
{
    if (e == null)
        return;

    if (e.DestUID.IsBroadcast)
        return;
    if(!e.Command.HasFlag(ERDM_Command.RESPONSE) && devices.ContainsKey(e.DestUID))
    {

    }
}


// Add Instance
ArtNet.Instance.AddInstance(controllerInstance);

// Configure Ports
for (ushort i = 0; i < 4; i++)
{
    var outputConfig = new PortConfig(i, true, false) { PortNumber = (byte)i, Type = EPortType.OutputFromArtNet, GoodOutput = EGoodOutput.ContiniuousOutput | EGoodOutput.DataTransmitted, };
    outputConfig.AddAdditionalRdmUIDs(generateUIDs());
    controllerInstance.AddPortConfig(outputConfig);
    controllerInstance.AddPortConfig(new PortConfig(i, false, true) { PortNumber = (byte)i, Type = EPortType.InputToArtNet | EPortType.ArtNet });
}

RDMUID[] generateUIDs()
{
    Random random = new Random();
    RDMUID[] uids = new RDMUID[100];
    for (int i = 0; i < uids.Length; i++)
    {
        var uid = new RDMUID(0x9fff, (uint)random.Next());
        devices.TryAdd(uid, new TestRDMDevice(uid));
        uids[i] = uid;
    }
    return uids;
}

// Genrerate some DMX-Data
byte[] data = new byte[512];
while (true)
{
    await Task.Delay(200);
    for (short k = 0; k < 512; k++)
        data[k]++;

    for (ushort i = 0; i < 32; i++)
        controllerInstance.WriteDMXValues(i, data);
}