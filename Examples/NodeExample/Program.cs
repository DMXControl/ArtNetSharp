﻿using ArtNetSharp;
using ArtNetSharp.Communication;

Console.WriteLine("Node Exampler!");

// Create Instance
NodeInstance nodeInstance = new NodeInstance();
nodeInstance.Name = "Test";

// Configure Ports
for (ushort i = 0; i < 32; i++)
    nodeInstance.AddPortConfig(new PortConfig(i, true, false) { PortNumber = (byte)i, Type = EPortType.OutputFromArtNet, GoodOutput = EGoodOutput.ContiniuousOutput | EGoodOutput.DataTransmitted });

// Listen for new Data
nodeInstance.DMXReceived += (sender, e) => 
{
    if (!(sender is NodeInstance ni))
        return;

    // Can be called from anywere anytime without listen to the Event!!!
    var data = ni.GetReceivedDMX(e);

    Console.WriteLine($"Received date for {e}: {data.Length} bytes");
};

// Add Instance
ArtNet.Instance.AddInstance(nodeInstance);

Console.WriteLine("Press any key to Exit!");
Console.ReadLine();