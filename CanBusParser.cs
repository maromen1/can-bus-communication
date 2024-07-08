using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;

public class CanBusParser
{
    private const uint CAN_ID_DONGLE = 0x7E0;
    private const byte SERVICE_36 = 0x36;

    private static readonly ILogger _logger = LoggerFactory.Create(builder => builder.AddConsole()).CreateLogger<CanBusParser>();

    public static byte[] ProcessCanBusLog(byte[] fileData)
{
    _logger.LogInformation($"Processing file data of length: {fileData.Length} bytes");

    var messages = ParseCanMessages(fileData);
    _logger.LogInformation($"Parsed {messages.Count} CAN messages");

    var transferData = ExtractTransferData(messages);
    _logger.LogInformation($"Extracted transfer data of length: {transferData.Length} bytes");

    if (transferData.Length == 0)
    {
        _logger.LogWarning("No transfer data was extracted. This might indicate an issue with the input file or the extraction process.");
    }
    else
    {
        _logger.LogInformation($"First few bytes of transfer data: {BitConverter.ToString(transferData.Take(10).ToArray())}");
    }

    return transferData;
}

    private static List<CanMessage> ParseCanMessages(byte[] fileData)
{
    var messages = new List<CanMessage>();
    _logger.LogInformation($"Parsing file data of length: {fileData.Length} bytes");

    for (int i = 0; i < fileData.Length; i += 17)
    {
        if (i + 17 > fileData.Length)
        {
            _logger.LogWarning($"Incomplete message at end of file, starting at index {i}");
            break;
        }

        var timestamp = BitConverter.ToUInt32(fileData, i);
        var canId = BitConverter.ToUInt32(fileData, i + 4);
        var data = new byte[9];
        Array.Copy(fileData, i + 8, data, 0, 9);

        messages.Add(new CanMessage { Timestamp = timestamp, CanId = canId, Data = data });

        // Log sample data for the first few messages
        if (messages.Count <= 5)
        {
            _logger.LogInformation($"Sample message {messages.Count}: Timestamp={timestamp}, CanId=0x{canId:X}, Data={BitConverter.ToString(data)}");
        }
    }

    _logger.LogInformation($"Parsed {messages.Count} CAN messages");

    // Log statistics about CAN IDs
    var canIdCounts = messages.GroupBy(m => m.CanId)
                              .Select(g => new { CanId = g.Key, Count = g.Count() })
                              .OrderByDescending(x => x.Count);

    foreach (var canIdCount in canIdCounts)
    {
        _logger.LogInformation($"CAN ID 0x{canIdCount.CanId:X}: {canIdCount.Count} messages");
    }

    return messages;
}

    private static byte[] ExtractTransferData(List<CanMessage> messages)
{
    var transferData = new List<byte>();
    var currentPacket = new List<byte>();
    int expectedPacketCount = 0;
    int currentPacketNumber = 0;
    bool isInTransferDataSection = false;

    _logger.LogInformation($"Starting to extract transfer data from {messages.Count} messages");

    bool foundService36 = false;

    foreach (var message in messages)
    {
        if (message.CanId != CAN_ID_DONGLE)
        {
            _logger.LogDebug($"Skipping message with CAN ID: 0x{message.CanId:X}");
            continue;
        }

        var firstByte = message.Data[0];
        _logger.LogDebug($"Processing message with first byte: 0x{firstByte:X}");

        if (firstByte == 0x10)
        {
            // Start of multi-packet message
            var size = (message.Data[1] << 8) | message.Data[2];
            expectedPacketCount = (int)Math.Ceiling((double)(size - 6) / 7) + 1;
            currentPacketNumber = 1;
            currentPacket.Clear();
            currentPacket.AddRange(message.Data.Skip(4));
            _logger.LogInformation($"Start of multi-packet message. Size: {size}, Expected packet count: {expectedPacketCount}");

            // Check if this is the start of Service 36 data
            if (message.Data[3] == SERVICE_36)
            {
                foundService36 = true;
                _logger.LogInformation($"Found Service 36 data starting at timestamp: {message.Timestamp}");

                isInTransferDataSection = true;
                _logger.LogInformation("Found start of Service 36 data");
            }
        }
        else if (firstByte >= 0x21 && firstByte <= 0x2F && isInTransferDataSection)
        {
            // Continuation of multi-packet message
            currentPacketNumber++;
            currentPacket.AddRange(message.Data.Skip(1));
            _logger.LogDebug($"Continuation packet {currentPacketNumber} of {expectedPacketCount}");

            if (currentPacketNumber == expectedPacketCount)
            {
                _logger.LogInformation($"Completed multi-packet message. Total bytes: {currentPacket.Count}");
                transferData.AddRange(currentPacket);
                _logger.LogInformation($"Added {currentPacket.Count} bytes to transfer data. Total transfer data size: {transferData.Count}");
                
                currentPacket.Clear();
                expectedPacketCount = 0;
                currentPacketNumber = 0;
                isInTransferDataSection = false;
            }
        }
        else
        {
            _logger.LogDebug($"Skipping message with unexpected first byte: 0x{firstByte:X}");
        }
    }

    if (!foundService36)
    {
        _logger.LogWarning("No Service 36 data found in the entire message set.");
    }

    _logger.LogInformation($"Finished extracting transfer data. Total size: {transferData.Count} bytes");
    return transferData.ToArray();
}

    private class CanMessage
    {
        public uint Timestamp { get; set; }
        public uint CanId { get; set; }
        public byte[] Data { get; set; }
    }
}