using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Win32;

[assembly: AssemblyTitle("Eaton Monitor")]
[assembly: AssemblyProduct("Eaton UPS Monitor")]
[assembly: AssemblyDescription("Unofficial USB monitor for Eaton 5E1600UD")]
[assembly: AssemblyVersion("3.1.0.0")]
[assembly: AssemblyFileVersion("3.1.0.0")]

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
            key.SetValue("DisplayName",L.T("Eaton Monitor (utility personale)"));key.SetValue("DisplayVersion","3.1");key.SetValue("Publisher",L.T("Utility personale"));
            key.SetValue("InstallLocation",InstallDirectory);key.SetValue("DisplayIcon",target+",0");key.SetValue("UninstallString","\""+target+"\" --uninstall");
            key.SetValue("NoModify",1,RegistryValueKind.DWord);key.SetValue("NoRepair",1,RegistryValueKind.DWord);
        }
        SetAutoStart(startup);
    }
    public static void RestoreDriver(string inf)
    {
        // The user supplies an official driver; vendor files are never embedded.
        inf=Path.GetFullPath(inf);
        if(!File.Exists(inf) || !string.Equals(Path.GetExtension(inf),".inf",StringComparison.OrdinalIgnoreCase) || inf.IndexOf('"')>=0)throw new Exception(L.T("File del driver non valido."));
        var start=new ProcessStartInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows),"System32","pnputil.exe"),"/add-driver \""+inf+"\" /install") {UseShellExecute=true,Verb="runas",WindowStyle=ProcessWindowStyle.Hidden};
        using(var process=Process.Start(start)) {
            if(!process.WaitForExit(30000))throw new Exception(L.T("La richiesta di installazione del driver è ancora in corso. Verifica l’autorizzazione di Windows."));
            if(process.ExitCode!=0 && process.ExitCode!=3010)throw new Exception(L.T("Windows non ha confermato l’installazione del driver (codice ")+process.ExitCode+").");
        }
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
    CheckBox startup,desktop;Button install;Label status;
    public SetupForm()
    {
        Text=L.T("Installa Eaton Monitor");ClientSize=new Size(610,350);FormBorderStyle=FormBorderStyle.FixedDialog;MaximizeBox=false;MinimizeBox=false;StartPosition=FormStartPosition.CenterParent;Font=new Font("Segoe UI",10);Icon=AppIntegration.ApplicationIcon;
        Controls.Add(new Label{Text=L.T("Eaton Monitor 3.1 · installazione personale"),Location=new Point(22,20),Size=new Size(565,30),Font=new Font("Segoe UI",15,FontStyle.Bold)});
        Controls.Add(new Label{Text=L.T("Non richiede amministratore. Il driver USB attuale non viene cambiato."),Location=new Point(24,61),Size=new Size(560,25)});
        Controls.Add(new TextBox{Text=AppIntegration.InstallDirectory,ReadOnly=true,Location=new Point(24,100),Width=560});
        startup=new CheckBox{Text=L.T("Avvia all’accesso a Windows, nella barra dell’orologio"),Checked=AppIntegration.AutoStart,Location=new Point(24,146),AutoSize=true};
        desktop=new CheckBox{Text=L.T("Crea un collegamento sul desktop"),Location=new Point(24,181),AutoSize=true};Controls.Add(startup);Controls.Add(desktop);
        status=new Label{Text=L.T("Crea un collegamento nel menu Start e una voce tra le app installate."),Location=new Point(24,227),Size=new Size(560,45)};Controls.Add(status);
        install=new Button{Text=L.T("Installa / Aggiorna"),Location=new Point(370,290),Size=new Size(214,36)};install.Click+=(s,e)=>{
            install.Enabled=false;try{AppIntegration.Install(startup.Checked,desktop.Checked);status.Text=L.T("Installazione completata. Puoi aprirlo dal menu Start.");DialogResult=DialogResult.OK;Close();}
            catch(Exception ex){status.Text=ex.Message;install.Enabled=true;}
        };Controls.Add(install);
    }
}
