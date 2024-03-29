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


// Add Instance
ArtNet.Instance.AddInstance(controllerInstance);

// Configure Ports
for (ushort i = 1; i <= 4; i++)
{
    try
    {
        var outputConfig = new PortConfig((byte)i, new PortAddress((ushort)(i - 1)), true, false) { PortNumber = (byte)i, Type = EPortType.OutputFromArtNet, GoodOutput = EGoodOutput.ContiniuousOutput | EGoodOutput.DataTransmitted, };
        outputConfig.AddAdditionalRdmUIDs(generateUIDs());
        controllerInstance.AddPortConfig(outputConfig);
        controllerInstance.AddPortConfig(new PortConfig((byte)(i+4), new PortAddress((ushort)(i - 1)), false, true) { PortNumber = (byte)i, Type = EPortType.InputToArtNet | EPortType.ArtNet });
    }
    catch (Exception ex)
    {

    }
}

RDMUID[] generateUIDs()
{
    RDMUID[] uids = new RDMUID[30];
    for (int i = 0; i < uids.Length; i++)
    {
        var uid = new RDMUID(0x9fff, (uint)devices.Count + 1);
        devices.TryAdd(uid, new TestRDMDevice(uid));
        uids[i] = uid;
    }
    return uids;
}
Console.ReadLine();