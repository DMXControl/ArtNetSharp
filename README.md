# ArtNetSharp

ArtNetSharp is a modern, cross-platform C# library for working with the Art-Net protocol (version 4).  
It enables you to send and receive DMX data over Ethernet, making it ideal for lighting control, stage automation, and interactive installations.

## Features

- Full Art-Net 4 protocol support
- Send and receive DMX data
- Discover and manage Art-Net nodes
- Cross-platform: Windows, Linux, and macOS works on .NET 6, .NET 7, .NET 8, .NET 9 and in future upcomming versions
- Easy-to-use API
- Actively maintained

## Installation

Install via NuGet:
Or via the NuGet Package Manager in Visual Studio.

## Quickstart

### Controller Example

Send DMX data to an Art-Net node:
[Full Example](https://github.com/DMXControl/ArtNetSharp/blob/main/Examples/ControllerExample/Program.cs)

### Node Output Example

Create an Art-Net node that outputs DMX data:
[Full Example](https://github.com/DMXControl/ArtNetSharp/blob/main/Examples/NodeOutputExample/Program.cs)

### Node Input Example

Receive DMX data from Art-Net:
[Full Example](https://github.com/DMXControl/ArtNetSharp/blob/main/Examples/NodeInputExample/Program.cs)

## Documentation

- [API Reference](https://github.com/DMXControl/ArtNetSharp/wiki)
- [Art-Net Protocol Specification](https://art-net.org.uk/downloads/art-net.pdf)

## Supported Platforms

- .NET 6 (not recommended)
- .NET 7 (not recommended)
- .NET 8
- .NET 9
- .NET 10 (upcoming)

## Contributing

Contributions are welcome! Please open issues or submit pull requests via GitHub.

1. Fork the repository
2. Create a new branch (`git checkout -b feature/your-feature`)
3. Commit your changes (`git commit -am 'Add new feature'`)
4. Push to the branch (`git push origin feature/your-feature`)
5. Open a pull request

## License

This project is licensed under the Creative Commons Attribution-NonCommercial 4.0 International Public License. See the [LICENSE](LICENSE.md) file for details.


---

**ArtNetSharp** is developed and maintained by [DMXControl Projects e.V.](https://www.dmxcontrol.de/).
