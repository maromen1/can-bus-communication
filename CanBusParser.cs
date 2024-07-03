using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

public class CanBusParser
{
    private const uint CAN_ID_DONGLE = 0x7E0;
    private const uint CAN_ID_ENGINE = 0x7E8;
    private const byte SERVICE_36 = 0x36;

    public static byte[] ProcessCanBusLog(byte[] fileData)
    {
        var messages = ParseCanMessages(fileData);
        var transferData = ExtractTransferData(messages);
        return transferData;
    }

    private static List<CanMessage> ParseCanMessages(byte[] fileData)
    {
        var messages = new List<CanMessage>();
        for (int i = 0; i < fileData.Length; i += 17)
        {
            var timestamp = BitConverter.ToUInt32(fileData, i);
            var canId = BitConverter.ToUInt32(fileData, i + 4);
            var data = new byte[9];
            Array.Copy(fileData, i + 8, data, 0, 9);
            messages.Add(new CanMessage { Timestamp = timestamp, CanId = canId, Data = data });
        }
        return messages;
    }

    private static byte[] ExtractTransferData(List<CanMessage> messages)
    {
        var transferData = new List<byte>();
        var currentPacket = new List<byte>();
        int expectedPacketCount = 0;
        int currentPacketNumber = 0;

        foreach (var message in messages)
        {
            if (message.CanId != CAN_ID_DONGLE) continue;

            var firstByte = message.Data[0];
            if (firstByte == 0x10)
            {
                // Start of multi-packet message
                var size = (message.Data[1] << 8) | message.Data[2];
                expectedPacketCount = (int)Math.Ceiling((double)(size - 6) / 7) + 1;
                currentPacketNumber = 1;
                currentPacket = new List<byte>(message.Data.Skip(4));
            }
            else if (firstByte >= 0x21 && firstByte <= 0x2F)
            {
                // Continuation of multi-packet message
                currentPacketNumber++;
                currentPacket.AddRange(message.Data.Skip(1));

                if (currentPacketNumber == expectedPacketCount)
                {
                    if (currentPacket[0] == SERVICE_36)
                    {
                        transferData.AddRange(currentPacket.Skip(2));
                    }
                    currentPacket.Clear();
                    expectedPacketCount = 0;
                    currentPacketNumber = 0;
                }
            }
        }

        return transferData.ToArray();
    }

    private class CanMessage
    {
        public uint Timestamp { get; set; }
        public uint CanId { get; set; }
        public byte[] Data { get; set; }
    }
}