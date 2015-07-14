// Copyright Piotr Trojanowski 2015

// This program is free software; you can redistribute it and/or modify
// it under the terms of the GNU Lesser General Public License as published by
// the Free Software Foundation; either version 2.1 of the License, or
// (at your option) any later version.

// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU Lesser General Public License for more details.

// You should have received a copy of the GNU Lesser General Public License
// along with this program; if not, write to the Free Software
// Foundation, Inc., 51 Franklin St, Fifth Floor, Boston, MA  02110-1301  USA

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Security.Cryptography;

namespace WinRVClient
{
    public class RVClient
    {
        // TODO remove
        public static string CertificateCommonName = "Internet Widgits Pty Ltd"; // just for testing

        public struct StreamDataV1
        {
            public string streamName;
            public int port;
            public string proxiedName;
        }

        protected static class RVProtocolV1PacketHandler
        {
            public const int PacketSizeConst = 516; //int32 + byte[512]
            public const int ChallengeWordSize = 32;

            private static Dictionary<DataKey, string> DataKeyStrings = new Dictionary<DataKey, string>() { //const
                {DataKey.Separator, ":"},
                {DataKey.User, "user"},
                {DataKey.Pass, "pass"},
                {DataKey.Stream, "stream"},
                {DataKey.Port, "port"},
                {DataKey.ProxiedName, "proxied_name"},
            };

            private enum MessageCode : int //this must be Int32
            {
                Invalid = 0,
                Hello = 100,
                Welcome = 101,
                ChallengePlease = 200,
                ChallengeIs = 201,
                ChallengeResponse = 210,
                ChallengePass = 211,
                ChallengeFail = 212,
                Bye = 300
            }

            private enum DataKey
            {
                Separator,
                User,
                Pass,
                Stream,
                Port,
                ProxiedName,
            }

            /// <summary>
            /// Combine message code and data into a byte packet
            /// </summary>
            /// <param name="messageCode"></param>
            /// <param name="data">Only ASCII characters string.</param>
            /// <returns></returns>
            /// <remarks>String passed as parameter is converted to ASCII so it should not contain any non-ascii characters.</remarks>
            private static byte[] Combine(MessageCode messageCode, string data)
            {
                byte[] packet = Combine(messageCode);
                System.Text.Encoding.ASCII.GetBytes(data).CopyTo(packet, 4);

                return packet;
            }

            /// <summary>
            /// Combine message code and data into a byte packet
            /// </summary>
            /// <param name="messageCode"></param>
            /// <param name="data"></param>
            /// <returns></returns>
            private static byte[] Combine(MessageCode messageCode, byte[] data)
            {
                byte[] packet = Combine(messageCode);
                data.CopyTo(packet, 4);

                return packet;
            }

            /// <summary>
            /// Extracts message code from the packet
            /// </summary>
            /// <param name="packet"></param>
            /// <returns></returns>
            private static MessageCode ExtractMessageCode(byte[] packet)
            {
                int code = BitConverter.ToInt32(packet, 0);
                return (MessageCode)code;
            }

            /// <summary>
            /// Combine message code into a byte packet
            /// </summary>
            /// <param name="messageCode"></param>
            /// <returns></returns>
            private static byte[] Combine(MessageCode messageCode)
            {
                byte[] packet = new byte[PacketSizeConst];
                BitConverter.GetBytes((Int32)messageCode).CopyTo(packet, 0);

                return packet;
            }

            /// <summary>
            /// Print packet in HEX values starting from offset and ending after
            /// length bytes
            /// </summary>
            /// <param name="packet"></param>
            /// <param name="offset"></param>
            /// <param name="length"></param>
            public static void DebugPacket(byte[] packet, int offset, int length, string info)
            {
                Console.Write("PACKET DEBUG [" + info + "]\n");
                Console.Write("PACKET LENGTH [" + packet.Length + "]"); 

                Console.BackgroundColor = ConsoleColor.Red;
                Console.ForegroundColor = ConsoleColor.Black;

                for(int i = offset; i < offset + length && i <packet.Length; i++)
                {
                    if (i % 4 == 0)
                    {
                        Console.Write("\n");
                    }

                    Console.Write(packet[i].ToString("X2") + " ");
                }

                Console.ResetColor();
                Console.Write("\n");
            }

            /// <summary>
            /// Generate HELLO packet
            /// </summary>
            /// <returns>Array of bytes ready to send via network</returns>
            public static byte[] GetHello()
            {
                return Combine(MessageCode.Hello);
            }

            /// <summary>
            /// Generate BYE packet
            /// </summary>
            /// <returns>Array of bytes ready to send via network</returns>
            public static byte[] GetBye()
            {
                return Combine(MessageCode.Bye);
            }

            /// <summary>
            /// Generate CHALLENGE_PLEASE packet
            /// </summary>
            /// <param name="username"></param>
            /// <param name="password"></param>
            /// <param name="streamData"></param>
            /// <returns>Array of bytes ready to send via network</returns>
            /// <remarks>As the maximum lenght of a packet is limited it may happen that
            /// too big streamData won't fit inside. If it happens some of the stream data
            /// may not be included in the generated packet. Hovewer the returned array
            /// of bytes is guaranteed to contain a valid RVProtocolV1 packet in that case.
            /// The excluded part of the streamData WILL NOT be put into the returned array partially 
            /// and WILL NOT invalidate the packet.
            /// 
            /// A workaround to this would be to split streamData and prepare many packets instead 
            /// of one.</remarks>
            public static byte[] GetChallengePlease(string username, StreamDataV1[] streamData)
            {
                string packetData =
                    DataKeyStrings[DataKey.User] + DataKeyStrings[DataKey.Separator] + 
                    username + DataKeyStrings[DataKey.Separator];

                foreach (StreamDataV1 s in streamData)
                {
                    string currentStreamData =
                        DataKeyStrings[DataKey.Stream] + DataKeyStrings[DataKey.Separator] + s.streamName + DataKeyStrings[DataKey.Separator] +
                        DataKeyStrings[DataKey.Port] + DataKeyStrings[DataKey.Separator] + s.port + DataKeyStrings[DataKey.Separator] +
                        DataKeyStrings[DataKey.ProxiedName] + DataKeyStrings[DataKey.Separator] + s.proxiedName + DataKeyStrings[DataKey.Separator];

                    //check if there is still enough space in the packet to add more data
                    if (currentStreamData.Length + 1 <= PacketSizeConst - packetData.Length)
                    {
                        packetData += currentStreamData;
                    }
                    else
                    {
                        break;
                    }
                }

                // remove the trailing ':' from the end of string
                return Combine(MessageCode.ChallengePlease, packetData.Substring(0, packetData.Length - 1));
            }

            /// <summary>
            /// Genereate CHALLENGE_RESPONSE packet
            /// </summary>
            /// <param name="challengeResponse">Challenge hash (this is a byte array and unlike strings 
            /// it doesn't end with '\0' suffix at the end)</param>
            /// <returns>Array of bytes ready to send via network</returns>
            public static byte[] GetChallengeResponse(byte[] challengeResponse)
            {
                return Combine(MessageCode.ChallengeResponse, challengeResponse);
            }
            /// <summary>
            /// Generate BYE packet
            /// </summary>
            /// <returns>Array of bytes ready to send via network</returns>
            public static byte[] Bye()
            {
                return Combine(MessageCode.Bye);
            }

            /// <summary>
            /// Parse the WELCOME packet
            /// </summary>
            /// <param name="packet">Array of bytes containing the packet</param>
            /// <exception cref="Exception">Thrown on unsuccesful parsing (packet is invalid)</exception>
            public static void ParseWelcome(byte[] packet)
            {
                if (ExtractMessageCode(packet) != MessageCode.Welcome)
                {
                    throw new Exception("Invalid packet type code");
                }
            }
            /// <summary>
            /// Parse the CHALLENGE_IS packet
            /// </summary>
            /// <param name="packet">Array of bytes containing the packet</param>
            /// <returns>Array of bytes containing the challenge word</returns>
            /// <exception cref="Exception">Thrown on unsuccesful parsing (packet is invalid)</exception>
            public static byte[] ParseChallengeIs(byte[] packet)
            {
                if (ExtractMessageCode(packet) == MessageCode.ChallengeIs)
                {
                    byte[] challenge = new byte[32];
                    Array.Copy(packet, 4, challenge, 0, 32);
                    return challenge;
                }
                else
                {
                    throw new Exception("Invalid packet type code");
                }
            }
            /// <summary>
            /// Parse the CHALLENGE_PASS or CHALLENGE_FAIL packet
            /// </summary>
            /// <param name="packet">Array of bytes containing the packet</param>
            /// <returns>true if packet is CHALLENGE_PASS, false otherwise</returns>
            /// <exception cref="Exception">Thrown on unsuccesful parsing (packet is invalid)</exception>
            public static bool ParseChallengePassOrFail(byte[] packet)
            {
                MessageCode code = ExtractMessageCode(packet);
                if (code == MessageCode.ChallengePass)
                {
                    return true;
                }
                else if (code == MessageCode.ChallengeFail)
                {
                    return false;
                }
                else
                {
                    throw new Exception("Invalid packet type code");
                }
            }

            /// <summary>
            /// Generate Challenge Response hash for RV Protocol V1 Challenge
            /// </summary>
            /// <param name="challengeWord">byte word given by server</param>
            /// <param name="password">user's password</param>
            /// <returns>SHA 256h hash of word + password concatenation</returns>
            public static byte[] GenerateChallengeResponseHash(byte[] challengeWord, string password)
            {
                byte[] concatenation = new byte[ChallengeWordSize + password.Length + 1];
                challengeWord.CopyTo(concatenation, 0);
                System.Text.Encoding.ASCII.GetBytes(password).CopyTo(concatenation, ChallengeWordSize);

                SHA256 algorithm = SHA256.Create();
                return algorithm.ComputeHash(concatenation);
            }
        }

        protected IPAddress serverAddress;
        protected int serverPort;

        /// <summary>
        /// Certificate Validator
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="certificate"></param>
        /// <param name="chain"></param>
        /// <param name="sslPolicyErrors"></param>
        /// <returns>Always true</returns>
        /// TODO Make real certificate validation
        protected bool RemoteCertificateValidationCallback(Object sender, X509Certificate certificate,
	        X509Chain chain, SslPolicyErrors sslPolicyErrors)
        {
            Console.WriteLine("Certificate error: {0}", sslPolicyErrors);
            return true;
        }

        /// <summary>
        /// Create RVClient object and prepare it to connect to specific address and port
        /// </summary>
        /// <param name="address">Valid host address</param>
        /// <param name="port">Valid port number</param>
        public RVClient(string address, int port)
        {
            if (!(port > 0 && port < 65536))
            {
                throw new Exception("Invalid port number.");
            }

            IPHostEntry entries = Dns.GetHostEntry(address);
            if (entries.AddressList.Length < 1)
            {
                throw new Exception("Host '" + address + "' is unreachable by DNS.");
            }

            this.serverAddress = entries.AddressList[0];
            this.serverPort = port;
        }

        /// <summary>
        /// Synchronously send passed stream data using RV Protocol Version 1
        /// </summary>
        /// <param name="username"></param>
        /// <param name="password"></param>
        /// <param name="streamData"></param>
        /// <returns>Whether data was sent and accepted by server. This will be false on
        /// connection error, invalid packet content or authorization failure. </returns>
        /// <remarks>Before calling this method Connect() and Handshake() should be called first.
        /// After data is sent connection is closed.</remarks>
        public bool sendStreamDataV1(string username, string password, StreamDataV1[] streamData)
        {
            Socket clientSocket = this.Connect();
            SslStream clientSslStream = this.Handshake(clientSocket);

            try
            {
                byte[] buffer = new byte[RVProtocolV1PacketHandler.PacketSizeConst];

                byte[] packet = RVProtocolV1PacketHandler.GetHello();
                clientSslStream.Write(packet);

                clientSslStream.Read(buffer, 0, RVProtocolV1PacketHandler.PacketSizeConst);
                RVProtocolV1PacketHandler.ParseWelcome(buffer);

                packet = RVProtocolV1PacketHandler.GetChallengePlease(username, streamData);
                clientSslStream.Write(packet);

                clientSslStream.Read(buffer, 0, RVProtocolV1PacketHandler.PacketSizeConst);
                byte[] challengeWord = RVProtocolV1PacketHandler.ParseChallengeIs(buffer);

                packet = RVProtocolV1PacketHandler.GetChallengeResponse(RVProtocolV1PacketHandler.GenerateChallengeResponseHash(challengeWord, password));
                clientSslStream.Write(packet);

                clientSslStream.Read(buffer, 0, RVProtocolV1PacketHandler.PacketSizeConst);
                bool result = RVProtocolV1PacketHandler.ParseChallengePassOrFail(buffer);

                packet = RVProtocolV1PacketHandler.GetBye();
                clientSslStream.Write(packet);

                clientSslStream.Close();
                clientSocket.Close();

                return result;
            }
            catch(Exception e)
            {
                clientSslStream.Close();
                clientSocket.Close();

                throw new Exception("Error during socket communication.", e);
            }
        }

        /// <summary>
        /// Connect to server synchronously
        /// </summary>
        /// <returns>Connected socket</returns>
        protected Socket Connect()
        {
            try
            {
                IPEndPoint endPoint = new IPEndPoint(this.serverAddress, this.serverPort);

                Socket clientSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                clientSocket.Connect(endPoint);

                // Socket is necessary to perform SSL handshake
                return clientSocket;
            }
            catch (Exception e)
            {
                throw new Exception("Could not connect to host.", e);
            }   
        }

        /// <summary>
        /// Perform SSL Handshake on a connected socket
        /// </summary>
        /// <param name="clientSocket">Socket object returned by Connect()</param>
        /// <returns>Stream which may be used for secure communication</returns>
        /// <remarks>Ceritifacte validation depends on RemoteCertificateValidationCallback() method</remarks>
        protected SslStream Handshake(Socket clientSocket)
        {
            NetworkStream clientSocketStream = null;
            SslStream clientSslStream = null;

            try
            {
                clientSocketStream = new NetworkStream(clientSocket); // this stream is going to be controlled by SslStream
                clientSslStream = new SslStream(clientSocketStream, false, this.RemoteCertificateValidationCallback);

                clientSslStream.AuthenticateAsClient(CertificateCommonName);

                // SslStream is necessary to read/write data
                return clientSslStream;
            }
            catch (Exception e)
            {
                if (clientSocketStream != null)
                {
                    clientSocketStream.Close();
                }
                if (clientSslStream != null)
                {
                    clientSslStream.Close();
                }
                clientSocket.Close();

                throw new Exception("Could not perform handshake process.", e);
            }
        }

    }
}
