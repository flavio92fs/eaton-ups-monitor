using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

// Only the individually validated AudibleAlarmControl report can be written.
public static partial class EatonUsbProbe
{
    [StructLayout(LayoutKind.Sequential)] struct InterfaceData { public int cbSize; public Guid guid; public int flags; public IntPtr reserved; }
    [StructLayout(LayoutKind.Sequential, Pack=1)] struct Setup { public byte type, request; public ushort value, index, length; }
    [StructLayout(LayoutKind.Sequential, Pack=1)] struct UsbInterface { public byte length, type, number, alternate, endpoints, cls, subcls, protocol, str; }
    [DllImport("setupapi.dll", SetLastError=true)] static extern IntPtr SetupDiGetClassDevs(ref Guid g, IntPtr enumerator, IntPtr hwnd, uint flags);
    [DllImport("setupapi.dll", SetLastError=true)] static extern bool SetupDiEnumDeviceInterfaces(IntPtr set, IntPtr dev, ref Guid g, uint n, ref InterfaceData data);
    [DllImport("setupapi.dll", CharSet=CharSet.Unicode, SetLastError=true)] static extern bool SetupDiGetDeviceInterfaceDetail(IntPtr set, ref InterfaceData data, IntPtr detail, uint size, out uint required, IntPtr dev);
    [DllImport("setupapi.dll")] static extern bool SetupDiDestroyDeviceInfoList(IntPtr set);
    [DllImport("kernel32.dll", CharSet=CharSet.Unicode, SetLastError=true)] static extern SafeFileHandle CreateFile(string path, uint access, uint sharing, IntPtr sa, uint creation, uint flags, IntPtr template);
    [DllImport("winusb.dll", SetLastError=true)] static extern bool WinUsb_Initialize(SafeFileHandle file, out IntPtr handle);
    [DllImport("winusb.dll")] static extern bool WinUsb_Free(IntPtr handle);
    [DllImport("winusb.dll", SetLastError=true)] static extern bool WinUsb_QueryInterfaceSettings(IntPtr handle, byte alt, out UsbInterface descriptor);
    [DllImport("winusb.dll", SetLastError=true)] static extern bool WinUsb_GetDescriptor(IntPtr handle, byte type, byte index, ushort lang, byte[] buffer, uint length, out uint transferred);
    [DllImport("winusb.dll", SetLastError=true)] static extern bool WinUsb_ControlTransfer(IntPtr handle, Setup setup, byte[] buffer, uint length, out uint transferred, IntPtr overlapped);

    static void Check(bool ok, string operation) { if (!ok) throw new Win32Exception(Marshal.GetLastWin32Error(), operation); }
    public static string[] Find()
    {
        Guid guid = new Guid("A5DCBF10-6530-11D2-901F-00C04FB951ED");
        IntPtr set = SetupDiGetClassDevs(ref guid, IntPtr.Zero, IntPtr.Zero, 0x12);
        if (set == new IntPtr(-1)) throw new Win32Exception(Marshal.GetLastWin32Error());
        var result = new List<string>();
        try {
            for (uint n=0;;n++) {
                var data = new InterfaceData(); data.cbSize=Marshal.SizeOf(typeof(InterfaceData));
                if (!SetupDiEnumDeviceInterfaces(set, IntPtr.Zero, ref guid, n, ref data)) {
                    int error=Marshal.GetLastWin32Error(); if (error==259) break; throw new Win32Exception(error);
                }
                uint required;
                SetupDiGetDeviceInterfaceDetail(set, ref data, IntPtr.Zero, 0, out required, IntPtr.Zero);
                IntPtr detail=Marshal.AllocHGlobal((int)required);
                try {
                    Marshal.WriteInt32(detail, IntPtr.Size==8 ? 8 : 6);
                    Check(SetupDiGetDeviceInterfaceDetail(set, ref data, detail, required, out required, IntPtr.Zero), "Interface path");
                    string path=Marshal.PtrToStringUni(IntPtr.Add(detail,4));
                    if (path.IndexOf("vid_0463&pid_ffff",StringComparison.OrdinalIgnoreCase)>=0) result.Add(path);
                } finally { Marshal.FreeHGlobal(detail); }
            }
        } finally { SetupDiDestroyDeviceInfoList(set); }
        return result.ToArray();
    }

    public static string ReadDescriptor(string path) { return Operate(path,-1,true); }
    public static string Alarm(int value)
    {
        if(value!=-1 && value!=1 && value!=2 && value!=3) throw new ArgumentException("Invalid alarm action");
        string[] paths=Find();
        if(paths.Length==0) throw new Exception(L.T("UPS Eaton non collegato via USB."));
        if(paths.Length!=1) throw new Exception(L.T("Più UPS Eaton collegati: selezione ambigua."));
        return Operate(paths[0],value,false);
    }
    static byte ValidateAlarmDescriptor(byte[] descriptor)
    {
        uint page=0,size=0,count=0,id=0;
        var globals=new Stack<uint[]>();
        var collections=new Stack<uint>();
        var usages=new List<uint>();
        var featureBits=new Dictionary<uint,uint>();
        int matches=0; uint alarmId=0;
        for(int p=0;p<descriptor.Length;) {
            byte prefix=descriptor[p++];
            if(prefix==0xfe) throw new Exception("Unsupported HID long item");
            int bytes=prefix&3; if(bytes==3) bytes=4;
            if(p+bytes>descriptor.Length) throw new Exception("Truncated HID item");
            uint v=0; for(int i=0;i<bytes;i++) v|=(uint)descriptor[p++]<<(8*i);
            int type=(prefix>>2)&3,tag=prefix>>4;
            if(type==1) {
                if(tag==0) page=v;
                else if(tag==7) size=v;
                else if(tag==8) id=v;
                else if(tag==9) count=v;
                else if(tag==10) globals.Push(new uint[]{page,size,count,id});
                else if(tag==11) { var g=globals.Pop();page=g[0];size=g[1];count=g[2];id=g[3]; }
            } else if(type==2 && tag==0) usages.Add(bytes==4?v:(page<<16)|v);
            else if(type==0) {
                if(tag==10) collections.Push(usages.Count>0?usages[0]:0);
                else if(tag==12) { if(collections.Count==0) throw new Exception("Unbalanced HID collection"); collections.Pop(); }
                else if(tag==11) {
                    uint offset; featureBits.TryGetValue(id,out offset);
                    if(usages.Contains(0x0084005a)) {
                        if(!collections.Contains(0x00840024) || usages.Count!=1 || size!=8 || count!=1 || offset!=0 || (v&1)!=0)
                            throw new Exception("Unexpected audible alarm field layout");
                        matches++;alarmId=id;
                    }
                    featureBits[id]=checked(offset+checked(size*count));
                }
                usages.Clear();
            }
        }
        if(matches!=1 || alarmId!=0x14 || featureBits[alarmId]!=8)
            throw new Exception("The dedicated alarm report does not match the verified Eaton layout");
        return (byte)alarmId;
    }
    static byte ReadAlarm(IntPtr usb,byte iface,byte report)
    {
        byte[] data=new byte[2]; uint got;
        var request=new Setup {type=0xa1,request=1,value=(ushort)(0x0300|report),index=iface,length=2};
        Check(WinUsb_ControlTransfer(usb,request,data,2,out got,IntPtr.Zero),"Read audible alarm");
        if(got!=2 || data[0]!=report || data[1]<1 || data[1]>3) throw new Exception("Unexpected audible alarm response: "+BitConverter.ToString(data));
        return data[1];
    }
    static string Operate(string path,int value,bool descriptorOnly)
    {
        if (path.IndexOf("vid_0463&pid_ffff",StringComparison.OrdinalIgnoreCase)<0) throw new ArgumentException("Not the selected Eaton USB identity");
        using (SafeFileHandle file=CreateFile(path,0xC0000000,3,IntPtr.Zero,3,0x40000000,IntPtr.Zero)) {
            if (file.IsInvalid) throw new Win32Exception(Marshal.GetLastWin32Error(),"Open Eaton USB");
            IntPtr usb; Check(WinUsb_Initialize(file,out usb),"WinUSB initialize");
            try {
                UsbInterface iface; Check(WinUsb_QueryInterfaceSettings(usb,0,out iface),"USB interface");
                byte[] config=new byte[4096]; uint got;
                Check(WinUsb_GetDescriptor(usb,2,0,0,config,(uint)config.Length,out got),"Configuration descriptor");
                int reportLength=0; bool selected=false;
                for (int p=0;p+2<=got;) {
                    int len=config[p]; if(len<2 || p+len>got) throw new Exception("Malformed USB descriptor");
                    if(config[p+1]==4 && len>=9) selected=config[p+2]==iface.number && config[p+3]==iface.alternate;
                    if(selected && config[p+1]==0x21 && len>=9) {
                        for(int k=0;k<config[p+5];k++) {
                            int q=p+6+3*k; if(q+3>p+len) throw new Exception("Malformed HID descriptor");
                            if(config[q]==0x22) reportLength=config[q+1]|config[q+2]<<8;
                        }
                    }
                    p+=len;
                }
                if(reportLength==0) throw new Exception("No HID report descriptor for this interface");
                byte[] descriptor=new byte[reportLength];
                var request=new Setup {type=0x81,request=6,value=0x2200,index=iface.number,length=(ushort)reportLength};
                Check(WinUsb_ControlTransfer(usb,request,descriptor,(uint)descriptor.Length,out got,IntPtr.Zero),"Read HID report descriptor");
                if(got!=descriptor.Length) throw new Exception("Incomplete HID report descriptor");
                if(descriptorOnly) return "Interface="+iface.number+"; ReportDescriptor="+BitConverter.ToString(descriptor);
                byte alarmReport=ValidateAlarmDescriptor(descriptor);
                byte before=ReadAlarm(usb,iface.number,alarmReport);
                if(value==-1) return "AudibleAlarmControl="+before;
                byte[] outgoing=new byte[]{alarmReport,(byte)value};
                var write=new Setup {type=0x21,request=9,value=(ushort)(0x0300|alarmReport),index=iface.number,length=2};
                Check(WinUsb_ControlTransfer(usb,write,outgoing,2,out got,IntPtr.Zero),"Set audible alarm");
                if(got!=2) throw new Exception("Incomplete audible alarm write; read current state before retrying");
                byte after=ReadAlarm(usb,iface.number,alarmReport);
                return "AudibleAlarmControl: "+before+" -> "+after+"; requested="+value;
            } finally { WinUsb_Free(usb); }
        }
    }
}
