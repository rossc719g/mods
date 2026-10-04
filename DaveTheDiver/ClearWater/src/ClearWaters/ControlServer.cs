using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace ClearWaters;

// Optional local-subnet controls without a login. Bounded sockets
// avoid HTTP.sys reservations. Only these visual settings can be changed remotely.
public sealed class ControlServer : IDisposable
{
    private readonly TcpListener listener;
    private readonly HashSet<TcpClient> clients = new();
    private readonly LocalSubnets subnets = new();
    private readonly SettingsStore settings;
    private readonly Action<SettingsSnapshot> saveSettings;
#if WEB_FILE_OVERRIDES
    private readonly string webDirectory;
    public static bool SupportsWebFileOverrides => true;
#else
    public static bool SupportsWebFileOverrides => false;
#endif
#if RENDER_DIAGNOSTICS
    private volatile byte[] diagnostics = Encoding.UTF8.GetBytes("null");
    public static bool SupportsRenderDiagnostics => true;
    public void PublishDiagnostics(byte[] json) => diagnostics = json;
#else
    public static bool SupportsRenderDiagnostics => false;
#endif
    private readonly string sessionId = Guid.NewGuid().ToString("N");
    private volatile bool stopped;
    public bool IsStopped => stopped;
    public int Port => ((IPEndPoint)listener.LocalEndpoint).Port;

    public ControlServer(IPAddress bindAddress, int port, SettingsStore settings, string webDirectory, Action<SettingsSnapshot> saveSettings)
    {
        listener = new TcpListener(bindAddress, port);
        listener.Server.ExclusiveAddressUse = true;
        this.settings = settings;
        this.saveSettings = saveSettings;
#if WEB_FILE_OVERRIDES
        this.webDirectory = webDirectory;
#endif
    }
    public void Start()
    {
        if (!settings.Current.WebEnabled) throw new InvalidOperationException("Web controls are disabled in the configuration.");
        listener.Start(16); _ = Task.Run(AcceptLoop);
    }

    private void AcceptLoop()
    {
        while (!stopped)
        {
            TcpClient client;
            try { client = listener.AcceptTcpClient(); }
            catch (SocketException) { if (stopped) return; continue; }
            catch (ObjectDisposedException) { return; }
            lock (clients)
            {
                if (stopped || clients.Count >= 12) { client.Close(); continue; }
                clients.Add(client);
            }
            _ = Task.Run(() => Handle(client));
        }
    }

    private void Handle(TcpClient client)
    {
        try
        {
            client.ReceiveTimeout = client.SendTimeout = 2000;
            client.NoDelay = true;
            using var stream = client.GetStream();
            var remote = ((IPEndPoint)client.Client.RemoteEndPoint!).Address;
            if (!subnets.Contains(remote)) { Reply(stream, 403, "Home subnet only."); return; }
            var buffer = new byte[8192];
            int used = 0, end = -1;
            var timer = Stopwatch.StartNew();
            while (end < 0 && used < buffer.Length && timer.ElapsedMilliseconds < 4000)
            {
                int read = stream.Read(buffer, used, buffer.Length - used);
                if (read == 0) return;
                int scan = Math.Max(0, used - 3);
                used += read;
                for (int i = scan; i <= used - 4; i++)
                    if (buffer[i] == 13 && buffer[i + 1] == 10 && buffer[i + 2] == 13 && buffer[i + 3] == 10)
                    { end = i; break; }
            }
            if (end < 0) { Reply(stream, 431, "Header limit exceeded."); return; }
            var lines = Encoding.ASCII.GetString(buffer, 0, end).Split("\r\n");
            var request = lines[0].Split(' ');
            if (request.Length != 3 || (request[2] != "HTTP/1.1" && request[2] != "HTTP/1.0"))
            { Reply(stream, 400, "Invalid request."); return; }
            var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var line in lines.Skip(1))
            {
                int colon = line.IndexOf(':');
                if (colon <= 0 || !headers.TryAdd(line[..colon], line[(colon + 1)..].Trim()))
                { Reply(stream, 400, "Invalid or duplicate header."); return; }
            }
            if (!headers.TryGetValue("Host", out var host) || !ValidHost(host))
            { Reply(stream, 403, "Use this computer's name or IP address."); return; }
            if (headers.ContainsKey("Transfer-Encoding")) { Reply(stream, 400, "Chunked requests are not supported."); return; }
            var method = request[0];
            var path = request[1].Split('?')[0];
            bool head = method == "HEAD";
            if (method == "GET" || head)
            {
                if (path == "/api/state") Json(stream, ReadControlState(), head);
                else if (path == "/api/settings") Json(stream, new { settings = settings.Current }, head);
                else if (path == "/api/health") Json(stream, new { status = "ok", game = "Dave the Diver", version = "0.2.0", webFileOverrides = SupportsWebFileOverrides, renderDiagnostics = SupportsRenderDiagnostics }, head);
                else if (path is "/" or "/index.html" or "/app.js" or "/style.css") SendAsset(stream, path, head);
                else Reply(stream, 404, "Not found.", head);
            }
            else if ((method == "PATCH" && path == "/api/settings") || (method == "POST" && path == "/api/server/stop"))
            {
                if (!headers.TryGetValue("X-ClearWaters", out var marker) || marker != "1" ||
                    !headers.TryGetValue("Content-Type", out var contentType) || contentType.Split(';')[0].Trim() != "application/json" ||
                    (headers.TryGetValue("Origin", out var origin) && !string.Equals(origin, "http://" + host, StringComparison.OrdinalIgnoreCase)))
                { Reply(stream, 403, "Use the control page or send JSON with X-ClearWaters: 1."); return; }
                if (!headers.TryGetValue("Content-Length", out var lengthText) || !int.TryParse(lengthText, out var length) || length < 1 || length > 4096)
                { Reply(stream, 413, "A body of 1 to 4096 bytes is required."); return; }
                var body = new byte[length];
                int received = Math.Min(length, used - end - 4);
                Buffer.BlockCopy(buffer, end + 4, body, 0, received);
                while (received < length && timer.ElapsedMilliseconds < 4000)
                {
                    int read = stream.Read(body, received, length - received);
                    if (read == 0) return;
                    received += read;
                }
                if (received != length) { Reply(stream, 408, "Request timed out."); return; }
                try
                {
                    if (path == "/api/server/stop")
                    {
                        using var document = JsonDocument.Parse(body);
                        if (document.RootElement.ValueKind != JsonValueKind.Object || document.RootElement.EnumerateObject().Any())
                            throw new ArgumentException("Send an empty JSON object to stop the web controls.");
                        var saved = settings.DisableWeb(saveSettings);
                        // Acknowledge the completed save before closing this connection.
                        try { Json(stream, new { stopped = true, settings = saved }); }
                        finally { Dispose(); }
                    }
                    else Json(stream, new { settings = settings.Change(body, fromWeb: true), pending = true });
                }
                catch (Exception e) when (e is JsonException or ArgumentException) { Reply(stream, 400, e.Message); }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidOperationException)
                { Reply(stream, 503, "Could not complete the request; web controls remain available unless already stopped. " + e.Message); }
            }
            else Reply(stream, 405, "Supported: GET, HEAD, PATCH /api/settings, and POST /api/server/stop.");
        }
        catch (Exception e) when (e is IOException or SocketException or ObjectDisposedException or NetworkInformationException) { }
        finally { client.Close(); lock (clients) clients.Remove(client); }
    }

    private object ReadControlState()
    {
        var state = settings.ReadForControls();
        return new {
            schemaVersion = 3, ready = true, version = "0.2.0", sessionId,
            capabilities = new { stopWeb = true, renderDiagnostics = SupportsRenderDiagnostics },
            settings = state.Settings, persistedRevision = state.PersistedRevision,
            persistenceError = state.PersistenceError,
#if RENDER_DIAGNOSTICS
            diagnostics = JsonSerializer.Deserialize<JsonElement>(diagnostics),
#endif
            effects = Effects.All.Select(e => new { e.Id, e.Label, e.Description, e.Group,
                remove = state.Settings.Effects[e.Id], defaultRemove = e.DefaultRemove }).ToArray()
        };
    }

    private bool ValidHost(string host)
    {
        if (!Uri.TryCreate("http://" + host, UriKind.Absolute, out var uri) || uri.Port != Port || uri.AbsolutePath != "/" || uri.UserInfo.Length != 0)
            return false;
        return uri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase) ||
               uri.Host.Equals(Environment.MachineName, StringComparison.OrdinalIgnoreCase) ||
               (IPAddress.TryParse(uri.Host, out var address) && subnets.IsLocalAddress(address));
    }

    private void SendAsset(Stream stream, string path, bool head)
    {
        var filename = path is "/" or "/index.html" ? "index.html" : path[1..];
        var type = filename.EndsWith(".js") ? "application/javascript" : filename.EndsWith(".css") ? "text/css" : "text/html";
        try
        {
            using Stream file = OpenAsset(filename);
            if (file.Length < 1 || file.Length > 1024 * 1024) throw new IOException();
            using var bytes = new MemoryStream();
            file.CopyTo(bytes);
            Send(stream, 200, type + "; charset=utf-8", bytes.ToArray(), head);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        { Reply(stream, 503, "Control-page file unavailable: " + filename, head); }
    }

    private Stream OpenAsset(string filename)
    {
#if WEB_FILE_OVERRIDES
        // Included only in a development build; public builds never read loose pages.
        string overridePath = Path.Combine(webDirectory, filename);
        if (File.Exists(overridePath))
            return new FileStream(overridePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
#endif
        return typeof(ControlServer).Assembly.GetManifestResourceStream("ClearWaters.Web." + filename)
            ?? throw new FileNotFoundException("Embedded control page missing: " + filename);
    }

    private static void Json(Stream stream, object value, bool head = false) => Send(stream, 200, "application/json; charset=utf-8", JsonSerializer.SerializeToUtf8Bytes(value, SettingsStore.JsonOptions), head);
    private static void Reply(Stream stream, int code, string message, bool head = false) => Send(stream, code, "text/plain; charset=utf-8", Encoding.UTF8.GetBytes(message), head);
    private static void Send(Stream stream, int code, string contentType, byte[] body, bool head)
    {
        string reason = code switch { 200 => "OK", 400 => "Bad Request", 403 => "Forbidden", 404 => "Not Found", 405 => "Method Not Allowed", 408 => "Request Timeout", 413 => "Content Too Large", 431 => "Request Header Fields Too Large", _ => "Service Unavailable" };
        var header = Encoding.ASCII.GetBytes($"HTTP/1.1 {code} {reason}\r\nContent-Type: {contentType}\r\nContent-Length: {body.Length}\r\nConnection: close\r\nCache-Control: no-store\r\nX-Content-Type-Options: nosniff\r\n" +
            "Content-Security-Policy: default-src 'self'; connect-src 'self'; script-src 'self'; style-src 'self'; img-src 'self'; object-src 'none'; frame-ancestors 'none'; base-uri 'none'\r\nReferrer-Policy: no-referrer\r\n\r\n");
        stream.Write(header);
        if (!head) stream.Write(body);
    }

    public void Dispose()
    {
        stopped = true;
        listener.Stop();
        lock (clients) foreach (var client in clients) client.Close();
    }
}

public sealed class LocalSubnets
{
    private readonly object gate = new();
    private readonly List<(byte[] Address, byte[] Mask)> networks = new();
    private DateTime refreshAt;
    private void Refresh()
    {
        if (DateTime.UtcNow < refreshAt) return;
        networks.Clear();
        foreach (var adapter in NetworkInterface.GetAllNetworkInterfaces().Where(n => n.OperationalStatus == OperationalStatus.Up))
        foreach (var address in adapter.GetIPProperties().UnicastAddresses)
        {
            if (address.Address.AddressFamily != AddressFamily.InterNetwork || address.IPv4Mask == null) continue;
            var mask = address.IPv4Mask.GetAddressBytes();
            if (mask.All(b => b == 0)) continue;
            networks.Add((address.Address.GetAddressBytes(), mask));
        }
        refreshAt = DateTime.UtcNow.AddSeconds(30);
    }
    public bool Contains(IPAddress remote)
    {
        if (IPAddress.IsLoopback(remote)) return true;
        if (remote.AddressFamily != AddressFamily.InterNetwork) return false;
        lock (gate) { Refresh(); return networks.Any(n => InSubnet(remote.GetAddressBytes(), n.Address, n.Mask)); }
    }
    public bool IsLocalAddress(IPAddress address)
    {
        if (IPAddress.IsLoopback(address)) return true;
        lock (gate) { Refresh(); return networks.Any(n => address.GetAddressBytes().SequenceEqual(n.Address)); }
    }
    public static bool InSubnet(byte[] remote, byte[] local, byte[] mask)
    {
        if (remote.Length != 4 || local.Length != 4 || mask.Length != 4) return false;
        for (int i = 0; i < 4; i++) if ((remote[i] & mask[i]) != (local[i] & mask[i])) return false;
        return true;
    }
}
