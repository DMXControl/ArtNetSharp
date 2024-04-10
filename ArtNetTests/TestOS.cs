using ArtNetSharp;
using ArtNetSharp.Communication;
using ArtNetTests.Mocks;
using ArtNetTests.Mocks.Instances;
using NUnit.Framework.Internal;

namespace ArtNetTests
{
    public class TestOS
    {
        NodeInstance nodeInstance;
        ControllerInstance controllerInstance;
        byte ports = 2;
        ArtNet artNet;
        [OneTimeSetUp]
        public void Setup()
        {
            artNet = ArtNet.Instance;

            nodeInstance = new NodeMock();
            nodeInstance.Name = "Test Node";
            controllerInstance = new ControllerInstanceMock();
            controllerInstance.Name = "Test Controller";
            for (ushort i = 1; i <= ports; i++)
            {
                nodeInstance.AddPortConfig(new PortConfig((byte)i, i, true, false) { PortNumber = (byte)i, Type = EPortType.OutputFromArtNet, GoodOutput = EGoodOutput.ContiniuousOutput | EGoodOutput.DataTransmitted });
                controllerInstance.AddPortConfig(new PortConfig((byte)i, i, false, true) { PortNumber = (byte)i, Type = EPortType.InputToArtNet | EPortType.ArtNet });
            }
            artNet.AddInstance(nodeInstance);
            artNet.AddInstance(controllerInstance);
        }
        [OneTimeTearDown]
        public void Teardown()
        {
            artNet.RemoveInstance(nodeInstance);
            artNet.RemoveInstance(controllerInstance);
            ((IDisposable)nodeInstance).Dispose();
            ((IDisposable)controllerInstance).Dispose();
        }

        [Test]
        [Timeout(10000)]
        public async Task TestOnLinux()
        {
            if (!OperatingSystem.IsLinux())
                Assert.Ignore("Skiped in Release!");

            await doTests();
        }

        [Test]
        [Timeout(10000)]
        public async Task TestOnWindows()
        {
            if (!OperatingSystem.IsWindows())
                Assert.Ignore("Skiped in Release!");

            await doTests();
        }
        private async Task doTests()
        {
            while (controllerInstance.RemoteClients?.FirstOrDefault(rc => nodeInstance.Name.Equals(rc?.LongName))?.Ports.Count != ports)
                await Task.Delay(100);

            var nodeRD = controllerInstance.RemoteClients.FirstOrDefault(rc => nodeInstance.Name.Equals(rc?.LongName));
            Assert.That(nodeRD, Is.Not.Null);
            Assert.That(nodeRD.Ports.Count, Is.EqualTo(ports));
        }
    }
}