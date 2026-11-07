using System;
using System.Diagnostics;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Win32;

[assembly: AssemblyTitle("Eaton Monitor")]
[assembly: AssemblyProduct("Eaton UPS Monitor")]
[assembly: AssemblyDescription("Unofficial USB monitor for Eaton 5E1600UD")]
[assembly: AssemblyVersion("3.4.0.0")]
[assembly: AssemblyFileVersion("3.4.0.0")]

public static class AppIntegration
{
    public const string WindowTitle="Eaton Monitor · 5E1600UD";
    const string RunKey="Software\\Microsoft\\Windows\\CurrentVersion\\Run";
    const string UninstallKey="Software\\Microsoft\\Windows\\CurrentVersion\\Uninstall\\EatonMonitorPersonal";
    public static readonly int ShowMessage=RegisterWindowMessage("EatonMonitor.Personal.Show.3");
    public static readonly int ExitMessage=RegisterWindowMessage("EatonMonitor.Personal.Exit.3");
    [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern int RegisterWindowMessage(string name);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern IntPtr FindWindow(string cls,string title);
    [DllImport("user32.dll")] static extern bool PostMessage(IntPtr window,int message,IntPtr wp,IntPtr lp);
    public static string InstallDirectory {get{return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"EatonMonitor");}}
    public static string InstalledExe {get{return Path.Combine(InstallDirectory,"EatonMonitor.exe");}}
    public static string StartupCommand {get{return "\""+InstalledExe+"\" --tray";}}
    public static bool Installed {get{return File.Exists(InstalledExe);}}
    public static Icon ApplicationIcon {get{return Icon.ExtractAssociatedIcon(Application.ExecutablePath)??SystemIcons.Application;}}
    public static void Signal(bool exit){IntPtr window=FindWindow(null,WindowTitle);if(window!=IntPtr.Zero)PostMessage(window,exit?ExitMessage:ShowMessage,IntPtr.Zero,IntPtr.Zero);}
    public static bool AutoStart
    {
        get {using(var key=Registry.CurrentUser.OpenSubKey(RunKey))return key!=null && string.Equals(key.GetValue("EatonMonitorPersonal") as string,StartupCommand,StringComparison.OrdinalIgnoreCase);}
    }
    public static void SetAutoStart(bool enabled)
    {
        if(enabled && !Installed)throw new Exception(L.T("Installa prima Eaton Monitor per usare un percorso stabile all’avvio."));
        using(var key=Registry.CurrentUser.CreateSubKey(RunKey)){if(enabled)key.SetValue("EatonMonitorPersonal",StartupCommand,RegistryValueKind.String);else key.DeleteValue("EatonMonitorPersonal",false);}
    }
    public static bool OverlaysEnabled
    {
        get {using(var key=Registry.CurrentUser.OpenSubKey("Software\\EatonMonitorPersonal"))return key==null || Convert.ToInt32(key.GetValue("Overlays",1))!=0;}
        set {using(var key=Registry.CurrentUser.CreateSubKey("Software\\EatonMonitorPersonal"))key.SetValue("Overlays",value?1:0,RegistryValueKind.DWord);}
    }
    public static string ServerAddress
    {
        get {
            using(var key=Registry.CurrentUser.OpenSubKey("Software\\EatonMonitorPersonal"))
                return key==null?"":key.GetValue("ServerAddress","") as string ?? "";
        }
        set {
            using(var key=Registry.CurrentUser.CreateSubKey("Software\\EatonMonitorPersonal"))
                key.SetValue("ServerAddress",value??"",RegistryValueKind.String);
        }
    }
    public static string ReadBeeperKey(Uri address)
    {
        if(address==null)return "";
        try{
            using(var key=Registry.CurrentUser.OpenSubKey("Software\\EatonMonitorPersonal")){
                var encrypted=key==null?null:key.GetValue("BeeperKey") as byte[];
                if(encrypted==null)return "";
                var secret=ProtectedData.Unprotect(encrypted,Encoding.UTF8.GetBytes(address.AbsoluteUri),DataProtectionScope.CurrentUser);
                return Encoding.UTF8.GetString(secret);
            }
        }catch(CryptographicException){return "";}
    }
    public static void SaveBeeperKey(Uri address,string token)
    {
        using(var key=Registry.CurrentUser.CreateSubKey("Software\\EatonMonitorPersonal")){
            if(string.IsNullOrEmpty(token)){key.DeleteValue("BeeperKey",false);return;}
            var encrypted=ProtectedData.Protect(Encoding.UTF8.GetBytes(token),Encoding.UTF8.GetBytes(address.AbsoluteUri),DataProtectionScope.CurrentUser);
            key.SetValue("BeeperKey",encrypted,RegistryValueKind.Binary);
        }
    }
    static void ResourceToFile(string resource,string target)
    {
        using(var source=Assembly.GetExecutingAssembly().GetManifestResourceStream(resource)) {
            if(source==null)throw new Exception(L.T("Risorsa mancante: ")+resource);
            using(var dest=File.Create(target))source.CopyTo(dest);
        }
    }
    static bool SameFile(string a,string b)
    {
        if(!File.Exists(b))return false;
        using(var hash=SHA256.Create())using(var fa=File.OpenRead(a))using(var fb=File.OpenRead(b))return BitConverter.ToString(hash.ComputeHash(fa))==BitConverter.ToString(hash.ComputeHash(fb));
    }
    static void Shortcut(string shortcut,string target)
    {
        object shell=null,link=null;
        try {
            Type type=Type.GetTypeFromProgID("WScript.Shell");shell=Activator.CreateInstance(type);
            link=type.InvokeMember("CreateShortcut",BindingFlags.InvokeMethod,null,shell,new object[]{shortcut});
            Type lt=link.GetType();lt.InvokeMember("TargetPath",BindingFlags.SetProperty,null,link,new object[]{target});
            lt.InvokeMember("WorkingDirectory",BindingFlags.SetProperty,null,link,new object[]{InstallDirectory});
            lt.InvokeMember("IconLocation",BindingFlags.SetProperty,null,link,new object[]{target+",0"});
            lt.InvokeMember("Description",BindingFlags.SetProperty,null,link,new object[]{L.T("Monitor personale per UPS Eaton 5E1600UD")});
            lt.InvokeMember("Save",BindingFlags.InvokeMethod,null,link,null);
        }finally{if(link!=null)Marshal.FinalReleaseComObject(link);if(shell!=null)Marshal.FinalReleaseComObject(shell);}
    }
    public static void Install(bool startup,bool desktop)
    {
        if(Directory.Exists(InstallDirectory) && (File.GetAttributes(InstallDirectory)&FileAttributes.ReparsePoint)!=0)throw new Exception(L.T("La cartella di installazione è un collegamento: scegli una cartella ordinaria ripristinando il percorso predefinito."));
        Directory.CreateDirectory(InstallDirectory);
        string current=Path.GetFullPath(Application.ExecutablePath),target=Path.GetFullPath(InstalledExe);
        if(!string.Equals(current,target,StringComparison.OrdinalIgnoreCase) && !SameFile(current,target)) {
            try{File.Copy(current,target,true);}catch(IOException){throw new Exception(L.T("Chiudi la versione installata con Esci completamente, quindi riprova l’installazione."));}
        }
        ResourceToFile("EatonMonitor.Readme",Path.Combine(InstallDirectory,"README.md"));
        ResourceToFile("EatonMonitor.ReadmeIt",Path.Combine(InstallDirectory,"README.it.md"));
        ResourceToFile("EatonMonitor.License",Path.Combine(InstallDirectory,"LICENSE"));
        string menu=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs),"Eaton Monitor");Directory.CreateDirectory(menu);
        Shortcut(Path.Combine(menu,"Eaton Monitor.lnk"),target);
        if(desktop)Shortcut(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),"Eaton Monitor.lnk"),target);
        using(var key=Registry.CurrentUser.CreateSubKey(UninstallKey)) {
            key.SetValue("DisplayName",L.T("Eaton Monitor (utility personale)"));key.SetValue("DisplayVersion","3.4.0");key.SetValue("Publisher",L.T("Utility personale"));
            key.SetValue("InstallLocation",InstallDirectory);key.SetValue("DisplayIcon",target+",0");key.SetValue("UninstallString","\""+target+"\" --uninstall");
            key.SetValue("NoModify",1,RegistryValueKind.DWord);key.SetValue("NoRepair",1,RegistryValueKind.DWord);
        }
        SetAutoStart(startup);
    }
    public static string SelectDriver(IWin32Window owner)
    {
        using(var dialog=new OpenFileDialog {
            Title=L.T("Seleziona il driver USB originale Eaton"),
            Filter="Eaton (ups_usb.inf)|ups_usb.inf|INF (*.inf)|*.inf",
            CheckFileExists=true
        }) {
            return dialog.ShowDialog(owner)==DialogResult.OK?dialog.FileName:null;
        }
    }
    public static bool RestoreDriver(string inf)
    {
        // The user supplies an official driver; vendor files are never embedded.
        inf=Path.GetFullPath(inf);
        if(!File.Exists(inf) || !string.Equals(Path.GetExtension(inf),".inf",StringComparison.OrdinalIgnoreCase) || inf.IndexOf('"')>=0)throw new Exception(L.T("File del driver non valido."));
        var start=new ProcessStartInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows),"System32","pnputil.exe"),"/add-driver \""+inf+"\" /install") {UseShellExecute=true,Verb="runas",WindowStyle=ProcessWindowStyle.Hidden};
        try {
            using(var process=Process.Start(start)) {
                process.WaitForExit();
                if(process.ExitCode!=0 && process.ExitCode!=3010)
                    throw new Exception(L.T("Windows non ha confermato l’installazione del driver (codice ")+process.ExitCode+").");
                return process.ExitCode==3010;
            }
        } catch(Win32Exception ex) {
            if(ex.NativeErrorCode==1223)throw new Exception(L.T("Installazione del driver annullata nella richiesta di Windows."));
            throw;
        }
    }
    public static string DriverResult(bool restartRequired)
    {
        return L.T(restartRequired
            ?"Riavvia Windows per completare l’installazione del driver."
            :"Pacchetto driver accettato da Windows. Collega o ricollega l’UPS via USB.");
    }
    public static void StartUninstall()
    {
        if(MessageBox.Show(L.T("Rimuovere Eaton Monitor, i suoi collegamenti e l’avvio automatico? Il driver USB Eaton resterà installato."),L.T("Disinstalla Eaton Monitor"),MessageBoxButtons.YesNo,MessageBoxIcon.Question)!=DialogResult.Yes)return;
        string helper=Path.Combine(Path.GetTempPath(),"EatonMonitor-uninstall-"+Guid.NewGuid().ToString("N")+".exe");File.Copy(Application.ExecutablePath,helper);
        Signal(true);Process.Start(new ProcessStartInfo(helper,"--cleanup-uninstall "+Process.GetCurrentProcess().Id){UseShellExecute=true});
    }
    public static void FinishUninstall(int parent)
    {
        string exe=Path.GetFullPath(Application.ExecutablePath),temp=Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar;
        if(!exe.StartsWith(temp,StringComparison.OrdinalIgnoreCase) || !Path.GetFileName(exe).StartsWith("EatonMonitor-uninstall-",StringComparison.Ordinal))throw new Exception(L.T("Helper di disinstallazione non valido."));
        try{using(var p=Process.GetProcessById(parent))p.WaitForExit(10000);}catch(ArgumentException){}
        // The only removal target is the fixed per-user app directory; no caller-supplied paths.
        string root=Path.GetFullPath(InstallDirectory);
        string expected=Path.GetFullPath(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"EatonMonitor"));
        if(!string.Equals(root,expected,StringComparison.OrdinalIgnoreCase))throw new Exception(L.T("Percorso di disinstallazione non valido."));
        if(Directory.Exists(root) && (File.GetAttributes(root)&FileAttributes.ReparsePoint)!=0)throw new Exception(L.T("La cartella di installazione è un collegamento: rimozione automatica annullata."));
        for(int i=0;i<50;i++){try{File.Delete(InstalledExe);break;}catch(IOException){System.Threading.Thread.Sleep(100);}catch(UnauthorizedAccessException){System.Threading.Thread.Sleep(100);}}
        if(File.Exists(InstalledExe))throw new Exception(L.T("Eaton Monitor è ancora aperto. Usa Esci completamente e riprova."));
        SetAutoStart(false);Registry.CurrentUser.DeleteSubKeyTree(UninstallKey,false);
        File.Delete(Path.Combine(root,"LEGGIMI.txt"));File.Delete(Path.Combine(root,"README.md"));File.Delete(Path.Combine(root,"README.it.md"));File.Delete(Path.Combine(root,"LICENSE"));string driver=Path.Combine(root,"driver-backup");
        if(Directory.Exists(driver)){File.Delete(Path.Combine(driver,"ups_usb.inf"));File.Delete(Path.Combine(driver,"ups_usb.cat"));if(Directory.GetFileSystemEntries(driver).Length==0)Directory.Delete(driver,false);}
        if(Directory.Exists(root) && Directory.GetFileSystemEntries(root).Length==0)Directory.Delete(root,false);
        string menu=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs),"Eaton Monitor");File.Delete(Path.Combine(menu,"Eaton Monitor.lnk"));
        if(Directory.Exists(menu) && Directory.GetFileSystemEntries(menu).Length==0)Directory.Delete(menu,false);
        File.Delete(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),"Eaton Monitor.lnk"));
        MessageBox.Show(L.T("Eaton Monitor è stato rimosso. Il driver USB Eaton è rimasto disponibile."),L.T("Disinstallazione completata"));
    }
}

public class SetupForm : Form
{
    const string EatonDownload="https://www.eaton.com/gb/en-gb/catalog/backup-power-ups-surge-it-power-distribution/eaton-ups-companion---emea.html";
    readonly CheckBox startup,desktop,installDriver;
    readonly Button install,chooseDriver;
    readonly TextBox driverPath;
    readonly Label status;
    bool installing;

    public SetupForm()
    {
        Text=L.T("Installa Eaton Monitor");
        ClientSize=new Size(650,430);
        FormBorderStyle=FormBorderStyle.FixedDialog;
        MaximizeBox=false;
        MinimizeBox=false;
        StartPosition=FormStartPosition.CenterParent;
        Font=new Font("Segoe UI",10);
        Icon=AppIntegration.ApplicationIcon;
        Controls.Add(new Label{Text=L.T("Eaton Monitor 3.4 · installazione personale"),Location=new Point(22,20),Size=new Size(606,30),Font=new Font("Segoe UI",15,FontStyle.Bold)});
        Controls.Add(new TextBox{Text=AppIntegration.InstallDirectory,ReadOnly=true,Location=new Point(24,67),Width=602});
        startup=new CheckBox{Text=L.T("Avvia all’accesso a Windows, nella barra dell’orologio"),Checked=AppIntegration.AutoStart,Location=new Point(24,107),AutoSize=true};
        desktop=new CheckBox{Text=L.T("Crea un collegamento sul desktop"),Location=new Point(24,141),AutoSize=true};
        installDriver=new CheckBox{Text=L.T("Installa anche il driver USB Eaton (richiede amministratore)"),Location=new Point(24,187),AutoSize=true};
        driverPath=new TextBox{ReadOnly=true,Enabled=false,Location=new Point(24,225),Width=445};
        chooseDriver=new Button{Text=L.T("Scegli file INF…"),Enabled=false,Location=new Point(481,222),Size=new Size(145,31)};
        chooseDriver.Click+=(s,e)=>{
            string selected=AppIntegration.SelectDriver(this);
            if(selected!=null)driverPath.Text=selected;
        };
        installDriver.CheckedChanged+=(s,e)=>driverPath.Enabled=chooseDriver.Enabled=installDriver.Checked;
        var guide=new LinkLabel{Text=L.T("Apri la pagina ufficiale Eaton per ottenere il driver"),Location=new Point(24,265),AutoSize=true};
        guide.LinkClicked+=(s,e)=>{
            try{Process.Start(new ProcessStartInfo(EatonDownload){UseShellExecute=true});}
            catch(Exception ex){MessageBox.Show(this,ex.Message,Text,MessageBoxButtons.OK,MessageBoxIcon.Error);}
        };
        status=new Label{Text=L.T("Per il driver, seleziona ups_usb.inf dalla copia originale con ups_usb.cat nella stessa cartella."),Location=new Point(24,301),Size=new Size(602,55)};
        install=new Button{Text=L.T("Installa / Aggiorna"),Location=new Point(412,371),Size=new Size(214,36)};
        install.Click+=async(s,e)=>await InstallAsync();
        FormClosing+=(s,e)=>{if(installing)e.Cancel=true;};
        Controls.AddRange(new Control[]{startup,desktop,installDriver,driverPath,chooseDriver,guide,status,install});
    }

    async Task InstallAsync()
    {
        string inf=installDriver.Checked?driverPath.Text:null;
        if(installDriver.Checked && string.IsNullOrWhiteSpace(inf)){
            status.Text=L.T("Seleziona il file INF originale Eaton per continuare.");
            return;
        }
        if(inf!=null && (!string.Equals(Path.GetFileName(inf),"ups_usb.inf",StringComparison.OrdinalIgnoreCase)
                || !File.Exists(Path.Combine(Path.GetDirectoryName(inf),"ups_usb.cat")))){
            status.Text=L.T("Seleziona ups_usb.inf dalla cartella che contiene anche ups_usb.cat.");
            return;
        }

        installing=true;
        startup.Enabled=desktop.Enabled=installDriver.Enabled=install.Enabled=false;
        chooseDriver.Enabled=driverPath.Enabled=false;
        bool appInstalled=false;
        try {
            AppIntegration.Install(startup.Checked,desktop.Checked);
            appInstalled=true;
            if(inf!=null){
                status.Text=L.T("Installazione del driver USB Eaton in corso…");
                bool restartRequired=await Task.Run(()=>AppIntegration.RestoreDriver(inf));
                MessageBox.Show(this,AppIntegration.DriverResult(restartRequired),Text,MessageBoxButtons.OK,MessageBoxIcon.Information);
            }
        }catch(Exception ex){
            status.Text=appInstalled?L.T("Monitor installato. Riprova a installare il driver oppure deseleziona l’opzione."):ex.Message;
            if(appInstalled)MessageBox.Show(this,ex.Message,L.T("Driver USB"),MessageBoxButtons.OK,MessageBoxIcon.Warning);
            return;
        }finally{
            installing=false;
            startup.Enabled=desktop.Enabled=installDriver.Enabled=install.Enabled=true;
            chooseDriver.Enabled=driverPath.Enabled=installDriver.Checked;
        }
        DialogResult=DialogResult.OK;
        Close();
    }
}
