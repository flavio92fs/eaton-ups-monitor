using System;
using System.IO;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Web.Script.Serialization;
using System.Windows.Forms;

// Offline tests only: never construct a USB session or call Alarm/SetAlarm.
class TelemetryTests
{
    static int checks;
    static void Assert(bool condition,string text){if(!condition)throw new Exception(text);checks++;Console.WriteLine("PASS "+text);}
    static byte[] Hex(string h){var pieces=h.Split('-');var result=new byte[pieces.Length];for(int i=0;i<result.Length;i++)result[i]=Convert.ToByte(pieces[i],16);return result;}
    static bool Rejected(Action action){try{action();return false;}catch{return true;}}
    static object Validate(byte[] descriptor){return typeof(EatonUsbProbe).GetMethod("ValidateAlarmDescriptor",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{descriptor});}
    static List<string> Texts(Control root){var result=new List<string>{root.Text};foreach(Control child in root.Controls)result.AddRange(Texts(child));return result;}
    [STAThread] static int Main(string[] args)
    {
        try {
            L.Initialize(new string[]{"--lang","en"});
            string descriptor=File.ReadAllText(args[0]).Split(new string[]{"ReportDescriptor="},StringSplitOptions.None)[1].Trim();
            byte[] layout=Hex(descriptor);
            Dictionary<byte,int> lengths;var fields=EatonUsbProbe.ParseFeatures(layout,out lengths);
            var raw=(Dictionary<string,object>)new JavaScriptSerializer().DeserializeObject(File.ReadAllText(args[1]));
            var packets=new Dictionary<byte,byte[]>();foreach(var p in raw)packets[Convert.ToByte(p.Key,16)]=Hex((string)p.Value);
            var s=EatonUsbProbe.Decode(fields,packets,null);
            Assert(lengths[1]==4,"Status report has three data bytes and its ID");
            Assert(s.Get("UPS.PowerSummary.RemainingCapacity")==96,"Battery capacity decoded");
            Assert(s.Get("UPS.PowerConverter.Input[1].Voltage")==240,"Input voltage scaling");
            Assert(Math.Abs(s.Get("UPS.PowerSummary.Voltage").Value-27.2)<0.01,"Battery voltage scaling");
            Assert(s.Get("UPS.PowerConverter.Output.Frequency")==50,"Negative exponent: 50 Hz");
            Assert(s.Get("UPS.PowerSummary.DelayBeforeShutdown")==-1,"Signed delay -1 decoded without sending any command");
            Assert(s.Get("UPS.Flow[4].ConfigActivePower")==900 && s.Get("UPS.Flow[4].ConfigApparentPower")==1600,"Rated power 900 W / 1600 VA");
            bool dynamicAc=false;foreach(var f in fields)if(f.Path=="UPS.PowerSummary.PresentStatus.ACPresent")dynamicAc=!f.Constant;
            Assert(dynamicAc,"Mains status is not cached as constant");
            packets[1][1]&=0xfe;
            Assert(!EatonUsbProbe.Decode(fields,packets,null).AcPresent,"Offline packet simulates loss of mains");
            packets[1][1]|=1;
            Assert(EatonUsbProbe.Decode(fields,packets,null).AcPresent,"Offline packet simulates restored mains");
            packets[6]=new byte[]{6};Assert(Rejected(()=>EatonUsbProbe.Decode(fields,packets,null)),"Truncated report rejected");
            byte[] malformed=(byte[])layout.Clone();Array.Resize(ref malformed,malformed.Length-1);
            Assert(Rejected(()=>EatonUsbProbe.ParseFeatures(malformed,out lengths)),"Truncated descriptor rejected");
            Assert((byte)Validate(layout)==0x14,"Dedicated one-byte alarm report validated");
            var wrongReport=(byte[])layout.Clone();
            for(int i=0;i<wrongReport.Length-1;i++)if(wrongReport[i]==0x85 && wrongReport[i+1]==0x14){wrongReport[i+1]=0x15;break;}
            Assert(Rejected(()=>Validate(wrongReport)),"Different alarm report ID rejected");
            var wrongUsage=(byte[])layout.Clone();
            for(int i=0;i<wrongUsage.Length-1;i++)if(wrongUsage[i]==0x09 && wrongUsage[i+1]==0x5a){wrongUsage[i+1]=0x59;break;}
            Assert(Rejected(()=>Validate(wrongUsage)),"Missing alarm usage rejected");
            Assert(L.T("Disabilita cicalino")=="Disable buzzer" && L.T("non disponibile")=="unavailable","English controls and unavailable state");
            Assert(MonitorForm.Runtime(90)=="1 min 30 s" && MonitorForm.Runtime(-1)=="unavailable","Runtime formatting and invalid sentinel");
            var remote=ServerTelemetry.Decode("{\"ok\":true,\"values\":{\"ups.status\":\"OB LB\",\"battery.charge\":\"24\",\"ups.realpower\":\"181\",\"ups.beeper.status\":\"disabled\"}}");
            Assert(!remote.AcPresent && remote.Get("UPS.PowerSummary.RemainingCapacity")==24
                && remote.Get("UPS.PowerConverter.Output.ActivePower")==181
                && remote.Get("UPS.PowerSummary.AudibleAlarmControl")==1,
                "Server status, battery, active power and beeper map to the monitor");
            Assert(Rejected(()=>ServerTelemetry.Decode("{\"ok\":true,\"values\":{\"ups.status\":\"WAIT\"}}"))
                && Rejected(()=>ServerTelemetry.Decode("{\"ok\":false,\"error\":\"ups_unavailable\"}")),
                "Unavailable server data never becomes a blackout");
            Assert(ServerTelemetry.ParseAddress("http://server:8099/api/ups").AbsoluteUri=="http://server:8099/"
                && Rejected(()=>ServerTelemetry.ParseAddress("http://user:password@server:8099")),
                "Server addresses are normalized and embedded credentials are rejected");
            string testBeeperKey=new string('a',64);
            Assert(ServerTelemetry.CanSetBeeper(new Uri("http://100.64.0.1:8099"),testBeeperKey)
                && !ServerTelemetry.CanSetBeeper(new Uri("http://100.128.0.1:8099"),testBeeperKey)
                && !ServerTelemetry.CanSetBeeper(new Uri("http://192.0.2.10:8099"),testBeeperKey)
                && !ServerTelemetry.CanSetBeeper(new Uri("http://100.64.0.1:8099"),""),
                "Remote beeper requires a Tailscale address and a valid key");
            Application.EnableVisualStyles();
            using(var setup=new SetupForm())Assert(Texts(setup).Contains("Install / Update"),"English installer controls");
            using(var mutex=new Mutex())using(var monitor=new MonitorForm(mutex,false)) {
                var text=Texts(monitor);
                Assert(text.Contains("Settings") && text.Contains("Disable buzzer") && text.Contains("Exit completely") && text.Contains("BATTERY") && text.Contains("ESTIMATED RUNTIME") && text.Contains("LOAD"),"English monitor UI and all summary cards without opening USB");
                typeof(MonitorForm).GetMethod("Cleanup",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(monitor,new object[]{true});
            }
            L.Initialize(new string[]{"--lang","it"});
            Assert(L.T("Disabilita cicalino")=="Disabilita cicalino" && MonitorForm.Runtime(-1)=="non disponibile","Italian controls and unavailable state");
            using(var setup=new SetupForm())Assert(Texts(setup).Contains("Installa / Aggiorna"),"Italian installer controls");
            var assembly=Assembly.ReflectionOnlyLoadFrom(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"EatonMonitor.exe"));
            var resources=new List<string>(assembly.GetManifestResourceNames());
            Assert(resources.Contains("EatonMonitor.Readme") && resources.Contains("EatonMonitor.ReadmeIt"),"Both READMEs embedded for installation");
            Assert(resources.Count==3 && resources.Contains("EatonMonitor.License"),"License embedded; no vendor driver resources in release");
            Assert(AppIntegration.StartupCommand=="\""+AppIntegration.InstalledExe+"\" --tray","Startup path is quoted and uses tray mode");
            Console.WriteLine(checks+" checks passed. No USB device was opened.");return 0;
        }catch(Exception ex){Console.Error.WriteLine(ex);return 1;}
    }
}
