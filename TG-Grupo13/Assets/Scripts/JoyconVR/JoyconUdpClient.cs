// JoyconUdpClient.cs
//
// Cliente UDP del servidor de Joy-Cons, equivalente al bucle de red de
// client/client_main.cpp pero pensado para Unity:
//
//  - Corre en un hilo en segundo plano: Socket.ReceiveFrom es bloqueante y
//    congelaría el hilo principal de Unity si se llamara desde Update().
//  - El hilo NO toca ninguna API de Unity (no se puede desde otro hilo); solo
//    guarda la última orientación de cada sensor bajo un lock. El
//    MonoBehaviour (JoyconReceiver) la lee una vez por frame.
//  - Re-envía "hello" cada 2 s para seguir registrado (el servidor descarta
//    clientes que no saludan en 10 s).
//  - Windows: si el hello llega antes de que el servidor esté corriendo, el
//    ICMP "port unreachable" hace que el siguiente ReceiveFrom lance
//    SocketException(ConnectionReset). Se desactiva con SIO_UDP_CONNRESET y,
//    por si acaso, también se ignora ese error.
//
// No depende de UnityEngine.

using System;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace PujJoycon
{
    public struct JoyconSensorSample
    {
        /// <summary>True si este sensor ha llegado al menos una vez.</summary>
        public bool HasData;
        public RawQuaternion Orientation;
        /// <summary>Segundos desde la última actualización de este sensor.</summary>
        public double AgeSeconds;
    }

    public struct JoyconClientStatus
    {
        public long PacketsReceived;
        public long InvalidPackets;
        /// <summary>Segundos desde el último paquete válido (double.PositiveInfinity si nunca llegó uno).</summary>
        public double SecondsSinceLastPacket;
        /// <summary>Número de sensores en el último PoseUpdate (= Joy-Cons abiertos por el servidor).</summary>
        public int SensorCount;
        /// <summary>Último error de red (null si no hay). Útil para mostrar en pantalla.</summary>
        public string LastError;
    }

    public sealed class JoyconUdpClient : IDisposable
    {
        // IOCTL de Winsock: desactiva el reporte de ICMP port-unreachable como ConnectionReset.
        private const int SioUdpConnReset = unchecked((int)0x9800000C);

        public string ServerHost { get; }
        public int ServerPort { get; }
        public TimeSpan HelloInterval { get; set; } = TimeSpan.FromSeconds(2);

        private readonly object _lock = new object();
        private readonly RawQuaternion[] _orientations = new RawQuaternion[JoyconProtocol.MaxSensors];
        private readonly long[] _lastUpdateTimestamp = new long[JoyconProtocol.MaxSensors]; // Stopwatch ticks, 0 = nunca
        private long _lastPacketTimestamp;
        private long _packetsReceived;
        private long _invalidPackets;
        private int _sensorCount;
        private string _lastError;

        private Thread _thread;
        private volatile bool _running;
        private Socket _socket;

        public JoyconUdpClient(string serverHost, int serverPort = JoyconProtocol.DefaultPort)
        {
            if (string.IsNullOrWhiteSpace(serverHost)) throw new ArgumentException("serverHost vacío", nameof(serverHost));
            if (serverPort <= 0 || serverPort > 65535) throw new ArgumentOutOfRangeException(nameof(serverPort));
            ServerHost = serverHost.Trim();
            ServerPort = serverPort;
            for (int i = 0; i < _orientations.Length; i++) _orientations[i] = RawQuaternion.Identity;
        }

        public bool IsRunning => _running;

        public void Start()
        {
            if (_running) return;
            _running = true;
            _thread = new Thread(ThreadMain)
            {
                IsBackground = true, // no mantiene vivo el proceso al cerrar
                Name = "JoyconUdpClient",
            };
            _thread.Start();
        }

        public void Stop()
        {
            if (!_running) return;
            _running = false;
            CloseSocket(); // desbloquea ReceiveFrom
            if (_thread != null && _thread != Thread.CurrentThread)
            {
                _thread.Join(1000);
            }
            _thread = null;
        }

        public void Dispose() => Stop();

        /// <summary>Última orientación cruda del sensor. False si ese sensor nunca ha llegado.</summary>
        public bool TryGetSample(int sensorIndex, out JoyconSensorSample sample)
        {
            sample = default;
            if (sensorIndex < 0 || sensorIndex >= JoyconProtocol.MaxSensors) return false;
            long now = Stopwatch.GetTimestamp();
            lock (_lock)
            {
                long t = _lastUpdateTimestamp[sensorIndex];
                if (t == 0) return false;
                sample.HasData = true;
                sample.Orientation = _orientations[sensorIndex];
                sample.AgeSeconds = (now - t) / (double)Stopwatch.Frequency;
            }
            return true;
        }

        /// <summary>Copia el estado de todos los sensores de una sola vez (un único lock por frame).</summary>
        public void CopySamples(JoyconSensorSample[] destination)
        {
            if (destination == null) throw new ArgumentNullException(nameof(destination));
            long now = Stopwatch.GetTimestamp();
            lock (_lock)
            {
                int n = Math.Min(destination.Length, JoyconProtocol.MaxSensors);
                for (int i = 0; i < n; i++)
                {
                    long t = _lastUpdateTimestamp[i];
                    destination[i].HasData = t != 0;
                    destination[i].Orientation = _orientations[i];
                    destination[i].AgeSeconds = t == 0 ? double.PositiveInfinity : (now - t) / (double)Stopwatch.Frequency;
                }
            }
        }

        public JoyconClientStatus GetStatus()
        {
            long now = Stopwatch.GetTimestamp();
            lock (_lock)
            {
                return new JoyconClientStatus
                {
                    PacketsReceived = _packetsReceived,
                    InvalidPackets = _invalidPackets,
                    SecondsSinceLastPacket = _lastPacketTimestamp == 0
                        ? double.PositiveInfinity
                        : (now - _lastPacketTimestamp) / (double)Stopwatch.Frequency,
                    SensorCount = _sensorCount,
                    LastError = _lastError,
                };
            }
        }

        // ------------------------------------------------------------------
        // Hilo de red
        // ------------------------------------------------------------------

        private void ThreadMain()
        {
            byte[] hello = JoyconProtocol.EncodeHello();
            // Un poco más grande que el máximo para detectar paquetes ajenos/truncados sin excepción.
            byte[] recvBuf = new byte[JoyconProtocol.MaxPacketSize + 64];
            var poses = new SensorPose[JoyconProtocol.MaxSensors];
            IPEndPoint server = null;
            long lastHello = 0;
            long helloTicks = (long)(HelloInterval.TotalSeconds * Stopwatch.Frequency);

            while (_running)
            {
                try
                {
                    var sock = _socket;
                    if (sock == null || server == null)
                    {
                        server = ResolveServer();
                        sock = CreateSocket();
                        _socket = sock;
                        if (!_running) break; // Stop() llegó mientras se creaba
                        lastHello = 0;
                        SetError(null);
                    }

                    long now = Stopwatch.GetTimestamp();
                    if (lastHello == 0 || now - lastHello >= helloTicks)
                    {
                        sock.SendTo(hello, server);
                        lastHello = now;
                    }

                    EndPoint from = new IPEndPoint(IPAddress.Any, 0);
                    int received;
                    try
                    {
                        received = sock.ReceiveFrom(recvBuf, ref from);
                    }
                    catch (SocketException e) when (e.SocketErrorCode == SocketError.TimedOut ||
                                                    e.SocketErrorCode == SocketError.WouldBlock ||
                                                    e.SocketErrorCode == SocketError.ConnectionReset ||
                                                    e.SocketErrorCode == SocketError.MessageSize)
                    {
                        // Timeout normal (sin tráfico) o ICMP port-unreachable: servidor aún no arranca.
                        continue;
                    }

                    HandlePacket(recvBuf, received, poses);
                }
                catch (ObjectDisposedException)
                {
                    // Stop() cerró el socket.
                    break;
                }
                catch (Exception e)
                {
                    if (!_running) break;
                    // DNS que no resuelve, red caída, etc.: reintentar en 1 s con un socket nuevo.
                    SetError($"{e.GetType().Name}: {e.Message}");
                    CloseSocket();
                    server = null;
                    SleepWhileRunning(1000);
                }
            }

            CloseSocket();
        }

        private void HandlePacket(byte[] buf, int length, SensorPose[] poses)
        {
            if (!JoyconProtocol.TryDecodeHeader(buf, length, out var type) || type != MessageType.PoseUpdate)
            {
                lock (_lock) _invalidPackets++;
                return;
            }

            int count = JoyconProtocol.DecodePoseUpdate(buf, length, poses);
            if (count < 0)
            {
                lock (_lock) _invalidPackets++;
                return;
            }

            long now = Stopwatch.GetTimestamp();
            lock (_lock)
            {
                for (int i = 0; i < count; i++)
                {
                    int idx = poses[i].SensorIndex;
                    if (idx >= JoyconProtocol.MaxSensors) continue;
                    if (!TryNormalize(poses[i].Orientation, out var q)) continue; // NaN o norma ~0: descartar
                    _orientations[idx] = q;
                    _lastUpdateTimestamp[idx] = now;
                }
                _sensorCount = count;
                _packetsReceived++;
                _lastPacketTimestamp = now;
            }
        }

        private IPEndPoint ResolveServer()
        {
            // El servidor solo escucha IPv4 (AF_INET), así que se fuerza IPv4.
            if (IPAddress.TryParse(ServerHost, out var ip))
            {
                return new IPEndPoint(ip, ServerPort);
            }
            foreach (var addr in Dns.GetHostAddresses(ServerHost))
            {
                if (addr.AddressFamily == AddressFamily.InterNetwork)
                {
                    return new IPEndPoint(addr, ServerPort);
                }
            }
            throw new SocketException((int)SocketError.HostNotFound);
        }

        private static Socket CreateSocket()
        {
            var s = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            try
            {
                s.IOControl(SioUdpConnReset, new byte[] { 0, 0, 0, 0 }, null);
            }
            catch (Exception)
            {
                // No es Windows (Quest/Android, macOS...): no hace falta.
            }
            s.ReceiveTimeout = 250; // ms: permite re-enviar hello y revisar _running sin tráfico
            s.Bind(new IPEndPoint(IPAddress.Any, 0)); // puerto local efímero, igual que el cliente C++
            return s;
        }

        private void CloseSocket()
        {
            var s = Interlocked.Exchange(ref _socket, null);
            if (s == null) return;
            try { s.Close(); } catch (Exception) { /* ya cerrado */ }
        }

        private void SetError(string message)
        {
            lock (_lock) _lastError = message;
        }

        private void SleepWhileRunning(int milliseconds)
        {
            const int step = 50;
            for (int waited = 0; waited < milliseconds && _running; waited += step)
            {
                Thread.Sleep(step);
            }
        }

        private static bool TryNormalize(RawQuaternion q, out RawQuaternion result)
        {
            double n = Math.Sqrt((double)q.X * q.X + (double)q.Y * q.Y + (double)q.Z * q.Z + (double)q.W * q.W);
            if (double.IsNaN(n) || double.IsInfinity(n) || n < 1e-6)
            {
                result = RawQuaternion.Identity;
                return false;
            }
            result = new RawQuaternion((float)(q.X / n), (float)(q.Y / n), (float)(q.Z / n), (float)(q.W / n));
            return true;
        }
    }
}
