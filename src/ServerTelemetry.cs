using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;

public static class ServerTelemetry
{
    const string Summary = "UPS.PowerSummary.";
    const string Output = "UPS.PowerConverter.Output.";
    const string Input = "UPS.PowerConverter.Input[1].";
    static readonly HttpClient Client = new HttpClient(new HttpClientHandler {
        UseProxy = false, AllowAutoRedirect = false, UseCookies = false
    }) { Timeout = TimeSpan.FromSeconds(4), MaxResponseContentBufferSize = 32768 };
    static readonly HttpClient Commands = new HttpClient(new HttpClientHandler {
        UseProxy = false, AllowAutoRedirect = false, UseCookies = false
    }) { Timeout = TimeSpan.FromSeconds(24), MaxResponseContentBufferSize = 4096 };

    public static bool CanSetBeeper(Uri address, string token)
    {
        IPAddress ip;
        if (address == null || !IPAddress.TryParse(address.Host, out ip)
            || !Regex.IsMatch(token ?? "", @"\A[0-9a-f]{64}\z"))
            return false;
        byte[] octets = ip.GetAddressBytes();
        return octets.Length == 4 && octets[0] == 100 && (octets[1] & 0xc0) == 64;
    }

    public static string SetBeeper(Uri address, string token, int value)
    {
        if (!CanSetBeeper(address, token))
            throw new InvalidOperationException(L.T("Per i comandi serve un indirizzo IPv4 Tailscale e la chiave cicalino."));
        string action;
        switch (value) {
            case 1: action = "disable"; break;
            case 2: action = "enable"; break;
            case 3: action = "mute"; break;
            default: throw new ArgumentOutOfRangeException("value");
        }
        using (var request = new HttpRequestMessage(HttpMethod.Post, new Uri(address, "/api/beeper/" + action))) {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            using (var response = Commands.SendAsync(request).GetAwaiter().GetResult()) {
                if (response.StatusCode == HttpStatusCode.Unauthorized)
                    throw new InvalidOperationException(L.T("La chiave cicalino non è accettata dal server."));
                if (!response.IsSuccessStatusCode)
                    throw new InvalidOperationException(L.T("Il server non ha confermato il comando del cicalino."));
                var json = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                var data = new JavaScriptSerializer().Deserialize<Dictionary<string, object>>(json);
                object ok;
                if (data == null || !data.TryGetValue("ok", out ok) || !object.Equals(ok, true)
                    || Text(data, "action") != action)
                    throw new FormatException(L.T("Il server non ha confermato il comando del cicalino."));
                return L.T("Confermato dal server");
            }
        }
    }

    public static Uri ParseAddress(string address)
    {
        Uri uri;
        if (!Uri.TryCreate((address ?? "").Trim(), UriKind.Absolute, out uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
            || uri.Host.Length == 0 || uri.UserInfo.Length != 0
            || uri.Query.Length != 0 || uri.Fragment.Length != 0
            || (uri.AbsolutePath != "/" && uri.AbsolutePath != "/api/ups"))
            throw new ArgumentException(L.T("Inserisci l’indirizzo HTTP o HTTPS della dashboard, inclusa la porta."));
        return new Uri(uri.GetLeftPart(UriPartial.Authority));
    }

    public static UpsSnapshot Poll(Uri address)
    {
        return Decode(Client.GetStringAsync(new Uri(address, "/api/ups")).GetAwaiter().GetResult());
    }

    public static UpsSnapshot Decode(string json)
    {
        var response = new JavaScriptSerializer().Deserialize<Dictionary<string, object>>(json);
        object ok, values;
        if (response == null || !response.TryGetValue("ok", out ok) || !(ok is bool) || !(bool)ok
            || !response.TryGetValue("values", out values) || !(values is Dictionary<string, object>))
            throw new FormatException(L.T("Il server non ha restituito dati UPS validi."));

        var nut = (Dictionary<string, object>)values;
        var flags = new HashSet<string>((Text(nut, "ups.status") ?? "").Split(' '));
        bool online = flags.Contains("OL");
        bool onBattery = flags.Contains("OB");
        if (online == onBattery || flags.Contains("WAIT") || flags.Contains("OFFLINE"))
            throw new FormatException(L.T("Lo stato dell’alimentazione non è disponibile dal server."));

        var snapshot = new UpsSnapshot { Captured = DateTime.Now };
        Put(snapshot, Summary + "PresentStatus.ACPresent", online ? 1 : 0);
        Put(snapshot, Summary + "PresentStatus.Discharging", onBattery || flags.Contains("DISCHRG") ? 1 : 0);
        if (flags.Contains("CHRG")) Put(snapshot, Summary + "PresentStatus.Charging", 1);
        Put(snapshot, Summary + "PresentStatus.Overload", flags.Contains("OVER") ? 1 : 0);
        Put(snapshot, Summary + "PresentStatus.NeedReplacement", flags.Contains("RB") ? 1 : 0);
        Put(snapshot, Summary + "PresentStatus.BelowRemainingCapacityLimit", flags.Contains("LB") ? 1 : 0);
        Put(snapshot, Input + "PresentStatus.Boost", flags.Contains("BOOST") ? 1 : 0);
        Put(snapshot, Input + "PresentStatus.Buck", flags.Contains("TRIM") ? 1 : 0);
        CopyNumber(nut, "battery.charge", snapshot, Summary + "RemainingCapacity");
        CopyNumber(nut, "battery.runtime", snapshot, Summary + "RunTimeToEmpty");
        CopyNumber(nut, "battery.voltage", snapshot, Summary + "Voltage");
        CopyNumber(nut, "ups.load", snapshot, Summary + "PercentLoad");
        CopyNumber(nut, "ups.realpower", snapshot, Output + "ActivePower");
        CopyNumber(nut, "ups.power", snapshot, Output + "ApparentPower");
        CopyNumber(nut, "ups.realpower.nominal", snapshot, "UPS.Flow[4].ConfigActivePower");
        CopyNumber(nut, "ups.power.nominal", snapshot, "UPS.Flow[4].ConfigApparentPower");
        CopyNumber(nut, "input.voltage", snapshot, Input + "Voltage");
        CopyNumber(nut, "output.voltage", snapshot, Output + "Voltage");
        CopyNumber(nut, "output.frequency", snapshot, Output + "Frequency");
        string beeper = Text(nut, "ups.beeper.status");
        if (beeper == "disabled" || beeper == "enabled" || beeper == "muted")
            Put(snapshot, Summary + "AudibleAlarmControl", beeper == "disabled" ? 1 : beeper == "enabled" ? 2 : 3);

        snapshot.Values[Summary + "iProduct"] = new UpsValue { Text = Text(nut, "ups.model") ?? Text(nut, "device.model") ?? "Eaton" };
        snapshot.Values[Summary + "iVersion"] = new UpsValue { Text = Text(nut, "ups.firmware") };
        return snapshot;
    }

    static string Text(Dictionary<string, object> values, string key)
    {
        object value;
        return values.TryGetValue(key, out value) ? value as string : null;
    }

    static void CopyNumber(Dictionary<string, object> values, string key, UpsSnapshot snapshot, string path)
    {
        double number;
        if (double.TryParse(Text(values, key), NumberStyles.Float, CultureInfo.InvariantCulture, out number)
            && !double.IsInfinity(number) && !double.IsNaN(number))
            Put(snapshot, path, number);
    }

    static void Put(UpsSnapshot snapshot, string path, double number)
    {
        snapshot.Values[path] = new UpsValue { Number = number };
    }
}
