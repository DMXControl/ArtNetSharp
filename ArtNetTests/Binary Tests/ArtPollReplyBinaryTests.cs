using ArtNetSharp;
using Microsoft.Extensions.Logging;
using RDMSharp;

namespace ArtNetTests.Binary_Tests
{
    [Order(3)]
    [Category("Binary")]
    [TestFixtureSource(typeof(AbstractArtPollReplyBinaryTestSubject), nameof(AbstractArtPollReplyBinaryTestSubject.TestSubjects))]
    public class ArtPollReplyBinaryTests
    {
        private static readonly ILogger Logger = ArtNetSharp.Logging.CreateLogger<ArtPollReplyBinaryTests>();
        private readonly AbstractArtPollReplyBinaryTestSubject testSubject;

        public ArtPollReplyBinaryTests(AbstractArtPollReplyBinaryTestSubject _TestSubject)
        {
            testSubject = _TestSubject;
            Logger.LogDebug($"Initialize Test for {nameof(ArtPollReplyBinaryTests)} ({testSubject.ToString()})");
        }


        [Test]
        public void TestAll()
        {
            Logger.LogDebug($"Run Test for {nameof(ArtPollReplyBinaryTests)} ({testSubject.ToString()})");
            ArtPollReply? artPollReply = null;
            Assert.Multiple(() =>
            {
                Assert.DoesNotThrow(() => artPollReply = new ArtPollReply(testSubject.Data));
                Assert.That(artPollReply, Is.Not.Null);
                Assert.That(artPollReply!.Net, Is.EqualTo(testSubject.Net));
                Assert.That(artPollReply.LongName, Is.EqualTo(testSubject.LongName));
                Assert.That(artPollReply.ShortName, Is.EqualTo(testSubject.ShortName));
                Assert.That(artPollReply.MAC, Is.EqualTo(testSubject.MACAddress));
                Assert.That(artPollReply.OwnIp, Is.EqualTo(testSubject.IP));
                Assert.That(artPollReply.BindIp, Is.EqualTo(testSubject.BindIP));
                Assert.That(artPollReply.OemCode, Is.EqualTo(testSubject.OemCode));
                Assert.That(artPollReply.ManufacturerCode, Is.EqualTo(testSubject.ManufacturerCode));
                Assert.That(artPollReply.Style, Is.EqualTo(testSubject.StyleCode));
                
                if(testSubject.MajorVersion.HasValue)
                    Assert.That(artPollReply.MajorVersion, Is.EqualTo(testSubject.MajorVersion));
                if (testSubject.MinorVersion.HasValue)
                    Assert.That(artPollReply.MinorVersion, Is.EqualTo(testSubject.MinorVersion));

                Assert.That(artPollReply.Ports, Is.EqualTo(testSubject.Ports.Length));
                Assert.That(artPollReply.PortTypes, Has.Length.EqualTo(testSubject.Ports.Length));
                Assert.That(artPollReply.OutputUniverses, Has.Length.EqualTo(testSubject.Ports.Length));
                Assert.That(artPollReply.InputUniverses, Has.Length.EqualTo(testSubject.Ports.Length));
                Assert.That(artPollReply.GoodOutput, Has.Length.EqualTo(testSubject.Ports.Length));
                Assert.That(artPollReply.GoodInput, Has.Length.EqualTo(testSubject.Ports.Length));
                for (byte port = 0; port < artPollReply.Ports; port++)
                {
                    string portString = $"PortIndex: {port}";
                    var config = testSubject.Ports[port];
                    Assert.That(artPollReply.PortTypes[port], Is.EqualTo(config.PortType), portString);
                    if (artPollReply.Status.PortAddressBitResolution == NodeStatus.EPortAddressBitResolution._8Bit)
                    {
                        Assert.That(artPollReply.OutputUniverses[port], Is.EqualTo(config.OutputUniverse), portString);
                        Assert.That(artPollReply.InputUniverses[port], Is.EqualTo(config.InputUniverse), portString);
                    }
                    else if (artPollReply.OutputUniverses[port] is Universe outputUniverse && artPollReply.InputUniverses[port] is Universe inputUniverse)
                    {
                        PortAddress outputPort = new PortAddress(artPollReply.Net, artPollReply.Subnet, outputUniverse);
                        PortAddress inputPort = new PortAddress(artPollReply.Net, artPollReply.Subnet, inputUniverse);
                        Assert.That(outputPort, Is.EqualTo(config.OutputUniverse), portString);
                        Assert.That(inputPort, Is.EqualTo(config.InputUniverse), portString);
                    }
                    else
                    {
                        Assert.Fail("Not implementet this case");
                    }
                }
            });

            if (testSubject.NodeReport.HasValue)
                Assert.That(artPollReply.NodeReport, Is.EqualTo(testSubject.NodeReport.Value));

            if (testSubject.DataLengthIsEqual)
                Assert.That(artPollReply.GetPacket().Take(testSubject.Data.Length), Is.EqualTo(testSubject.Data));
        }
    }
}