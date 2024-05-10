using ArtNetSharp;
using RDMSharp;
using System.Net;
using System.Reflection;

namespace ArtNetTests.Binary_Tests
{
    public abstract class AbstractArtPollReplyBinaryTestSubject
    {
        public static readonly object[] TestSubjects = getTestSubjects();
        private static object[] getTestSubjects()
        {
            Type abstractType = typeof(AbstractArtPollReplyBinaryTestSubject);

            // Get all types in the current assembly that inherit from the abstract class
            IEnumerable<Type> concreteTypes = Assembly.GetExecutingAssembly().GetTypes()
                .Where(t => t.IsClass && !t.IsAbstract && t.GetConstructors().Any(c => c.IsPublic && c.GetParameters().Length == 0) && abstractType.IsAssignableFrom(t));

            // Create instances of each concrete class
            List<AbstractArtPollReplyBinaryTestSubject> instances = new List<AbstractArtPollReplyBinaryTestSubject>();
            foreach (Type concreteType in concreteTypes)
            {
                if (Activator.CreateInstance(concreteType) is AbstractArtPollReplyBinaryTestSubject instance)
                    instances.Add(instance);
            }

            return instances.ToArray();
        }

        public override string ToString() => TestLabel;

        public readonly byte[] Data;

        public readonly string TestLabel;

        public readonly Net Net;
        public readonly string ShortName;
        public readonly string LongName;
        public readonly MACAddress MACAddress;
        public readonly IPv4Address IP;
        public readonly IPv4Address BindIP;
        public readonly ushort OemCode;
        public readonly ushort ManufacturerCode;
        public readonly EStCodes StyleCode;
        public readonly PortTestSubject[] Ports;

        public readonly byte? MajorVersion;
        public readonly byte? MinorVersion;
        public readonly NodeReport? NodeReport;

        public readonly bool DataLengthIsEqual;

        public AbstractArtPollReplyBinaryTestSubject(
            in string testLabel,
            in byte[] data,
            in Net net,
            in string shortName,
            in string longName,
            in MACAddress mACAddress,
            in IPv4Address iP,
            in IPv4Address bindIP,
            in ushort oemCode,
            in ushort manufacturerCode,
            in EStCodes styleCode,
            in PortTestSubject[] ports,
            in bool dataLengthIsEqual,
            in byte? majorVersion = null,
            in byte? minorVersion = null,
            in NodeReport? nodeReport = null)
        {
            TestLabel = testLabel;
            Data = data;
            Net = net;
            ShortName = shortName;
            LongName = longName;
            MACAddress = mACAddress;
            IP = iP;
            BindIP = bindIP;
            OemCode = oemCode;
            ManufacturerCode = manufacturerCode;
            StyleCode = styleCode;
            Ports = ports;
            MajorVersion = majorVersion;
            MinorVersion = minorVersion;
            NodeReport = nodeReport;
            DataLengthIsEqual = dataLengthIsEqual;
        }

        public readonly struct PortTestSubject
        {
            public readonly EPortType PortType;
            public readonly object OutputUniverse;
            public readonly object InputUniverse;

            public PortTestSubject(EPortType portType, Universe outputUniverse, Universe inputUniverse)
            {
                PortType = portType;
                OutputUniverse = outputUniverse;
                InputUniverse = inputUniverse;
            }
            public PortTestSubject(EPortType portType, Address outputUniverse, Address inputUniverse)
            {
                PortType = portType;
                OutputUniverse = outputUniverse;
                InputUniverse = inputUniverse;
            }
        }
    }
}
 