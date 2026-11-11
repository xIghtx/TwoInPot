using System;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Services.Core;
using Unity.Services.Core.Environments;
using Unity.Services.Authentication;
using Unity.Services.Multiplayer;
using UnityEngine;

namespace TwoInPot
{
    public sealed class SessionController : MonoBehaviour
    {
        public NetworkManager manager;
        public UnityTransport transport;
        public string Status { get; private set; } = "Choose how to connect.";
        public string JoinCode { get; private set; } = "";
        public bool Busy { get; private set; }
        public bool Online => manager.IsListening;
        public bool UsesOnlineRoom => session != null || onlineCreating;
        public bool Ready => manager.IsConnectedClient && (!onlineCreating || !string.IsNullOrEmpty(JoinCode));
        public bool HostConnectionLost { get; private set; }
        public string LocalAddress { get; private set; } = "127.0.0.1";
        public event Action Changed;
        ISession session;
        readonly HashSet<ulong> reservations = new HashSet<ulong>();
        static readonly byte[] Protocol = Encoding.UTF8.GetBytes("TwoInPot:festival-circuits:8");
        float connectionStarted;
        bool connecting, leaving, onlineCreating;
        void Awake()
        {
            Application.runInBackground = true;
            transport.MaxPacketQueueSize = 1024;
            manager.NetworkConfig.TickRate = 30;
            Application.targetFrameRate = 60;
            try
            {
                foreach (var adapter in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (adapter.OperationalStatus != OperationalStatus.Up || adapter.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;
                    var properties = adapter.GetIPProperties();
                    if (properties.GatewayAddresses.Count == 0) continue;
                    foreach (var ip in properties.UnicastAddresses)
                        if (ip.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork) { LocalAddress = ip.Address.ToString(); break; }
                    if (LocalAddress != "127.0.0.1") break;
                }
            }
            catch (Exception) { /* Manual address entry remains available. */ }
            manager.NetworkConfig.ConnectionApproval = true;
            manager.NetworkConfig.ConnectionData = Protocol;
            manager.ConnectionApprovalCallback += Approve;
            manager.OnClientConnectedCallback += Connected;
            manager.OnClientDisconnectCallback += Disconnected;
            manager.OnTransportFailure += TransportFailed;
        }
        void OnDestroy()
        {
            manager.ConnectionApprovalCallback -= Approve;
            manager.OnClientConnectedCallback -= Connected;
            manager.OnClientDisconnectCallback -= Disconnected;
            manager.OnTransportFailure -= TransportFailed;
        }
        void Notify(string text) { Status = text; Changed?.Invoke(); Debug.Log("[TwoInPot] " + text); }
        void Approve(NetworkManager.ConnectionApprovalRequest request, NetworkManager.ConnectionApprovalResponse response)
        {
            bool matching = request.Payload != null && Encoding.UTF8.GetString(request.Payload) == Encoding.UTF8.GetString(Protocol);
            bool available = reservations.Count < 2;
            response.Approved = matching && available;
            response.CreatePlayerObject = response.Approved;
            response.Pending = false;
            response.Reason = !matching ? "Game versions differ. Use the same build." : "This room already has two players.";
            if (response.Approved)
            {
                reservations.Add(request.ClientNetworkId);
                response.Position = request.ClientNetworkId == NetworkManager.ServerClientId ? new Vector3(-7, 0, 0) : new Vector3(-10, 0, 0);
            }
        }
        void Connected(ulong id)
        {
            if (id == manager.LocalClientId) { connecting = false; Busy = false; }
            Notify(manager.IsHost ? "Room open. Waiting for your partner, or explore together." : "Connected. Choose your role.");
        }
        void Disconnected(ulong id)
        {
            reservations.Remove(id);
            if (leaving) return;
            if (manager.IsServer && id != manager.LocalClientId)
            {
                UnityEngine.Object.FindAnyObjectByType<StageState>()?.ResetSession();
                Notify("Partner left. The room is open for a new partner."); return;
            }
            if (!manager.IsServer) HostConnectionLost = true;
            connecting = false;
            Busy = false;
            string reason = manager.DisconnectReason;
            bool friendly = reason == "This room already has two players." || reason == "Game versions differ. Use the same build.";
            _ = LeaveAsync(friendly ? reason : "Connection ended. Create or join another room.");
        }
        public void DismissHostConnectionLost() => HostConnectionLost = false;
        void TransportFailed() => _ = LeaveAsync("Network unavailable. Check the address and firewall, then retry.");
        void Update()
        {
            if (connecting && Time.realtimeSinceStartup - connectionStarted > 15f)
                _ = LeaveAsync("Connection timed out. Check the host address and UDP port 7777.");
        }
        public void HostDirect()
        {
            if (Busy || Online || session != null) return;
            reservations.Clear();
            transport.SetConnectionData("127.0.0.1", 7777, "0.0.0.0");
            Notify("Starting a local room...");
            if (!manager.StartHost()) Notify("Could not open port 7777. Another host may already be running.");
        }
        public void JoinDirect(string address)
        {
            if (Busy || Online || session != null) return;
            if (!IPAddress.TryParse(address.Trim(), out var ip) || ip.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork)
            { Notify("Enter the host PC's IPv4 address, for example 192.168.0.10."); return; }
            transport.SetConnectionData(ip.ToString(), 7777);
            Busy = true;
            connecting = true;
            connectionStarted = Time.realtimeSinceStartup;
            Notify("Connecting to " + ip + "...");
            if (!manager.StartClient()) { connecting = false; Busy = false; Notify("Could not start the connection. Retry."); }
        }
        async Task InitializeOnline()
        {
            if (UnityServices.State != ServicesInitializationState.Initialized)
            {
                var options = new InitializationOptions();
                // Separate identities for simultaneous standalone/editor test processes.
                options.SetProfile("pot" + System.Diagnostics.Process.GetCurrentProcess().Id);
                await UnityServices.InitializeAsync(options);
            }
            if (!AuthenticationService.Instance.IsSignedIn) await AuthenticationService.Instance.SignInAnonymouslyAsync();
        }
        public async Task HostOnlineAsync() => await RunOnline(true, "");
        public async Task JoinOnlineAsync(string code) => await RunOnline(false, code.Trim().ToUpperInvariant());
        async Task RunOnline(bool host, string code)
        {
            if (Busy || Online || session != null) return;
            if (!host && code.Length == 0) { Notify("Enter your partner's room code."); return; }
            Busy = true;
            reservations.Clear();
            Notify(host ? "Creating an internet room..." : "Joining internet room...");
            try
            {
                await InitializeOnline();
                // Session networking starts NGO; never call StartHost/StartClient on this path.
                session = host
                    ? await MultiplayerService.Instance.CreateSessionAsync(new SessionOptions { MaxPlayers = 2, IsPrivate = true, Type = "two-in-pot" }.WithRelayNetwork())
                    : await MultiplayerService.Instance.JoinSessionByCodeAsync(code);
                JoinCode = session.Code;
                Notify(host ? "Room created. Share this code: " + JoinCode : "Connected to room " + JoinCode);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[TwoInPot] Online: " + ex.Message);
                await LeaveAsync("Internet connection failed. Check internet / Unity project services. Local IP mode is also available.");
            }
            finally { Busy = false; onlineCreating = false; Changed?.Invoke(); }
        }
        public async Task LeaveAsync(string message = "Disconnected. Ready for a new room.")
        {
            if (leaving) return;
            leaving = true; Busy = true; connecting = false;
            try
            {
                var old = session; session = null;
                if (old != null) await old.LeaveAsync();
            }
            catch (Exception ex) { Debug.LogWarning("[TwoInPot] Leave: " + ex.Message); }
            finally
            {
                if (manager.IsListening) manager.Shutdown();
                while (manager != null && manager.ShutdownInProgress) await Task.Yield();
                reservations.Clear(); JoinCode = ""; Busy = false; leaving = false;
                Notify(message);
            }
        }
    }
}

