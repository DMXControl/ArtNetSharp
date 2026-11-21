# ArtNetSharp

ArtNetSharp is a modern, cross-platform C# library for the Art-Net 4 protocol.
It sends and receives DMX512 and RDM over Ethernet and lets you build Art-Net **controllers**, **nodes** and **configuration tools**,
for lighting control, stage automation and interactive installations.

## Features

- **DMX**
  - Send DMX as a controller or an input node: unicast to subscribers only, 44 Hz rate limit, keep-alive every 800 ms, sequence numbers
  - Receive DMX as an output node, with merging per spec: HTP/LTP, at most two sources (IP + physical port), failed source held for 10 s, `GoodOutput` merge bit
  - Robust sequence handling: out-of-order packets are dropped, a restarted source is accepted again after a few packets
  - Optional `ArtSync` once per frame
- **Discovery**
  - `ArtPoll` / `ArtPollReply`, including targeted mode
  - Remote devices and their ports with timeout and revival (the same object comes back, including its RDM devices)
  - "ArtPollReply on change" for controllers that request it
- **RDM over Art-Net**
  - `ArtTodRequest` / `ArtTodControl` / `ArtTodData` (including more than 200 devices per port)
  - `ArtRdm` requests and responses, duplicate detection
  - RDM transmit queue of gateways (`FifoAvail` / `FifoMax`)
- **Remote configuration of nodes**
  - `ArtAddress`: names, Net / Sub-Net / universes (including reset), merge mode, cancel merge, indicator LED
  - `ArtInput`: enable / disable DMX inputs
  - `ArtIpProg`: IP programming handed to the application
  - Every change can be vetoed by the application
- **Diagnostics**: `ArtDiagData` sending and receiving, as requested by controllers via `ArtPoll`
- **ArtData**: product, support and user-guide URLs
- **Timecode**: `ArtTimeCode` and `ArtTimeSync`
- **Robust for host applications**: events run ordered on a queue per instance and are fail-safe, so a slow or throwing handler never blocks or breaks packet reception
- **Cross-platform**: Windows, Linux and macOS on .NET 8, 9, 10 and 11, including broadcast reception on Linux/macOS (one extra socket per interface)

## Installation

```
dotnet add package ArtNetSharp
```

Or via the NuGet Package Manager in Visual Studio.

## Quickstart

All instances live in `ArtNet.Instance`, which manages the network interfaces (one per IPv4 address) and the sockets on UDP port 6454.

### Controller: send DMX

```csharp
using ArtNetSharp;
using ArtNetSharp.Communication;

var controller = new ControllerInstance(ArtNet.Instance) { Name = "My Controller", ShortName = "MyCtrl" };

// One input port (DMX goes "into" Art-Net) per universe you want to send
var universe = new PortAddress(0, 0, 1); // Net 0, Sub-Net 0, Universe 1
controller.AddPortConfig(new PortConfig(1, universe, false, true) { PortNumber = 1, Type = EPortType.InputToArtNet });
ArtNet.Instance.AddInstance(controller);

byte[] data = new byte[512];
data[0] = 255;
controller.WriteDMXValues(universe, data);                            // whole frame
controller.WriteDMXValues(universe, new byte[] { 128 }, destinationIndex: 10, count: 1); // single channel
```

The library discovers the nodes that subscribe to the universe and sends to them at up to 44 Hz, plus a keep-alive.
It only writes when data changed, so calling `WriteDMXValues` on every change is fine.

### Node: receive DMX

```csharp
var node = new NodeInstance(ArtNet.Instance) { Name = "My Node", ShortName = "MyNode" };
node.AddPortConfig(new PortConfig(1, universe, true, false) { PortNumber = 1, Type = EPortType.OutputFromArtNet });

node.DMXReceived += (sender, portAddress) =>
{
    byte[] dmx = node.GetReceivedDMX(portAddress); // merged (HTP/LTP as configured), always a copy
    // output dmx ...
};
ArtNet.Instance.AddInstance(node);
```

### Discover devices

```csharp
controller.RemoteClientDiscovered += (_, client) => Console.WriteLine($"Found {client.LongName} at {client.IpAddress}");
controller.RemoteClientTimedOut  += (_, client) => Console.WriteLine($"Lost {client.LongName}");
foreach (var client in controller.RemoteClients)
    foreach (var port in client.Ports)
        Console.WriteLine($"{client.LongName}: out {port.OutputPortAddress} in {port.InputPortAddress}");
```

### RDM

```csharp
await controller.PerformRDMDiscovery(universe);            // ArtTodRequest -> devices in RemoteClientPort.KnownResponderRDMUIDs
controller.ResponseRDMMessageReceived += (_, e) => Console.WriteLine(e.Response);
await controller.SendArtRDM(new RDMMessage { Command = ERDM_Command.GET_COMMAND, Parameter = ERDM_Parameter.DEVICE_INFO, DestUID = uid });
```

A node with RDM devices (`new NodeInstance(ArtNet.Instance, supportRDM: true)`) answers requests in `RequestRDMMessageReceived` via `e.SetResponse(...)`.

### Remote configuration of a node

Nodes accept `ArtAddress`, `ArtInput` and `ArtIpProg` by default, as Art-Net 4 expects (e.g. from DMX-Workshop).
Every change is announced first and can be rejected:

```csharp
node.ArtAddressReceived += (_, e) =>
{
    if (!IsTrusted(e.Source))
        e.Cancel = true;              // reject; the node answers with its unchanged ArtPollReply
};
node.ArtInputReceived += (_, e) => { /* a controller enables/disables DMX inputs */ };
node.ArtIpProgReceived += (_, e) =>
{
    // The library cannot change the host's network settings. An application that can, applies
    // e.Request here and sets e.Ip / e.SubnetMask / e.DefaultGateway / e.DhcpEnabled for the reply.
};
```

To ignore remote configuration completely, derive from `NodeInstance` and override `AcceptArtAddress` to return `false`.

### Diagnostics

```csharp
// Controller side: ask devices for diagnostics in our ArtPolls
controller.RequestDiagnostics = true;
controller.DiagnosticsMinimumPriority = EPriorityCode.DpMed;
controller.DiagnosticsReceived += (_, e) => Console.WriteLine($"{e.Source}: {e.Diagnostics.Text}");

// Node side: only sent if a controller requested it (returns false otherwise)
await node.SendDiagnostics("Port 1 output short circuit", EPriorityCode.DpHigh, logicalPort: 1);
```

## Events and threading

- **Not on the receive thread:** events run on a queue per instance, ordered, outside the network receive loops. A slow handler (e.g. `Dispatcher.Invoke` on a busy UI thread) only delays later events of the same instance, never packet reception.
- **Use your own UI thread:** handlers run on a thread-pool thread, so switch to your UI thread yourself.
- **Exceptions are contained:** an exception in a handler is logged and does not affect the library or other handlers. Exceptions in your own `async void` handlers cannot be caught by any library; avoid them.
- **`DMXReceived` is coalesced** per port: at most one event per port is pending, so always read the current data with `GetReceivedDMX`.
- **Vetoable events** (`ArtAddressReceived`, `ArtInputReceived`, `ArtIpProgReceived`, `RequestRDMMessageReceived`) are awaited before the library continues, without blocking the network.

## Network notes

- **Choosing interfaces:** `ArtNet.Instance.NetworkClients` lists one client per local IPv4 address. Set `Enabled = false` on interfaces Art-Net should not use.
- **Sharing the port:** several Art-Net applications on the same machine share UDP port 6454. Unicast packets to a shared address can reach only one of them, so other Art-Net software running on the same computer can disturb communication via `127.0.0.1`.
- **Linux and macOS:** a socket bound to an interface address does not receive broadcasts there. ArtNetSharp therefore opens an extra receive socket per interface, bound to its broadcast address ([#11](https://github.com/DMXControl/ArtNetSharp/issues/11)).

## Art-Net 4 support

| Packet | Status |
|---|---|
| ArtPoll / ArtPollReply | ✅ incl. targeted mode, reply on change, bind index |
| ArtDmx | ✅ send (unicast to subscribers) and receive (merge per spec) |
| ArtSync | ✅ |
| ArtAddress | ✅ names, Port-Address incl. reset, merge LTP/HTP, cancel merge, LED; other commands reported via event |
| ArtInput | ✅ |
| ArtIpProg / ArtIpProgReply | ✅ reply with current settings; changes via application event |
| ArtDiagData | ✅ |
| ArtData / ArtDataReply | ✅ |
| ArtTimeCode / ArtTimeSync | ✅ send and receive |
| ArtTodRequest / ArtTodControl / ArtTodData | ✅ |
| ArtRdm | ✅ incl. gateway RDM queue (FifoAvail / FifoMax) |
| ArtNzs, ArtVlc, ArtRdmSub | packet classes; received packets reach `OnPacketReceived` of a derived instance |
| ArtCommand, ArtTrigger, ArtFirmware / ArtFileTn, ArtDirectory | not implemented |
| sACN switching (AcAcnSel) | not supported (Art-Net only) |

## Examples

- [Controller](https://github.com/DMXControl/ArtNetSharp/blob/main/Examples/ControllerExample/Program.cs): send DMX to 32 universes
- [Node output](https://github.com/DMXControl/ArtNetSharp/blob/main/Examples/NodeOutputExample/Program.cs): receive DMX
- [Node input](https://github.com/DMXControl/ArtNetSharp/blob/main/Examples/NodeInputExample/Program.cs): input DMX into Art-Net
- [Node with RDM](https://github.com/DMXControl/ArtNetSharp/blob/main/Examples/NodeOutputRDMExample/Program.cs): RDM devices behind an output node
- [Configuration tool](https://github.com/DMXControl/ArtNetSharp/blob/main/Examples/ConfigExample/Program.cs): discover devices and watch their ports

## Logging

ArtNetSharp logs via `Microsoft.Extensions.Logging`. Hand over your factory once at startup, before the first use of `ArtNet.Instance`:

```csharp
ArtNetSharp.Logging.LoggerFactory = yourLoggerFactory;
```

## Building and testing

```
dotnet build
dotnet test ArtNetTests
```

Most tests run without a network, using an internal loop adapter. A few use `127.0.0.1`. If other Art-Net software is running on the same machine and also listens on port 6454, those tests can fail. Tests in the category `Hardware` need specific devices and are skipped otherwise.

## Documentation

- [API Reference](https://github.com/DMXControl/ArtNetSharp/wiki)
- [Art-Net Protocol Specification](https://art-net.org.uk/downloads/art-net.pdf)

## Supported Platforms

- [![.NET 8](https://img.shields.io/badge/.NET%208-5C2D91?logo=.net&logoColor=white)](https://dotnet.microsoft.com/download/dotnet/8.0)
- [![.NET 9](https://img.shields.io/badge/.NET%209-5C2D91?logo=.net&logoColor=white)](https://dotnet.microsoft.com/download/dotnet/9.0)
- [![.NET 10](https://img.shields.io/badge/.NET%2010-5C2D91?logo=.net&logoColor=white)](https://dotnet.microsoft.com/download/dotnet/10.0)
- [![.NET 11](https://img.shields.io/badge/.NET%2011-5C2D91?logo=.net&logoColor=white)](https://dotnet.microsoft.com/download/dotnet/11.0)

Windows, Linux and macOS.

## Contributing

Contributions are welcome! Please open issues or submit pull requests via GitHub.

1. Fork the repository
2. Create a new branch (`git checkout -b feature/your-feature`)
3. Commit your changes (`git commit -am 'Add new feature'`)
4. Push to the branch (`git push origin feature/your-feature`)
5. Open a pull request

## License [![License](https://img.shields.io/badge/license-CC%20BY--NC%204.0-orange.svg)](LICENSE.md)

This project is licensed under the Creative Commons Attribution-NonCommercial 4.0 International Public License. See the [LICENSE](LICENSE.md) file for details.

---

**ArtNetSharp** is developed and maintained by [DMXControl Projects e.V.](https://www.dmxcontrol-projects.org/).

---

[![NuGet Version](https://img.shields.io/nuget/v/ArtNetSharp?color=green)](https://www.nuget.org/packages/ArtNetSharp/)
[![NuGet Downloads](https://img.shields.io/nuget/dt/ArtNetSharp?color=green)](https://www.nuget.org/packages/ArtNetSharp/)
![GitHub Top Language](https://img.shields.io/github/languages/top/DMXControl/ArtNetSharp?color=blue)
![GitHub Code Size](https://img.shields.io/github/languages/code-size/DMXControl/ArtNetSharp)

![GitHub Last Commit](https://img.shields.io/github/last-commit/DMXControl/ArtNetSharp?color=black)
![Contributions](https://img.shields.io/badge/contributions-welcome-black.svg)
![Contributors](https://img.shields.io/github/contributors/DMXControl/ArtNetSharp?color=black)
![GitHub issues](https://img.shields.io/github/issues/DMXControl/ArtNetSharp?color=black)
![GitHub pull requests](https://img.shields.io/github/issues-pr/DMXControl/ArtNetSharp?color=black)

![Discord](https://img.shields.io/discord/1051582916137861241)

![GitHub Forks](https://img.shields.io/github/forks/DMXControl/ArtNetSharp?style=social)
![GitHub Watchers](https://img.shields.io/github/watchers/DMXControl/ArtNetSharp?style=social)
![GitHub Stars](https://img.shields.io/github/stars/DMXControl/ArtNetSharp?style=social)
