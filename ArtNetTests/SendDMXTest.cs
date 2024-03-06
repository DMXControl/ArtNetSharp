using ArtNetSharp;
using ArtNetSharp.Communication;
using ArtNetTests.Mocks;
using ArtNetTests.Mocks.Instances;
using NUnit.Framework.Constraints;
using RDMSharp;
using RDMSharp.ParameterWrapper;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using System.Net;

namespace ArtNetTests
{
    public class SendDMXTest
    {
        ArtNet artNet;
        [SetUp]
        public void Setup()
        {
            artNet = ArtNet.Instance;
        }

        [Test]
        public async Task TestNodeInstance()
        {
            NodeInstance nodeInstance = new NodeMock();
            nodeInstance.Name = "Test Node";
            ControllerInstance controllerInstance = new ControllerInstanceMock();
            controllerInstance.Name = "Test Controller";
            byte ports = 255;
            for (ushort i = 1; i <= ports; i++)
            {
                nodeInstance.AddPortConfig(new PortConfig((byte)i, i, true, false) { PortNumber = (byte)i, Type = EPortType.OutputFromArtNet, GoodOutput = EGoodOutput.ContiniuousOutput | EGoodOutput.DataTransmitted });
                controllerInstance.AddPortConfig(new PortConfig((byte)i, i, false, true) { PortNumber = (byte)i, Type = EPortType.InputToArtNet | EPortType.ArtNet });
            }
            artNet.AddInstance(nodeInstance);
            artNet.AddInstance(controllerInstance);
            RemoteClient nodeRD = null;
            for (int i = 0; i < 80; i++)
            {
                nodeRD = controllerInstance.RemoteClients?.FirstOrDefault(rc => nodeInstance.Name.Equals(rc?.LongName));
                if (nodeRD != null)
                    break;
                await Task.Delay(100);
            }
            Assert.That(nodeRD, Is.Not.Null);
            Assert.That(nodeRD.Ports.Count, Is.EqualTo(ports));

            await SendReceiveDMX(controllerInstance, nodeInstance);

            await Task.Delay(100);
            artNet.RemoveInstance(nodeInstance);
            artNet.RemoveInstance(controllerInstance);
            ((IDisposable)nodeInstance).Dispose();
            ((IDisposable)controllerInstance).Dispose();
        }

        private async Task SendReceiveDMX(ControllerInstance controllerInstance, NodeInstance nodeInstance)
        {
            int count = 0;
            ConcurrentQueue<byte[]> predefinedData=new ConcurrentQueue<byte[]>();
            for (byte b = 0; b < byte.MaxValue; b++)
            {
                byte[] data = new byte[512];
                for (short k = 0; k < 512; k++)
                    data[k] = b;
                predefinedData.Enqueue(data);
            }
            Dictionary<PortAddress, List<TimeSpan>> timeCacheDict = new Dictionary<PortAddress, List<TimeSpan>>();
            Dictionary<PortAddress, Stopwatch> swDict = new Dictionary<PortAddress, Stopwatch>();
            nodeInstance.DMXReceived += (o, e) =>
            {
                if (!timeCacheDict.ContainsKey(e))
                    timeCacheDict[e] = new List<TimeSpan>();
                if (!swDict.ContainsKey(e))
                    swDict[e] = new Stopwatch();

                var timeCache = timeCacheDict[e];
                var sw = swDict[e];
                if (sw.IsRunning)
                    timeCache.Insert(0, sw.Elapsed);
                sw.Restart();
                while (timeCache.Count > 100)
                    timeCache.Remove(timeCache.Last());
                if (timeCache.Count > 90)
                {
                    double refreshRate;
                    refreshRate = timeCache.Take(50).Average(t => ((double)TimeSpan.TicksPerSecond) / t.Ticks);

                    Assert.That(refreshRate, Is.InRange(40, 55), $"PortAddress: {e} is receiving Values at strange RefreshRate: {refreshRate}");
                }
            };
            while (predefinedData.TryDequeue(out byte[] data))
            {
                await Task.Delay(10);
                predefinedData.Enqueue(data);//endless loop
                for (ushort i = 0; i < 32; i++)
                    controllerInstance.WriteDMXValues(i, data);
                count++;
                if (count == 1000)
                {
                    break;
                }
            }
            Assert.That(timeCacheDict.First().Value.Count, Is.GreaterThanOrEqualTo(90));
        }
    }
}