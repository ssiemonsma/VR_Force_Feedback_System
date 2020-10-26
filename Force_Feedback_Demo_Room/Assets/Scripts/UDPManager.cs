using System;
using System.Net.Sockets;
using System.Net;
using System.Text;
using System.Threading;
using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

/*
 * This script manages the UDP connection between the Raspberry Pi and the PC Unity-based demontration program.
 */

public class UDPManager
{
    private static Socket sock = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
    const float VOLTAGE_UPDATE_INTERVAL = 5.0f;
    const float CONNECTION_TIMEOUT = 1.0f;
    const float RECONNECTION_ATTEMPT_INTERVAL = 5.0f;
    const int NUM_DUPLICATE_PACKETS = 5;
    const int NUM_PACKETS_LOGGED = 100;
    const int PORT_NUMBER = 5005;
    const float NOT_ACKED = -1.0f;
    const string DEV_HOST_NAME = "ForceFeedback";
    const int VOLTAGE_MSG = 4;
    private static IPEndPoint ipPI;
    private static UdpClient udp;
    private static float lastSentTimeStamp = 0.0f;
    private static float lastReceivedTimeStamp = 0.0f;
    private static float lastVoltageTimeStamp = 0.0f;
    private static float lastVoltageTimeRequested = -15.0f;
    private static float lastVoltageReading = 0.0f;
    private static List<float> packetTimeStampLog = new List<float>();
    private static List<float> packetACKTimeLog = new List<float>();
    private static List<int> numACKsLog = new List<int>();
    private static Queue<float> ACKDelayLog = new Queue<float>();
    private static Queue<float> numACKsLogOld = new Queue<float>();
    private static float packetDropPercentage = 0;
    private static float messageFailurePercentage = 0;
    private static float avgACKDelay = 0;
    private static float lastReconnectionAttemptTime = 0;
    private static float currentTime = 0;
    private static bool isConnected = false;
    private static bool isSendingACKs = false;
    static IAsyncResult ar_ = null;
    static Thread asyncThread = null;

    public static bool CheckConnection()
    {
        return (isConnected && isSendingACKs);
    }

    public static bool GetIsConnected()
    {
        return isConnected;
    }

    private async static void GetConnectionIP()
    {
        lastReconnectionAttemptTime = currentTime;
        
        IPHostEntry hostEntry = await Dns.GetHostEntryAsync(DEV_HOST_NAME);
        if (hostEntry.AddressList.Length < 1)

        {
            throw new ArgumentException("Hostname has no assigned IP address.");
        }
        else
        {
            string address = hostEntry.AddressList[1].ToString();
            //string address = "192.168.1.43";
            ipPI = new IPEndPoint(IPAddress.Parse(address), PORT_NUMBER);
            Debug.Log(ipPI.ToString());
            isConnected = true;
        }
    }

    // start thread if there isnt one already
    public static void Start()
    {
        udp = new UdpClient(PORT_NUMBER);

        GetConnectionIP();


        if (asyncThread != null)
        {
            throw new Exception("Already started, stop first");
        }
        Debug.Log("Started listening");
        StartListening();
    }
    // close the UDP connection
    public static void Stop()
    {
        try
        {
            udp.Close();
            Debug.Log("Stopped listening");
            isConnected = false;
        }
        catch { /* don't care */ }
    }
    // Start async callback for receive
    private static void StartListening()
    {
        ar_ = udp.BeginReceive(new AsyncCallback(Receive), new object());
    }
    // Function called when new data is received
    private static void Receive(IAsyncResult ar)
    {
        IPEndPoint ip = new IPEndPoint(IPAddress.Any, PORT_NUMBER);
        byte[] packetContents = udp.EndReceive(ar, ref ip);

        parseReceivedPacket(packetContents);

        isSendingACKs = true;

        StartListening();
    }

    // Parse the received packet and update relevant variable
    private static void parseReceivedPacket(byte[] packetContents)
    {
        // Left Servo Angle
        int leftServoAngle = BitConverter.ToInt32(packetContents, 0);
        // Right Servo Angle
        int rightServoAngle = BitConverter.ToInt32(packetContents, 4);
        // Right Servo Angle
        int messageType = BitConverter.ToInt32(packetContents, 8);
        // Time Stamp
        float timeStamp = BitConverter.ToSingle(packetContents, 12);

        string receivedString = "Received: " + leftServoAngle + ", " + rightServoAngle + ", " + messageType + ", " + timeStamp;


        if (messageType == VOLTAGE_MSG && currentTime > lastVoltageTimeStamp)
        {
            float voltageReading = timeStamp;    // The float in the payload for this type of message is not actually a time stamp; it's a voltage reading
            lastVoltageReading = voltageReading;
            lastVoltageTimeStamp = currentTime;
            Debug.Log("Voltage message received");
        }
        else if (messageType <= 3)
        {
            if (timeStamp > lastReceivedTimeStamp)
            {
                lastReceivedTimeStamp = timeStamp;
            }

            int packetIndex = packetTimeStampLog.BinarySearch(timeStamp);
            if (packetIndex < 0)
            {
                Debug.Log("Stale packet received!");
            }

            else
            {
                numACKsLog[packetIndex]++;

                if (packetACKTimeLog[packetIndex] == NOT_ACKED)
                {
                    packetACKTimeLog[packetIndex] = currentTime;  // This thread runs outside of the regular Unity threads, so it cannot directly call Time.fixedTime
                }

            }

        }

    }

    // Send data to ForceFeedback device
    private static void Send(byte[] data)
    {
        for (int i = 0; i < NUM_DUPLICATE_PACKETS; i++)
        {
            sock.SendTo(data, ipPI);
        }

        string test = "Sent: " + Encoding.ASCII.GetString(data);
    }

    public static void SendFormattedPacket(int leftAngle, int rightAngle, int messageType, float timeStamp)
    {
        byte[] data = new byte[0];

        byte[] timeStampAsBytes = BitConverter.GetBytes(timeStamp);
        data = Combine(timeStampAsBytes, data);

        byte[] messageTypeAsBytes = BitConverter.GetBytes(messageType);
        data = Combine(messageTypeAsBytes, data);

        byte[] rightAngleAsBytes = BitConverter.GetBytes(rightAngle);
        data = Combine(rightAngleAsBytes, data);

        byte[] leftAngleAsBytes = BitConverter.GetBytes(leftAngle);
        data = Combine(leftAngleAsBytes, data);

        if (timeStamp > lastSentTimeStamp && messageType <= 3)
        {
            UpdatePacketStats(timeStamp);
            lastSentTimeStamp = timeStamp;
        }

        Send(data);
    }

    // This updates the packet performance statistics using data from an older batch of packets
    private static void UpdatePacketStats(float timeStamp)
    {
        lastSentTimeStamp = timeStamp;

        packetTimeStampLog.Add(timeStamp);
        packetACKTimeLog.Add(NOT_ACKED);
        numACKsLog.Add(0);

        while (packetTimeStampLog.Count > NUM_PACKETS_LOGGED)
        {
            while (ACKDelayLog.Count >= NUM_PACKETS_LOGGED)
            {
                ACKDelayLog.Dequeue();
            }
            while (numACKsLogOld.Count >= NUM_PACKETS_LOGGED)
            {
                numACKsLogOld.Dequeue();
            }

            if (packetACKTimeLog[0] != NOT_ACKED)
            {
                ACKDelayLog.Enqueue(packetACKTimeLog[0] - packetTimeStampLog[0]);
            }
            else
            {
                ACKDelayLog.Enqueue(NOT_ACKED);
            }
            //Debug.Log("packetACKTimeLog[0]:" + packetACKTimeLog[0]);
            numACKsLogOld.Enqueue(numACKsLog[0]);

            packetTimeStampLog.RemoveAt(0);
            packetACKTimeLog.RemoveAt(0);
            numACKsLog.RemoveAt(0);
        }

        float avgPacketDelayNew = 0;
        int numACKedPackets = 0;
        foreach (float ACKDelay in ACKDelayLog)
        {
            if (ACKDelay != NOT_ACKED)
            {
                avgPacketDelayNew += ACKDelay;
                numACKedPackets++;
            }
        }
        avgPacketDelayNew /= numACKedPackets;
        if (numACKedPackets != 0)
        {
            avgACKDelay = avgPacketDelayNew;
        }

        int numPacketsNotACKed = ACKDelayLog.Count - numACKedPackets;
        if (ACKDelayLog.Count != 0)
        {
            messageFailurePercentage = numPacketsNotACKed / (float)ACKDelayLog.Count * 100;
        }

        int totalACKs = 0;
        foreach (int numACKs in numACKsLogOld)
        {
            totalACKs += numACKs;
        }
        int expectedNumACKS = numACKsLogOld.Count * NUM_DUPLICATE_PACKETS;
        if (numACKsLogOld.Count != 0)
        {
            packetDropPercentage = (expectedNumACKS - totalACKs) / (float)expectedNumACKS * 100;
        }
    }

    // This is used to combine 2 byte arrays so we can format our messages
    private static byte[] Combine(byte[] first, byte[] second)
    {
        byte[] ret = new byte[first.Length + second.Length];
        Buffer.BlockCopy(first, 0, ret, 0, first.Length);
        Buffer.BlockCopy(second, 0, ret, first.Length, second.Length);
        return ret;
    }

    public static bool CheckForTimeout()
    {
        if ((Time.fixedTime - lastReceivedTimeStamp) > CONNECTION_TIMEOUT)
        {
            isSendingACKs = false;

            if ((Time.fixedTime - lastReconnectionAttemptTime) > RECONNECTION_ATTEMPT_INTERVAL)
            {
                Debug.Log("Connection timeout detected.");

                GetConnectionIP();
            }
            return !isConnected;
        }
        else
        {
            return false;
        }
    }

    public static void UpdateVoltage()
    {
        if ((currentTime - lastVoltageTimeRequested) > VOLTAGE_UPDATE_INTERVAL)
        {
            if (isConnected)
            {
                SendFormattedPacket(0, 0, 4, currentTime);
                lastVoltageTimeRequested = currentTime;

            }
        }

    }


    public static float GetLastReceivedTimeStamp()
    {
        return lastReceivedTimeStamp;
    }

    public static float GetLastVoltageReading()
    {
        return lastVoltageReading;
    }

    public static float GetPacketDropPercentage()
    {
        return packetDropPercentage;
    }

    public static float GetMessageFailurePercentage()
    {
        return messageFailurePercentage;
    }

    public static float GetAvgACKDelay()
    {
        return avgACKDelay;
    }

    public static float GetLastReconnectionAttemptTime()
    {
        return lastReconnectionAttemptTime;
    }

    public static float GetCurrentTime()
    {
        return currentTime;
    }
    public static void SetCurrentTime(float time)
    {
        currentTime = time;
    }
}
