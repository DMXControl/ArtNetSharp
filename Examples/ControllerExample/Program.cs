using ArtNetSharp;
using ArtNetSharp.Communication;

Console.WriteLine("Controller Example!");

// Create Instance
ControllerInstance controllerInstance = new ControllerInstance();
controllerInstance.Name = controllerInstance.ShortName = "Controller Example";

// Configure Ports
for (ushort i = 0; i < 32; i++)
    controllerInstance.AddPortConfig(new PortConfig(i, false, true) { PortNumber = (byte)i, Type = EPortType.InputToArtNet | EPortType.ArtNet});

// Add Instance
ArtNet.Instance.AddInstance(controllerInstance);

// Genrerate some DMX-Data
byte[] data= new byte[512];
while (true)
{
    await Task.Delay(200);
    for (short k = 0; k < 512; k++)
        data[k]++;

    for (ushort i = 0; i < 32; i++)
        controllerInstance.WriteDMXValues(i, data);
}