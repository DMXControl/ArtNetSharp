using ArtNetSharp;
using RDMSharp;

namespace ArtNetTests
{
    public class ObjectTypesTests
    {
        [SetUp]
        public void Setup()
        {
        }
        [Test]
        public void TestUniverse()
        {
            for (byte b = 0; b < byte.MaxValue; b++)
            {
                try
                {
                    Universe u = new Universe(b);

                    Assert.That(b, Is.LessThanOrEqualTo(0xf));
                    Assert.That(u.Value, Is.EqualTo(b));
                }
                catch
                {
                    Assert.That(b, Is.GreaterThan(0xf));
                }
            }
        }

        [Test]
        public void TestSubnet()
        {
            for (byte b = 0; b < byte.MaxValue; b++)
            {
                try
                {
                    Subnet s = new Subnet(b);

                    Assert.That(b, Is.LessThanOrEqualTo(0xf));
                    Assert.That(s.Value, Is.EqualTo(b));
                }
                catch
                {
                    Assert.That(b, Is.GreaterThan(0xf));
                }
            }
        }

        [Test]
        public void TestAddress()
        {
            Address a = new Address(1);
            Assert.That(a.Universe.Value, Is.EqualTo(1));
            Assert.That(a.Combined, Is.EqualTo(1));

            a = new Address(15);
            Assert.That(a.Universe.Value, Is.EqualTo(15));
            Assert.That(a.Combined, Is.EqualTo(15));

            a = new Address(16);
            Assert.That(a.Universe.Value, Is.EqualTo(0));
            Assert.That(a.Subnet.Value, Is.EqualTo(1));
            Assert.That(a.Combined, Is.EqualTo(16));

            a = new Address(0xff);
            Assert.That(a.Universe.Value, Is.EqualTo(15));
            Assert.That(a.Subnet.Value, Is.EqualTo(15));
            Assert.That(a.Combined, Is.EqualTo(0xff));
        }

        [Test]
        public void TestNet()
        {
            for (byte b = 0; b < byte.MaxValue; b++)
            {
                try
                {
                    Net n = new Net(b);

                    Assert.That(b, Is.LessThanOrEqualTo(0x7f));
                    Assert.That(n.Value, Is.EqualTo(b));
                }
                catch
                {
                    Assert.That(b, Is.GreaterThan(0xf));
                }
            }
        }
        [Test]
        public void TestNodeReport()
        {
            NodeReport src = new NodeReport(ENodeReportCodes.RcFirmwareFail, "FAILED", 33);
            NodeReport dest = new NodeReport(src.ToString());
            Assert.That(dest, Is.EqualTo(src));
        }
        [Test]
        public void TestIPv4Address()
        {
            IPv4Address src = new IPv4Address("192.168.178.158");
            IPv4Address dest = new IPv4Address(src.ToString());
            Assert.That(dest, Is.EqualTo(src));
        }
        [Test]
        public void TestMACAddress()
        {
            MACAddress src = new MACAddress("00:11:ee:44:2f:27");
            MACAddress dest = new MACAddress((byte[])src);
            Assert.That(dest, Is.EqualTo(src));
        }
    }
}