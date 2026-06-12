#nullable enable
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace LuksAttendance;

/// <summary>
/// ZK protocol implementation for Pollo/ZKTeco attendance devices.
/// Communicates over TCP on the configured port (default 5005 for Pollo).
/// </summary>
public class ZkDevice : IDisposable
{
    private Socket? _socket;
    private readonly string _ip;
    private readonly int _port;
    private ushort _sessionId;
    private ushort _replyId;

    // ZK Protocol commands
    private const ushort CMD_CONNECT = 1000;
    private const ushort CMD_EXIT = 1001;
    private const ushort CMD_ATTLOG_RRQ = 13;
    private const ushort CMD_USERINFO_RRQ = 9;
    private const ushort CMD_DATA_RDY = 1500;
    private const ushort CMD_DATA = 1501;
    private const ushort CMD_ACK_OK = 2000;
    private const ushort CMD_PREPARE_DATA = 1500;
    private const ushort CMD_FREE_DATA = 1502;

    // Diagnostic log
    public List<string> DiagLog { get; } = new();

    public ZkDevice(string ip, int port = 5005)
    {
        _ip = ip;
        _port = port;
    }

    public async Task<bool> ConnectAsync()
    {
        try
        {
            _socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            _socket.ReceiveTimeout = 5000;
            _socket.SendTimeout = 5000;

            DiagLog.Add($"Connecting to {_ip}:{_port} (TCP)...");
            await _socket.ConnectAsync(new IPEndPoint(IPAddress.Parse(_ip), _port));
            DiagLog.Add("TCP connected. Sending ZK CMD_CONNECT...");

            var cmd = CreatePacket(CMD_CONNECT, null);
            DiagLog.Add($"Sent {cmd.Length} bytes: {BitConverter.ToString(cmd, 0, Math.Min(cmd.Length, 24))}");
            _socket.Send(cmd);

            var reply = ReceiveReply();
            if (reply == null)
            {
                DiagLog.Add("ERROR: No reply received (timeout or empty).");
                return false;
            }

            ushort replyCmd = GetCommand(reply);
            DiagLog.Add($"Reply: {reply.Length} bytes, CMD={replyCmd}, raw={BitConverter.ToString(reply, 0, Math.Min(reply.Length, 16))}");

            if (replyCmd == CMD_ACK_OK)
            {
                _sessionId = BitConverter.ToUInt16(reply, 4);
                _replyId = BitConverter.ToUInt16(reply, 6);
                DiagLog.Add($"SUCCESS: SessionID={_sessionId}, ReplyID={_replyId}");
                return true;
            }
            else
            {
                DiagLog.Add($"FAILED: Expected CMD {CMD_ACK_OK}, got {replyCmd}");
                return false;
            }
        }
        catch (Exception ex)
        {
            DiagLog.Add($"EXCEPTION: {ex.GetType().Name}: {ex.Message}");
            return false;
        }
    }

    public List<AttendanceLog> GetAttendanceLogs()
    {
        var logs = new List<AttendanceLog>();
        if (_socket == null) return logs;

        try
        {
            var cmd = CreatePacket(CMD_ATTLOG_RRQ, null);
            _socket.Send(cmd);

            var reply = ReceiveReply();
            if (reply == null) return logs;

            ushort command = GetCommand(reply);

            if (command == CMD_PREPARE_DATA)
            {
                int dataSize = BitConverter.ToInt32(reply, 8);
                var data = ReceiveData(dataSize);
                if (data != null)
                    logs = ParseAttendanceLogs(data);

                var freeCmd = CreatePacket(CMD_FREE_DATA, null);
                _socket.Send(freeCmd);
                ReceiveReply();
            }
            else if (command == CMD_DATA)
            {
                var data = new byte[reply.Length - 8];
                Array.Copy(reply, 8, data, 0, data.Length);
                logs = ParseAttendanceLogs(data);
            }
        }
        catch { }

        return logs;
    }

    public void Disconnect()
    {
        if (_socket == null) return;
        try
        {
            var cmd = CreatePacket(CMD_EXIT, null);
            _socket.Send(cmd);
            ReceiveReply();
        }
        catch { }
        finally
        {
            _socket.Close();
            _socket = null;
        }
    }

    /// <summary>Fetch user list from device (UserID → Name mapping).</summary>
    public Dictionary<string, string> GetUsers()
    {
        var users = new Dictionary<string, string>();
        if (_socket == null) return users;

        try
        {
            var cmd = CreatePacket(CMD_USERINFO_RRQ, null);
            _socket.Send(cmd);

            var reply = ReceiveReply();
            if (reply == null) return users;

            ushort command = GetCommand(reply);
            byte[]? data = null;

            if (command == CMD_PREPARE_DATA)
            {
                int dataSize = BitConverter.ToInt32(reply, 8);
                data = ReceiveData(dataSize);
                var freeCmd = CreatePacket(CMD_FREE_DATA, null);
                _socket.Send(freeCmd);
                ReceiveReply();
            }
            else if (command == CMD_DATA)
            {
                data = new byte[reply.Length - 8];
                Array.Copy(reply, 8, data, 0, data.Length);
            }

            if (data != null)
                users = ParseUsers(data);
        }
        catch { }
        return users;
    }

    private static Dictionary<string, string> ParseUsers(byte[] data)
    {
        var users = new Dictionary<string, string>();
        int recordSize = 72;
        if (data.Length < recordSize)
        {
            recordSize = 28;
            if (data.Length < recordSize) return users;
        }

        for (int i = 0; i < data.Length - recordSize + 1; i += recordSize)
        {
            try
            {
                string uid;
                string name;

                if (recordSize == 72)
                {
                    uid = Encoding.ASCII.GetString(data, i + 2, 9).TrimEnd('\0').Trim();
                    name = Encoding.UTF8.GetString(data, i + 11, 24).TrimEnd('\0').Trim();
                }
                else
                {
                    uid = BitConverter.ToUInt16(data, i).ToString();
                    name = Encoding.UTF8.GetString(data, i + 2, 20).TrimEnd('\0').Trim();
                }

                if (!string.IsNullOrEmpty(uid) && !string.IsNullOrEmpty(name))
                    users[uid] = name;
            }
            catch { }
        }
        return users;
    }

    public void Dispose() => Disconnect();

    private byte[] CreatePacket(ushort command, byte[]? data)
    {
        int dataLen = data?.Length ?? 0;
        var packet = new byte[8 + dataLen];
        BitConverter.GetBytes(command).CopyTo(packet, 0);
        BitConverter.GetBytes((ushort)0).CopyTo(packet, 2); // checksum placeholder
        BitConverter.GetBytes(_sessionId).CopyTo(packet, 4);
        BitConverter.GetBytes(_replyId).CopyTo(packet, 6);
        if (data != null) data.CopyTo(packet, 8);

        ushort checksum = CalcChecksum(packet);
        BitConverter.GetBytes(checksum).CopyTo(packet, 2);
        _replyId++;

        // Wrap with header
        var full = new byte[packet.Length + 8];
        full[0] = 0x50; full[1] = 0x50; full[2] = 0x82; full[3] = 0x7d;
        BitConverter.GetBytes((uint)packet.Length).CopyTo(full, 4);
        packet.CopyTo(full, 8);
        return full;
    }

    private byte[]? ReceiveReply()
    {
        try
        {
            var header = new byte[8];
            int received = _socket!.Receive(header);
            if (received < 8) return null;

            uint dataLen = BitConverter.ToUInt32(header, 4);
            if (dataLen == 0 || dataLen > 65535) return null;

            var data = new byte[dataLen];
            int total = 0;
            while (total < dataLen)
            {
                int r = _socket.Receive(data, total, (int)dataLen - total, SocketFlags.None);
                if (r == 0) break;
                total += r;
            }
            return data;
        }
        catch { return null; }
    }

    private byte[]? ReceiveData(int size)
    {
        try
        {
            var buffer = new byte[size];
            int total = 0;
            while (total < size)
            {
                var header = new byte[8];
                int hr = _socket!.Receive(header);
                if (hr < 8) break;
                uint chunkLen = BitConverter.ToUInt32(header, 4);

                var chunk = new byte[chunkLen];
                int chunkTotal = 0;
                while (chunkTotal < chunkLen)
                {
                    int r = _socket.Receive(chunk, chunkTotal, (int)chunkLen - chunkTotal, SocketFlags.None);
                    if (r == 0) break;
                    chunkTotal += r;
                }

                int payloadStart = 8;
                int payloadLen = chunkTotal - payloadStart;
                if (payloadLen > 0 && total + payloadLen <= size)
                {
                    Array.Copy(chunk, payloadStart, buffer, total, payloadLen);
                    total += payloadLen;
                }
                else
                {
                    int copyLen = Math.Min(chunkTotal, size - total);
                    Array.Copy(chunk, 0, buffer, total, copyLen);
                    total += copyLen;
                }
            }
            return buffer;
        }
        catch { return null; }
    }

    private static List<AttendanceLog> ParseAttendanceLogs(byte[] data)
    {
        var logs = new List<AttendanceLog>();
        int recordSize = data.Length >= 40 ? 40 : 16;
        if (data.Length < recordSize) return logs;

        for (int i = 0; i < data.Length - recordSize + 1; i += recordSize)
        {
            try
            {
                string userId;
                DateTime timestamp;

                if (recordSize == 40)
                {
                    userId = Encoding.ASCII.GetString(data, i, 24).TrimEnd('\0').Trim();
                    uint ts = BitConverter.ToUInt32(data, i + 24);
                    timestamp = DecodeTimestamp(ts);
                }
                else
                {
                    userId = BitConverter.ToUInt16(data, i).ToString();
                    uint ts = BitConverter.ToUInt32(data, i + 4);
                    timestamp = DecodeTimestamp(ts);
                }

                if (!string.IsNullOrEmpty(userId) && timestamp.Year > 2000)
                    logs.Add(new AttendanceLog { UserId = userId, Timestamp = timestamp });
            }
            catch { }
        }
        return logs;
    }

    private static DateTime DecodeTimestamp(uint t)
    {
        int second = (int)(t % 60); t /= 60;
        int minute = (int)(t % 60); t /= 60;
        int hour = (int)(t % 24); t /= 24;
        int day = (int)(t % 31) + 1; t /= 31;
        int month = (int)(t % 12) + 1; t /= 12;
        int year = (int)t + 2000;
        try { return new DateTime(year, month, day, hour, minute, second); }
        catch { return DateTime.MinValue; }
    }

    private static ushort CalcChecksum(byte[] data)
    {
        uint sum = 0;
        for (int i = 0; i < data.Length - 1; i += 2)
            sum += BitConverter.ToUInt16(data, i);
        if (data.Length % 2 != 0)
            sum += data[^1];
        sum = (sum >> 16) + (sum & 0xFFFF);
        sum = ~sum & 0xFFFF;
        return (ushort)sum;
    }

    private static ushort GetCommand(byte[] reply) => BitConverter.ToUInt16(reply, 0);
}

public class AttendanceLog
{
    public string UserId { get; set; } = "";
    public string UserName { get; set; } = "";
    public DateTime Timestamp { get; set; }
}
