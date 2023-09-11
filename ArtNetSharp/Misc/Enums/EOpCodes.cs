using System;

namespace ArtNetSharp
{
    public enum EOpCodes : ushort
    {
        /// <summary>
        /// This is an ArtPoll packet, no other data is contained
        /// in this UDP packet.
        /// </summary>
        OpPoll = 0x2000,
        /// <summary>
        /// This is an ArtPollReply Packet. It contains device
        /// status information.
        /// </summary>
        OpPollReply = 0x2100,
        /// <summary>
        /// Diagnostics and data logging packet.
        /// </summary>
        OpDiagData = 0x2300,
        /// <summary>
        /// Used to send text based parameter commands.
        /// </summary>
        OpCommand = 0x2400,
        /// <summary>
        /// This is an ArtDataRequest packet. It is used to request data such as products URLs
        /// </summary>
        OpDataRequest = 0x2700,
        /// <summary>
        /// This is an ArtDmx data packet. It contains zero start
        /// code DMX512 information for a single Universe.
        /// </summary>
        OpDataReply = 0x2800,
        /// <value>
        /// OpOutput / OpDmx
        /// </value>
        /// <summary>
        /// This is an ArtDataReply packet. It is used to reply to ArtDataRequest packets.
        /// </summary>
        OpOutput = 0x5000,
        /// <summary>
        /// This is an ArtNzs data packet. It contains non-zero
        /// start code (except RDM) DMX512 information for a
        /// single Universe.
        /// </summary>
        OpNzs = 0x5100,
        /// <summary>
        /// This is an ArtSync data packet. It is used to force
        /// synchronous transfer of ArtDmx packets to a node’s
        /// output.
        /// </summary>
        OpSync = 0x5200,
        /// <summary>
        /// This is an ArtAddress packet. It contains remote
        /// programming information for a Node.
        /// </summary>
        OpAddress = 0x6000,
        /// <summary>
        /// This is an ArtInput packet. It contains enable –
        /// disable data for DMX inputs.
        /// </summary>
        OpInput = 0x7000,
        /// <summary>
        /// This is an ArtTodRequest packet. It is used to request        /// a Table of Devices (ToD) for RDM discovery.
        /// </summary>
        OpTodRequest = 0x8000,
        /// <summary>
        /// This is an ArtTodData packet. It is used to send a
        /// Table of Devices (ToD) for RDM discovery.
        /// </summary>
        OpTodData = 0x8100,
        /// <summary>
        /// This is an ArtTodControl packet. It is used to send        /// RDM discovery control messages.
        /// </summary>
        OpTodControl = 0x8200,
        /// <summary>
        /// This is an ArtRdm packet. It is used to send all non
        /// discovery RDM messages.
        /// </summary>
        OpRdm = 0x8300,
        /// <summary>
        /// This is an ArtRdmSub packet. It is used to send
        /// compressed, RDM Sub-Device data.
        /// </summary>
        OpRdmSub = 0x8400,
        /// <summary>
        /// This is an ArtVideoSetup packet. It contains video
        /// screen setup information for nodes that implement
        /// the extended video features.
        /// </summary>
        OpVideoSetup = 0xa010,
        /// <summary>
        /// This is an ArtVideoPalette packet. It contains color
        /// palette setup information for nodes that implement
        /// the extended video features.
        /// </summary>
        OpVideoPalette = 0xa020,
        /// <summary>
        /// This is an ArtVideoData packet. It contains display
        /// data for nodes that implement the extended video
        /// features.
        /// </summary>
        OpVideoData = 0xa040,
        /// <summary>
        /// This packet is deprecated.
        /// </summary>
        [Obsolete]
        OpMacMaster = 0xf000,
        /// <summary>
        /// This packet is deprecated.
        /// </summary>
        [Obsolete]
        OpMacSlave = 0xf100,
        /// <summary>
        /// This is an ArtFirmwareMaster packet. It is used to
        /// upload new firmware or firmware extensions to the
        /// Node.
        /// </summary>
        OpFirmwareMaster = 0xf200,
        /// <summary>
        /// Thisis an ArtFirmwareReply packet. It is returned by
        /// the node to acknowledge receipt of an
        /// ArtFirmwareMaster packet or ArtFileTnMaster
        /// packet.
        /// </summary>
        OpFirmwareReply = 0xf300,
        /// <summary>
        /// Uploads user file to node.
        /// </summary>
        OpFileTnMaster = 0xf400,
        /// <summary>
        /// Downloads user file from node.
        /// </summary>
        OpFileFnMaster = 0xf500,
        /// <summary>
        /// Server to Nodeacknowledge for downloadpackets.
        /// </summary>
        OpFileFnReply = 0xf600,
        /// <summary>
        /// This is an ArtIpProg packet. It is used to reprogramme the IP addressand Mask of the Node.
        /// </summary>
        OpIpProg = 0xf800,
        /// <summary>
        /// This is an ArtIpProgReply packet. It is returned by the
        /// node to acknowledge receipt of an ArtIpProg packet.
        /// </summary>
        OpIpProgReply = 0xf900,
        /// <summary>
        /// This is an ArtMedia packet. It is Unicast by a Media
        /// Server and acted upon by a Controller.
        /// </summary>
        OpMedia = 0x9000,
        /// <summary>
        /// This is an ArtMediaPatch packet. It is Unicast by a
        /// Controller and acted uponby a Media Server.
        /// </summary>
        OpMediaPatch = 0x9100,
        /// <summary>
        /// This is an ArtMediaControl packet. It is Unicast by a
        /// Controller and acted upon by a Media Server.
        /// </summary>
        OpMediaControl = 0x9200,
        /// <summary>
        /// This is an ArtMediaControlReply packet. It is Unicast
        /// by a Media Server and acted upon by a Controller.
        /// </summary>
        OpMediaContrlReply = 0x9300,
        /// <summary>
        /// This is an ArtTimeCode packet. It is used to transport
        /// time code overthe network.
        /// </summary>
        OpTimeCode = 0x9700,
        /// <summary>
        /// Used to synchronise real time date and clock.
        /// </summary>
        OpTimeSync = 0x9800,
        /// <summary>
        /// Used to send trigger macros.
        /// </summary>
        OpTrigger = 0x9900,
        /// <summary>
        /// Requests a node's file list.
        /// </summary>
        OpDirectory = 0x9a00,
        /// <summary>
        /// Replies to OpDirectory with file list.
        /// </summary>
        OpDirectoryReply = 0x9b00,
    }
}