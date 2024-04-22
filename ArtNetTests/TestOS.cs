using ArtNetSharp;
using ArtNetSharp.Communication;
using ArtNetTests.Mocks;
using ArtNetTests.Mocks.Instances;
using NUnit.Framework.Internal;
using System.Diagnostics;

namespace ArtNetTests
{
    public class TestOS
    {
        NodeInstance nodeInstance;
        ControllerInstance controllerInstance;
        byte ports = 2;
        ArtNet artNet;
        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            artNet = ArtNet.Instance;
        }
        [SetUp]
        public void SetUp()
        {
            nodeInstance = new NodeMock();
            nodeInstance.Name = "Test Node";
            controllerInstance = new ControllerInstanceMock(0x42);
            controllerInstance.Name = "Test Controller";
            for (ushort i = 1; i <= ports; i++)
            {
                nodeInstance.AddPortConfig(new PortConfig((byte)i, i, true, false) { PortNumber = (byte)i, Type = EPortType.OutputFromArtNet, GoodOutput = EGoodOutput.ContiniuousOutput | EGoodOutput.DataTransmitted });
                controllerInstance.AddPortConfig(new PortConfig((byte)i, i, false, true) { PortNumber = (byte)i, Type = EPortType.InputToArtNet | EPortType.ArtNet });
            }
        }



        [TearDown]
        public void Teardown()
        {
            artNet.RemoveInstance(nodeInstance);
            artNet.RemoveInstance(controllerInstance);
            ((IDisposable)nodeInstance).Dispose();
            ((IDisposable)controllerInstance).Dispose();
        }
        [OneTimeTearDown]
        public void OneTimeTearDown()
        {
            Trace.Flush();
        }

        [Test]
#pragma warning disable CS0618 // Typ oder Element ist veraltet
        [Timeout(10000)]
#pragma warning restore CS0618 // Typ oder Element ist veraltet
        public async Task TestOnLinux()
        {
            if (!OperatingSystem.IsLinux())
                Assert.Ignore("Skiped, only run on Linux");

            await doTests();
        }

        [Test]
#pragma warning disable CS0618 // Typ oder Element ist veraltet
        [Timeout(10000)]
#pragma warning restore CS0618 // Typ oder Element ist veraltet
        public async Task TestOnWindows()
        {
            if (!OperatingSystem.IsWindows())
                Assert.Ignore("Skiped, only run on Windows");

            await doTests();
        }

        [Test]
#pragma warning disable CS0618 // Typ oder Element ist veraltet
        [Timeout(10000)]
#pragma warning restore CS0618 // Typ oder Element ist veraltet
        public async Task TestOnMackOS()
        {
            if (!OperatingSystem.IsMacOS())
                Assert.Ignore("Skiped, only run on Mac OS");

            await doTests();
        }
        private async Task doTests()
        {

            artNet.AddInstance(nodeInstance);
            artNet.AddInstance(controllerInstance);

            while (controllerInstance.RemoteClients?.FirstOrDefault(rc => nodeInstance.Name.Equals(rc?.LongName))?.Ports.Count != ports)
                await Task.Delay(100);

            var nodeRD = controllerInstance.RemoteClients.FirstOrDefault(rc => nodeInstance.Name.Equals(rc?.LongName));
            Assert.That(nodeRD, Is.Not.Null);
            Assert.That(nodeRD.Ports.Count, Is.EqualTo(ports));

            artNet.RemoveInstance(nodeInstance);
            artNet.RemoveInstance(controllerInstance);
            ((IDisposable)nodeInstance).Dispose();
            ((IDisposable)controllerInstance).Dispose();
        }
    }
}