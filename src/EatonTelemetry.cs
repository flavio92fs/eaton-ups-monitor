using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;

public class UpsField
{
    public byte Report;
    public int Offset, Bits, Exponent;
    public uint Usage, Unit;
    public long Minimum;
    public string Path, Name;
    public bool Constant;
}
public class UpsValue
{
    public string Name, Path, Unit, Text;
    public double Number;
    public long Raw;
}
public class UpsSnapshot
{
    public DateTime Captured;
    public Dictionary<string,UpsValue> Values=new Dictionary<string,UpsValue>();
    public Dictionary<string,string> Reports=new Dictionary<string,string>();
    public double? Get(string path) { UpsValue v;return Values.TryGetValue(path,out v)?(double?)v.Number:null; }
    public string GetText(string path) { UpsValue v;return Values.TryGetValue(path,out v)?v.Text:null; }
    public bool AcPresent { get { return Get("UPS.PowerSummary.PresentStatus.ACPresent")==1; } }
}

public static partial class EatonUsbProbe
{
    static readonly Dictionary<uint,string> UsageNames=new Dictionary<uint,string> {
        {0x840004,"UPS"},{0x840010,"BatterySystem"},{0x840012,"Battery"},{0x840018,"OutletSystem"},
        {0x84001e,"Flow"},{0x840020,"Outlet"},{0x840016,"PowerConverter"},{0x84001a,"Input"},{0x84001c,"Output"},
        {0x840002,"PresentStatus"},{0x840024,"PowerSummary"},{0x840030,"Voltage"},{0x840032,"Frequency"},
        {0x840033,"ApparentPower"},{0x840034,"ActivePower"},{0x840035,"PercentLoad"},
        {0x840040,"ConfigVoltage"},{0x840042,"ConfigFrequency"},{0x840043,"ConfigApparentPower"},{0x840044,"ConfigActivePower"},
        {0x840057,"DelayBeforeShutdown"},{0x840056,"DelayBeforeStartup"},{0x84005a,"AudibleAlarmControl"},
        {0x84001f,"FlowID"},{0x840021,"OutletID"},{0x840019,"OutletSystemID"},{0x84001b,"InputID"},
        {0x84001d,"OutputID"},{0x840017,"PowerConverterID"},{0x840025,"PowerSummaryID"},
        {0x840061,"Good"},{0x840062,"InternalFailure"},{0x840065,"Overload"},{0x840069,"ShutdownImminent"},
        {0x84006c,"Switchable"},{0x84006e,"Boost"},{0x84006f,"Buck"},{0x840073,"CommunicationLost"},
        {0x85002c,"CapacityMode"},{0x850083,"DesignCapacity"},{0x850067,"FullChargeCapacity"},
        {0x850089,"iDeviceChemistry"},{0x8400fd,"iManufacturer"},{0xffff00f0,"iModel"},
        {0x85008f,"iOEMInformation"},{0xffff00f3,"iPartNumber"},{0x8400fe,"iProduct"},
        {0xffff00f4,"iReferenceNumber"},{0x8400ff,"iSerialNumber"},{0xffff00f1,"iVersion"},
        {0x8500d0,"ACPresent"},{0x850042,"BelowRemainingCapacityLimit"},{0x850044,"Charging"},
        {0x850045,"Discharging"},{0x85004b,"NeedReplacement"},{0x850066,"RemainingCapacity"},
        {0x850029,"RemainingCapacityLimit"},{0x850068,"RunTimeToEmpty"},
        {0xffff0049,"DeepDischargeProtection"},{0xffff0098,"Code"},{0xffff0095,"Country"},
        {0xffff0047,"StartOnBattery"},{0xffff004d,"RemainingCapacityLimitSetting"},
        {0xffff00e5,"System"},{0x840005,"USB"},{0xffff0094,"Mode"}
    };
    static string UsageName(uint u) { string name;return UsageNames.TryGetValue(u,out name)?name:"Usage_"+u.ToString("X8"); }
    sealed class HidGlobals
    {
        public uint Page,Size,Count,Id,Unit;public int Exponent;public long Minimum;
        public HidGlobals Copy(){return (HidGlobals)MemberwiseClone();}
    }
    public static List<UpsField> ParseFeatures(byte[] bytes,out Dictionary<byte,int> reportLengths)
    {
        var fields=new List<UpsField>();var offsets=new Dictionary<byte,int>();var g=new HidGlobals();
        var globals=new Stack<HidGlobals>();var usages=new List<uint>();var path=new List<string>();
        for(int p=0;p<bytes.Length;) {
            int prefix=bytes[p++];if(prefix==254)throw new Exception(L.T("Descrittore HID esteso non supportato."));
            int size=prefix&3;if(size==3)size=4;if(p+size>bytes.Length)throw new Exception(L.T("Descrittore HID incompleto."));
            uint value=0;for(int k=0;k<size;k++)value|=(uint)bytes[p++]<<(8*k);
            int type=(prefix>>2)&3,tag=prefix>>4;
            if(type==1) {
                switch(tag) {
                    case 0:g.Page=value;break;case 1:g.Minimum=Signed(value,size*8);break;
                    case 5:int e=(int)(value&15);g.Exponent=e>=8?e-16:e;break;
                    case 6:g.Unit=value;break;case 7:g.Size=value;break;case 8:g.Id=value;break;case 9:g.Count=value;break;
                    case 10:globals.Push(g.Copy());break;case 11:if(globals.Count==0)throw new Exception(L.T("Stack HID non valido."));g=globals.Pop();break;
                }
            } else if(type==2 && tag==0) usages.Add(size==4?value:(g.Page<<16)|value);
            else if(type==0) {
                if(tag==10) {
                    uint u=usages.Count==0?0:usages[0];string segment=UsageName(u);
                    bool indexed=u==0x84001a || u==0x840020 || u==0x84001e;
                    if(indexed)segment+="["+((value&0x80)!=0?(value&0x7f):1)+"]";
                    path.Add(segment);
                } else if(tag==12) {
                    if(path.Count==0)throw new Exception(L.T("Collezione HID non valida."));path.RemoveAt(path.Count-1);
                } else if(tag==11) {
                    if(g.Id==0 || g.Id>255 || g.Count>4096 || g.Size>32)throw new Exception(L.T("Formato Feature HID non supportato."));
                    byte id=(byte)g.Id;int offset;offsets.TryGetValue(id,out offset);
                    for(int n=0;n<g.Count;n++) {
                        uint u=usages.Count==0?0:usages[Math.Min(n,usages.Count-1)];
                        string name=UsageName(u);string full=string.Join(".",path.ToArray())+"."+name;
                        fields.Add(new UpsField {Report=id,Offset=offset+n*(int)g.Size,Bits=(int)g.Size,Usage=u,Unit=g.Unit,Exponent=g.Exponent,Minimum=g.Minimum,Path=full,Name=name,Constant=(value&0x80)==0});
                    }
                    offsets[id]=checked(offset+(int)(g.Size*g.Count));
                }
                usages.Clear();
            }
        }
        if(path.Count!=0 || globals.Count!=0)throw new Exception(L.T("Descrittore HID non bilanciato."));
        reportLengths=new Dictionary<byte,int>();foreach(var pair in offsets)reportLengths[pair.Key]=1+(pair.Value+7)/8;
        return fields;
    }
    static long Signed(uint value,int bits) { if(bits==0)return 0;if(bits==32)return (int)value;uint sign=1u<<(bits-1);return (value&sign)!=0?(long)value-(1L<<bits):value; }
    static uint Bits(byte[] packet,int offset,int count)
    {
        if(count<1 || count>32 || offset+count>(packet.Length-1)*8)throw new Exception(L.T("Campo fuori dal report USB."));
        uint result=0;for(int i=0;i<count;i++)if((packet[1+(offset+i)/8]&(1<<((offset+i)%8)))!=0)result|=1u<<i;return result;
    }
    public static UpsSnapshot Decode(List<UpsField> fields,Dictionary<byte,byte[]> packets,Func<byte,string> stringLookup)
    {
        var snapshot=new UpsSnapshot {Captured=DateTime.Now};
        foreach(var pair in packets)snapshot.Reports[pair.Key.ToString("X2")]=BitConverter.ToString(pair.Value);
        foreach(var f in fields) {
            byte[] packet;if(!packets.TryGetValue(f.Report,out packet))continue;
            uint rawBits=Bits(packet,f.Offset,f.Bits);long raw=f.Minimum<0?Signed(rawBits,f.Bits):rawBits;
            double number=raw*Math.Pow(10,f.Exponent);
            string unit="";
            // The Eaton descriptor uses HID SI centimetre/gram electrical units;
            // the known voltage/power units require an additional 10^-7 factor.
            if(f.Unit==0x00f0d121) {number=raw*Math.Pow(10,f.Exponent-7);unit="V";}
            else if(f.Unit==0x0000f001) {number=raw*Math.Pow(10,f.Exponent);unit="Hz";}
            else if(f.Unit==0x0000d121) {number=raw*Math.Pow(10,f.Exponent-7);unit=f.Name.IndexOf("Apparent")>=0?"VA":"W";}
            else if(f.Name=="PercentLoad" || f.Name=="RemainingCapacity" || f.Name=="RemainingCapacityLimit" || f.Name=="RemainingCapacityLimitSetting" || f.Name=="FullChargeCapacity" || f.Name=="DesignCapacity")unit="%";
            else if(f.Name=="RunTimeToEmpty" || f.Name.StartsWith("DelayBefore"))unit="s";
            string text=number.ToString("0.###",CultureInfo.InvariantCulture);
            if(f.Name.StartsWith("i",StringComparison.Ordinal) && stringLookup!=null)text=stringLookup((byte)raw);
            snapshot.Values[f.Path]=new UpsValue {Name=f.Name,Path=f.Path,Number=number,Raw=raw,Unit=unit,Text=text};
        }
        return snapshot;
    }
    public sealed class TelemetrySession : IDisposable
    {
        SafeFileHandle file;IntPtr usb;byte iface;byte[] descriptor;
        List<UpsField> fields;Dictionary<byte,int> lengths;Dictionary<byte,byte[]> constants=new Dictionary<byte,byte[]>();
        Dictionary<byte,string> strings=new Dictionary<byte,string>();ushort language=0x0409;
        public TelemetrySession()
        {
            var paths=Find();if(paths.Length==0)throw new Exception(L.T("UPS non collegato via USB."));if(paths.Length!=1)throw new Exception(L.T("Più UPS Eaton collegati."));
            try {
                file=CreateFile(paths[0],0xc0000000,3,IntPtr.Zero,3,0x40000000,IntPtr.Zero);
                if(file.IsInvalid)throw new Win32Exception(Marshal.GetLastWin32Error(),L.T("Apertura UPS"));
                Check(WinUsb_Initialize(file,out usb),L.T("Inizializzazione USB"));UsbInterface info;
                Check(WinUsb_QueryInterfaceSettings(usb,0,out info),L.T("Interfaccia USB"));iface=info.number;
                byte[] config=new byte[4096];uint got;Check(WinUsb_GetDescriptor(usb,2,0,0,config,4096,out got),L.T("Descrittore USB"));
                int size=0;bool selected=false;
                for(int p=0;p+2<=got;) {
                    int len=config[p];if(len<2 || p+len>got)throw new Exception(L.T("Descrittore USB non valido."));
                    if(config[p+1]==4 && len>=9)selected=config[p+2]==iface && config[p+3]==info.alternate;
                    if(selected && config[p+1]==0x21 && len>=9)for(int k=0;k<config[p+5];k++) {int q=p+6+3*k;if(q+3>p+len)throw new Exception(L.T("HID non valido."));if(config[q]==0x22)size=config[q+1]|config[q+2]<<8;}
                    p+=len;
                }
                if(size<1 || size>4096)throw new Exception(L.T("Descrittore HID non disponibile."));descriptor=new byte[size];
                var req=new Setup {type=0x81,request=6,value=0x2200,index=iface,length=(ushort)size};
                Check(WinUsb_ControlTransfer(usb,req,descriptor,(uint)size,out got,IntPtr.Zero),L.T("Lettura descrittore HID"));
                if(got!=size)throw new Exception(L.T("Descrittore HID incompleto."));
                ValidateAlarmDescriptor(descriptor);fields=ParseFeatures(descriptor,out lengths);
                byte[] languages=new byte[255];if(WinUsb_GetDescriptor(usb,3,0,0,languages,255,out got) && got>=4)language=(ushort)(languages[2]|languages[3]<<8);
            } catch {Dispose();throw;}
        }
        byte[] Read(byte report)
        {
            int length=lengths[report];if(length<2 || length>64)throw new Exception(L.T("Report non ammesso per il monitoraggio."));
            byte[] data=new byte[length];uint got;var req=new Setup {type=0xa1,request=1,value=(ushort)(0x300|report),index=iface,length=(ushort)length};
            Check(WinUsb_ControlTransfer(usb,req,data,(uint)length,out got,IntPtr.Zero),L.T("Lettura report ")+report.ToString("X2"));
            if(got!=length || data[0]!=report)throw new Exception(L.T("Report USB incompleto o inatteso."));return data;
        }
        string ReadString(byte index)
        {
            if(index==0)return "";string cached;if(strings.TryGetValue(index,out cached))return cached;
            byte[] data=new byte[255];uint got;Check(WinUsb_GetDescriptor(usb,3,index,language,data,255,out got),L.T("Stringa UPS"));
            if(got<2 || data[1]!=3 || data[0]>got || data[0]<2 || (data[0]&1)!=0)throw new Exception(L.T("Stringa USB non valida."));
            cached=Encoding.Unicode.GetString(data,2,data[0]-2);strings[index]=cached;return cached;
        }
        public UpsSnapshot Poll()
        {
            var packets=new Dictionary<byte,byte[]>();
            foreach(var pair in lengths) {
                byte report=pair.Key;
                // Exclude COPI mailboxes and vendor control fields. No diagnostic mode reads.
                if(report==0xfe || report==0xff || report==0xfd || report==0x22 || report==0x12)continue;
                bool constant=true;foreach(var f in fields)if(f.Report==report && !f.Constant){constant=false;break;}
                byte[] data;if(constant && constants.TryGetValue(report,out data))packets[report]=data;
                else {data=Read(report);packets[report]=data;if(constant)constants[report]=data;}
            }
            var snapshot=Decode(fields,packets,ReadString);
            if(!snapshot.Get("UPS.PowerSummary.PresentStatus.ACPresent").HasValue || !snapshot.Get("UPS.PowerSummary.RemainingCapacity").HasValue)throw new Exception(L.T("Dati essenziali mancanti."));
            return snapshot;
        }
        public string SetAlarm(int value)
        {
            if(value<1 || value>3)throw new ArgumentException(L.T("Comando cicalino non valido."));
            byte report=ValidateAlarmDescriptor(descriptor);byte before=ReadAlarm(usb,iface,report);uint got;
            byte[] data=new byte[]{report,(byte)value};var req=new Setup {type=0x21,request=9,value=(ushort)(0x300|report),index=iface,length=2};
            Check(WinUsb_ControlTransfer(usb,req,data,2,out got,IntPtr.Zero),L.T("Comando cicalino"));if(got!=2)throw new Exception(L.T("Scrittura non confermata: rileggere lo stato."));
            byte after=ReadAlarm(usb,iface,report);
            if(value!=3 && after!=value)throw new Exception(L.T("Impostazione del cicalino non confermata dall’UPS."));
            return before+" -> "+after;
        }
        public void Dispose() {if(usb!=IntPtr.Zero){WinUsb_Free(usb);usb=IntPtr.Zero;}if(file!=null){file.Dispose();file=null;}}
    }
}
