using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Timer = System.Windows.Forms.Timer;

public class PowerOverlay : Form
{
    Label title,details;Button mute;Timer hideTimer;bool preview;public bool AllowClose;
    readonly Action muteAction;
    public PowerOverlay(Action action)
    {
        muteAction=action;Text=L.T("Eaton · alimentazione");Size=new Size(390,260);Icon=AppIntegration.ApplicationIcon;
        FormBorderStyle=FormBorderStyle.FixedSingle;ShowInTaskbar=false;TopMost=true;MaximizeBox=false;MinimizeBox=false;StartPosition=FormStartPosition.Manual;
        BackColor=Color.FromArgb(25,32,44);ForeColor=Color.White;Font=new Font("Segoe UI",11);
        title=new Label{Location=new Point(20,18),Size=new Size(345,54),Font=new Font("Segoe UI",16,FontStyle.Bold)};
        details=new Label{Location=new Point(20,82),Size=new Size(345,70)};
        mute=new Button{Text=L.T("Silenzia allarme attuale"),Location=new Point(20,164),Size=new Size(335,38),FlatStyle=FlatStyle.Flat};
        mute.Click+=(s,e)=>{if(!preview)muteAction();};Controls.Add(title);Controls.Add(details);Controls.Add(mute);
        hideTimer=new Timer{Interval=10000};hideTimer.Tick+=(s,e)=>{hideTimer.Stop();Hide();preview=false;};
        FormClosing+=(s,e)=>{if(!AllowClose){e.Cancel=true;Hide();hideTimer.Stop();preview=false;}};
    }
    protected override bool ShowWithoutActivation {get{return true;}}
    public void Display(Form owner,string kind,UpsSnapshot snapshot,bool show,bool isPreview,bool serverMode=false,bool canMute=true)
    {
        preview=isPreview;
        if(kind=="battery") {title.Text=(preview?L.T("ANTEPRIMA\n"):"")+L.T("CORRENTE ASSENTE");title.ForeColor=Color.FromArgb(255,196,80);mute.Visible=canMute;mute.Enabled=!preview && canMute;details.Text=MonitorForm.BatterySummary(snapshot)+(serverMode?L.T("\nL’UPS collegato al server funziona a batteria."):L.T("\nIl PC è alimentato dalla batteria dell’UPS."));}
        else if(kind=="mains") {title.Text=L.T("CORRENTE RIPRISTINATA");title.ForeColor=Color.FromArgb(98,220,161);mute.Visible=false;details.Text=MonitorForm.BatterySummary(snapshot)+L.T("\nAlimentazione di rete disponibile.");}
        else {title.Text=L.T(serverMode?"UPS NON DISPONIBILE DAL SERVER":"COLLEGAMENTO USB PERSO");title.ForeColor=Color.FromArgb(195,205,220);mute.Visible=false;details.Text=L.T(serverMode?"Lo stato della corrente non è disponibile.\nControlla il server e il collegamento di rete.":"Lo stato della corrente non è disponibile.\nControlla il cavo USB dell’UPS.");}
        if(show && !Visible) {var area=Screen.FromControl(owner).WorkingArea;Location=new Point(area.Right-Width-18,area.Top+18);Show();}
        if(kind=="battery" && !preview)hideTimer.Stop();
        if(show){hideTimer.Stop();if(kind=="mains" || preview)hideTimer.Start();}
    }
    public void HideBattery(){hideTimer.Stop();Hide();preview=false;}
    protected override void Dispose(bool disposing){if(disposing && hideTimer!=null)hideTimer.Dispose();base.Dispose(disposing);}
}

public class MonitorForm : Form
{
    public static readonly Color Background=Color.FromArgb(18,24,34),PanelColor=Color.FromArgb(28,37,51),Muted=Color.FromArgb(167,184,205);
    const string Summary="UPS.PowerSummary.";
    Label state,updated,identity,battery,autonomy,load,power,readings,alarm,message;
    DataGridView table;TextBox events;CheckBox overlays;Button mute,disable,enable,refresh;
    Timer timer;NotifyIcon tray;PowerOverlay overlay;EatonUsbProbe.TelemetrySession session;
    UpsSnapshot latest;bool busy,closing,connected,exitRequested,cleaned,balloonShown,updatingStartup;bool? lastAc;string lastError;int rowCursor,pendingAlarm;
    CheckBox startupCheck;Label installState;
    Uri serverAddress;
    string serverToken;
    Button sourceApply;
    bool CanSetBeeper {get{return serverAddress==null || ServerTelemetry.CanSetBeeper(serverAddress,serverToken);}}
    public MonitorForm(Mutex mutex,bool startInTray,Uri address=null)
    {
        serverAddress=address;
        serverToken=AppIntegration.ReadBeeperKey(address);
        Text=AppIntegration.WindowTitle;Icon=AppIntegration.ApplicationIcon;Size=new Size(1010,790);MinimumSize=new Size(940,730);
        BackColor=Background;ForeColor=Color.White;Font=new Font("Segoe UI",10);StartPosition=FormStartPosition.CenterScreen;
        AutoScaleMode=AutoScaleMode.Dpi;
        var root=new TableLayoutPanel{Dock=DockStyle.Fill,Padding=new Padding(22),ColumnCount=1,RowCount=7};
        root.RowStyles.Add(new RowStyle(SizeType.Absolute,88));root.RowStyles.Add(new RowStyle(SizeType.Absolute,112));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute,60));root.RowStyles.Add(new RowStyle(SizeType.Percent,100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute,58));root.RowStyles.Add(new RowStyle(SizeType.Absolute,45));root.RowStyles.Add(new RowStyle(SizeType.Absolute,37));
        Controls.Add(root);
        var header=new Panel{Dock=DockStyle.Fill};state=LabelAt(L.T("Collegamento all’UPS…"),0,0,700,35,21,true);
        identity=LabelAt(serverAddress==null?L.T("Lettura diretta USB · Companion non richiesto"):serverAddress.Authority,0,42,900,25,10,false);identity.ForeColor=Muted;
        header.Controls.Add(state);header.Controls.Add(identity);root.Controls.Add(header,0,0);
        var cards=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=4};
        for(int i=0;i<4;i++)cards.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,25));
        battery=Card(cards,L.T("BATTERIA"),0);autonomy=Card(cards,L.T("AUTONOMIA STIMATA"),1);autonomy.Font=new Font("Segoe UI",21,FontStyle.Bold);load=Card(cards,L.T("CARICO"),2);power=Card(cards,L.T("POTENZA"),3);root.Controls.Add(cards,0,1);
        readings=new Label{Dock=DockStyle.Fill,Padding=new Padding(0,13,0,0),ForeColor=Muted};root.Controls.Add(readings,0,2);
        var tabs=new TabControl{Dock=DockStyle.Fill};var details=new TabPage(L.T("Dati dell’UPS")){BackColor=Background};var history=new TabPage(L.T("Eventi della sessione")){BackColor=Background};
        tabs.TabPages.Add(details);tabs.TabPages.Add(history);tabs.TabPages.Add(CreateSettings());root.Controls.Add(tabs,0,3);
        table=new DataGridView{Dock=DockStyle.Fill,ReadOnly=true,AllowUserToAddRows=false,AllowUserToDeleteRows=false,RowHeadersVisible=false,AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.Fill,BackgroundColor=PanelColor,BorderStyle=BorderStyle.None,EnableHeadersVisualStyles=false,SelectionMode=DataGridViewSelectionMode.FullRowSelect};
        table.DefaultCellStyle.BackColor=PanelColor;table.DefaultCellStyle.ForeColor=Color.White;table.DefaultCellStyle.SelectionBackColor=Color.FromArgb(48,72,100);table.DefaultCellStyle.SelectionForeColor=Color.White;
        table.ColumnHeadersDefaultCellStyle.BackColor=Color.FromArgb(38,50,68);table.ColumnHeadersDefaultCellStyle.ForeColor=Color.White;table.ColumnHeadersHeight=32;
        table.Columns.Add("name",L.T("Parametro"));table.Columns.Add("value",L.T("Valore"));table.Columns[0].FillWeight=65;table.Columns[1].FillWeight=35;details.Controls.Add(table);
        events=new TextBox{Dock=DockStyle.Fill,Multiline=true,ReadOnly=true,ScrollBars=ScrollBars.Vertical,BackColor=PanelColor,ForeColor=Color.White,BorderStyle=BorderStyle.None};history.Controls.Add(events);
        var buttons=new FlowLayoutPanel{Dock=DockStyle.Fill,Padding=new Padding(0,10,0,0)};
        mute=ButtonFor(L.T("Silenzia allarme attuale"),205,async()=>await SetAlarm(3));disable=ButtonFor(L.T("Disabilita cicalino"),170,async()=>await SetAlarm(1));enable=ButtonFor(L.T("Riabilita cicalino"),170,async()=>await SetAlarm(2));refresh=ButtonFor(L.T("Aggiorna"),105,async()=>await Poll());
        buttons.Controls.AddRange(new Control[]{mute,disable,enable,refresh});root.Controls.Add(buttons,0,4);
        var options=new FlowLayoutPanel{Dock=DockStyle.Fill};overlays=new CheckBox{Text=L.T("Overlay per blackout e ripristino"),Checked=AppIntegration.OverlaysEnabled,AutoSize=true,Padding=new Padding(0,8,16,0)};
        overlays.CheckedChanged+=(s,e)=>{try{AppIntegration.OverlaysEnabled=overlays.Checked;}catch(Exception ex){Log(L.T("Preferenza non salvata: ")+ex.Message);}if(!overlays.Checked)overlay.HideBattery();else if(connected && latest!=null && !latest.AcPresent)overlay.Display(this,"battery",latest,true,false,serverAddress!=null,CanSetBeeper);};
        options.Controls.Add(overlays);options.Controls.Add(ButtonFor(L.T("Anteprima overlay"),175,()=>{bool real=connected && latest!=null && !latest.AcPresent;overlay.Display(this,"battery",latest,true,!real,serverAddress!=null,CanSetBeeper);return Task.FromResult(0);}));
        options.Controls.Add(ButtonFor(L.T("Esci completamente"),185,()=>{RequestExit();return Task.FromResult(0);}));root.Controls.Add(options,0,5);
        var footer=new Panel{Dock=DockStyle.Fill};updated=LabelAt(L.T("In attesa di dati"),0,3,280,25,9,false);updated.ForeColor=Muted;
        alarm=LabelAt("",280,3,220,25,9,false);message=LabelAt(L.T("Nessuno spegnimento automatico del PC configurato"),505,3,460,25,9,false);message.ForeColor=Muted;
        footer.Controls.Add(updated);footer.Controls.Add(alarm);footer.Controls.Add(message);root.Controls.Add(footer,0,6);
        overlay=new PowerOverlay(()=>{var ignored=SetAlarm(3);});
        tray=new NotifyIcon{Icon=Icon,Text="Eaton Monitor"};var menu=new ContextMenuStrip();menu.Items.Add(L.T("Apri monitor"),null,(s,e)=>Restore());menu.Items.Add(L.T("Esci completamente"),null,(s,e)=>RequestExit());tray.ContextMenuStrip=menu;tray.DoubleClick+=(s,e)=>Restore();
        Resize+=(s,e)=>{if(WindowState==FormWindowState.Minimized)HideToTray(false);};
        timer=new Timer{Interval=2000};timer.Tick+=async(s,e)=>await Poll();
        Shown+=async(s,e)=>{tray.Visible=true;if(startInTray)HideToTray(false);timer.Start();await Poll();};
        FormClosing+=(s,e)=>{
            if(!exitRequested && e.CloseReason==CloseReason.UserClosing){e.Cancel=true;HideToTray(true);return;}
            if(e.CloseReason==CloseReason.WindowsShutDown){Cleanup(false);return;}
            if(busy){closing=true;e.Cancel=true;timer.Stop();message.Text=L.T("Chiusura in corso…");}else Cleanup(true);
        };
        EnabledActions(false);
        if(startInTray){ShowInTaskbar=false;WindowState=FormWindowState.Minimized;}
    }
    TabPage CreateSettings()
    {
        var page=new TabPage(L.T("Impostazioni")){BackColor=PanelColor,AutoScroll=true};
        var heading=LabelAt(L.T("Eaton Monitor 3.4 · utility personale"),22,18,810,35,16,true);page.Controls.Add(heading);
        page.Controls.Add(LabelAt(L.T("Sorgente dati"),24,65,145,25,10,true));
        var source=new ComboBox{DropDownStyle=ComboBoxStyle.DropDownList,Location=new Point(170,61),Width=240};
        source.Items.AddRange(new object[]{L.T("USB diretto"),L.T("Server Ubuntu")});
        source.SelectedIndex=serverAddress==null?0:1;
        var address=new TextBox{Location=new Point(24,105),Width=545,Text=serverAddress==null?"":serverAddress.AbsoluteUri,Enabled=serverAddress!=null};
        address.AccessibleName=L.T("Indirizzo del server UPS");
        var token=new TextBox{Location=new Point(188,143),Width=381,Text=serverToken,UseSystemPasswordChar=true,MaxLength=64,Enabled=serverAddress!=null};
        token.AccessibleName=L.T("Chiave cicalino");
        source.SelectedIndexChanged+=(s,e)=>address.Enabled=token.Enabled=source.SelectedIndex==1;
        address.TextChanged+=(s,e)=>{if(token.Text==serverToken && !string.IsNullOrEmpty(serverToken))token.Clear();};
        sourceApply=ButtonFor(L.T("Salva connessione"),190,async()=>{
            try{
                Uri next=source.SelectedIndex==1?ServerTelemetry.ParseAddress(address.Text):null;
                string nextToken=next==null?"":token.Text.Trim();
                if(nextToken.Length!=0 && !ServerTelemetry.CanSetBeeper(next,nextToken))
                    throw new ArgumentException(L.T("Per i comandi serve un indirizzo IPv4 Tailscale e la chiave cicalino."));
                if(next!=null)AppIntegration.SaveBeeperKey(next,nextToken);
                AppIntegration.ServerAddress=next==null?"":next.AbsoluteUri;
                if(Equals(next,serverAddress) && nextToken==serverToken)return;
                if(session!=null){session.Dispose();session=null;}
                serverAddress=next;
                serverToken=nextToken;
                connected=false;
                latest=null;
                lastAc=null;
                lastError=null;
                pendingAlarm=0;
                overlay.HideBattery();
                EnabledActions(false);
                await Poll();
            }catch(Exception ex){MessageBox.Show(this,ex.Message,L.T("Sorgente dati"));}
        });
        sourceApply.Location=new Point(584,100);
        page.Controls.AddRange(new Control[]{source,address,sourceApply,token,LabelAt(L.T("Chiave cicalino"),24,146,160,25,10,false)});
        page.Controls.Add(LabelAt(L.T("Usa l’indirizzo della dashboard su LAN o Tailscale. La chiave abilita i comandi via Tailscale."),24,178,810,34,9,false));
        installState=LabelAt("",24,215,810,52,10,false);installState.ForeColor=Muted;page.Controls.Add(installState);
        var install=ButtonFor(L.T("Installa per questo utente"),260,()=>{using(var setup=new SetupForm())setup.ShowDialog(this);RefreshSettings();return Task.FromResult(0);});install.Location=new Point(24,269);page.Controls.Add(install);
        startupCheck=new CheckBox{Text=L.T("Avvia con Windows, nella barra dell’orologio"),Location=new Point(24,321),AutoSize=true};
        startupCheck.CheckedChanged+=(s,e)=>{if(updatingStartup)return;try{AppIntegration.SetAutoStart(startupCheck.Checked);Log(startupCheck.Checked?L.T("Avvio con Windows attivato."):L.T("Avvio con Windows disattivato."));}catch(Exception ex){MessageBox.Show(this,ex.Message,L.T("Avvio automatico"));RefreshSettings();}};page.Controls.Add(startupCheck);
        var driver=ButtonFor(L.T("Ripristina driver USB Eaton…"),270,async()=>{
            try{
                string inf=AppIntegration.SelectDriver(this);
                if(inf==null)return;
                bool restartRequired=await Task.Run(()=>AppIntegration.RestoreDriver(inf));
                MessageBox.Show(this,AppIntegration.DriverResult(restartRequired),L.T("Driver USB"));}
            catch(Exception ex){MessageBox.Show(this,ex.Message,L.T("Driver USB"));}
        });driver.Location=new Point(24,365);page.Controls.Add(driver);
        var note=LabelAt(L.T("Il driver originale non è incluso. Prima di disinstallare Companion esporta il driver\ncon pnputil (vedi README); per ripristinarlo seleziona il file INF originale.\nSolo l’installazione del driver richiede l’autorizzazione di amministratore."),24,415,810,76,10,false);note.ForeColor=Muted;page.Controls.Add(note);
        var languageLabel=LabelAt(L.T("Lingua / Language"),330,276,180,25,10,false);page.Controls.Add(languageLabel);
        var language=new ComboBox{DropDownStyle=ComboBoxStyle.DropDownList,Location=new Point(520,273),Width=185};language.Items.AddRange(new object[]{"Italiano","English"});language.SelectedIndex=L.IsItalian?0:1;
        language.SelectedIndexChanged+=(s,e)=>{try{L.Save(language.SelectedIndex==0?"it":"en");MessageBox.Show(this,L.T("Lingua salvata. Riavvia l’app con Esci completamente per applicarla."),"Eaton Monitor");}catch(Exception ex){MessageBox.Show(this,ex.Message,"Eaton Monitor");}};page.Controls.Add(language);
        RefreshSettings();return page;
    }
    void RefreshSettings()
    {
        updatingStartup=true;
        try{startupCheck.Enabled=AppIntegration.Installed;startupCheck.Checked=AppIntegration.AutoStart;installState.Text=AppIntegration.Installed?L.T("Installato in: ")+AppIntegration.InstallDirectory:L.T("Versione portatile. Installa l’app per attivare l’avvio automatico.\nLa X mantiene il monitor nell’area di notifica; Esci completamente lo termina.");}
        finally{updatingStartup=false;}
    }
    void HideToTray(bool notify)
    {
        Hide();ShowInTaskbar=false;
        if(notify && !balloonShown){balloonShown=true;tray.ShowBalloonTip(3500,L.T("Eaton Monitor è ancora attivo"),L.T("Apri l’icona vicino all’orologio. Per chiudere tutto: tasto destro → Esci completamente."),ToolTipIcon.Info);}
    }
    void RequestExit(){exitRequested=true;Close();}
    protected override void WndProc(ref Message m)
    {
        if(m.Msg==AppIntegration.ShowMessage){Restore();return;}
        if(m.Msg==AppIntegration.ExitMessage){RequestExit();return;}
        base.WndProc(ref m);
    }
    static Label LabelAt(string text,int x,int y,int width,int height,float size,bool bold) {return new Label{Text=text,Location=new Point(x,y),Size=new Size(width,height),Font=new Font("Segoe UI",size,bold?FontStyle.Bold:FontStyle.Regular)};}
    static Label Card(TableLayoutPanel parent,string title,int col)
    {
        var panel=new Panel{Dock=DockStyle.Fill,BackColor=PanelColor,Margin=new Padding(0,0,col==3?0:12,0),Padding=new Padding(14)};
        var label=new Label{Text=title,ForeColor=Muted,Dock=DockStyle.Top,Height=26,Font=new Font("Segoe UI",9,FontStyle.Bold)};
        var number=new Label{Text="—",Dock=DockStyle.Fill,Font=new Font("Segoe UI",27,FontStyle.Bold)};panel.Controls.Add(number);panel.Controls.Add(label);parent.Controls.Add(panel,col,0);return number;
    }
    static Button ButtonFor(string text,int width,Func<Task> action)
    {
        var b=new Button{Text=text,Width=width,Height=36,FlatStyle=FlatStyle.Flat,BackColor=PanelColor,ForeColor=Color.White,Margin=new Padding(0,0,10,0)};b.Click+=async(s,e)=>await action();return b;
    }
    void Restore(){ShowInTaskbar=true;Show();WindowState=FormWindowState.Normal;Activate();}
    void Log(string text){events.AppendText(DateTime.Now.ToString("HH:mm:ss")+"  "+text+Environment.NewLine);if(events.TextLength>20000)events.Text=events.Text.Substring(events.TextLength-15000);}
    void EnabledActions(bool enabled){mute.Enabled=disable.Enabled=enable.Enabled=enabled && connected && CanSetBeeper;refresh.Enabled=enabled;sourceApply.Enabled=enabled;}
    async Task Poll()
    {
        if(busy || closing)return;busy=true;sourceApply.Enabled=false;
        Uri address=serverAddress;
        try {latest=await Task.Run(()=>{if(address!=null)return ServerTelemetry.Poll(address);if(session==null)session=new EatonUsbProbe.TelemetrySession();return session.Poll();});Render(latest);}
        catch(Exception ex){Disconnected(ex.Message);}
        finally {busy=false;EnabledActions(true);if(closing)Close();}
        if(pendingAlarm!=0 && !closing && connected){int next=pendingAlarm;pendingAlarm=0;await SetAlarm(next);}
    }
    async Task SetAlarm(int value)
    {
        if(closing || !connected || !CanSetBeeper)return;
        if(busy){pendingAlarm=value;Log(L.T("Comando cicalino in attesa della lettura USB."));return;}
        busy=true;EnabledActions(false);
        Uri address=serverAddress;
        string token=serverToken;
        try {
            string result=await Task.Run(()=>address==null?session.SetAlarm(value):ServerTelemetry.SetBeeper(address,token,value));
            latest=await Task.Run(()=>address==null?session.Poll():ServerTelemetry.Poll(address));Render(latest);
            message.Text=result;
            Log((value==1?L.T("Cicalino disabilitato"):value==2?L.T("Cicalino riabilitato"):L.T("Silenziamento temporaneo richiesto"))+" ("+result+")");
        }catch(Exception ex){
            Log(L.T("Comando non concluso: ")+ex.Message);
            if(address==null)Disconnected(ex.Message);
            else message.Text=ex.Message;
        }
        finally{busy=false;EnabledActions(true);if(closing)Close();}
        if(pendingAlarm!=0 && !closing && connected){int next=pendingAlarm;pendingAlarm=0;await SetAlarm(next);}
    }
    void Disconnected(string error)
    {
        if(session!=null){session.Dispose();session=null;}pendingAlarm=0;bool wasConnected=connected;connected=false;
        bool remote=serverAddress!=null;
        state.Text=L.T(remote?"UPS non disponibile dal server":"UPS non disponibile via USB");state.ForeColor=Muted;battery.Text=autonomy.Text=load.Text=power.Text="—";
        readings.Text=L.T(remote?"Controlla il server Ubuntu, il collegamento USB al server e la rete LAN o Tailscale.":"Il collegamento perso non indica che sia mancata la corrente. Controlla il cavo USB e lascia Companion chiuso.");
        updated.Text=L.T("Dati non disponibili");alarm.Text=L.T("Cicalino: stato sconosciuto");
        identity.Text=error;
        table.Rows.Clear();
        tray.Text="Eaton · "+L.T("Dati non disponibili");
        if(error!=lastError){Log(L.T(remote?"Lettura dal server interrotta: ":"Lettura USB interrotta: ")+error);lastError=error;}
        if(wasConnected && overlays.Checked)overlay.Display(this,"usb",null,true,false,remote);
    }
    static string Format(double? value,string unit){return value.HasValue?value.Value.ToString("0.#",CultureInfo.CurrentCulture)+unit:"—";}
    public static string Runtime(double? seconds)
    {
        if(!seconds.HasValue || seconds.Value<0 || seconds.Value>=65535)return L.T("non disponibile");
        return ((int)seconds.Value/60)+" min "+((int)seconds.Value%60).ToString("00")+" s";
    }
    public static string BatterySummary(UpsSnapshot s){return s==null?L.T("Dati di esempio: nessun comando inviato all’UPS."):L.T("Batteria ")+Format(s.Get(Summary+"RemainingCapacity"),"%")+L.T(" · autonomia stimata ")+Runtime(s.Get(Summary+"RunTimeToEmpty"));}
    static string Flag(double? value){return !value.HasValue?L.T("non disponibile"):value.Value==0?"No":L.T("Sì");}
    static string AlarmText(double? value){return value==1?L.T("disabilitato"):value==2?L.T("abilitato"):value==3?L.T("silenziato temporaneamente"):L.T("sconosciuto");}
    void Row(string name,string value){if(rowCursor>=table.Rows.Count)table.Rows.Add(name,value??L.T("non disponibile"));else {table.Rows[rowCursor].Cells[0].Value=name;table.Rows[rowCursor].Cells[1].Value=value??L.T("non disponibile");}rowCursor++;}
    void Render(UpsSnapshot s)
    {
        bool reconnect=!connected;connected=true;lastError=null;bool ac=s.AcPresent;
        state.Text=ac?L.T("Rete elettrica presente"):L.T("CORRENTE ASSENTE · UPS a batteria");state.ForeColor=ac?Color.FromArgb(98,220,161):Color.FromArgb(255,196,80);
        string product=s.GetText(Summary+"iProduct")??"Eaton";
        string model=s.GetText(Summary+"iModel");
        string firmware=s.GetText(Summary+"iVersion");
        identity.Text=product+(string.IsNullOrEmpty(model)?"":" · "+model)+(string.IsNullOrEmpty(firmware)?"":" · Firmware "+firmware)+(serverAddress==null?L.T(" · Collegamento USB diretto"):" · "+L.T("Server Ubuntu")+" "+serverAddress.Authority);
        battery.Text=Format(s.Get(Summary+"RemainingCapacity"),"%");autonomy.Text=Runtime(s.Get(Summary+"RunTimeToEmpty"));load.Text=Format(s.Get(Summary+"PercentLoad"),"%");power.Text=Format(s.Get("UPS.PowerConverter.Output.ActivePower")," W");
        readings.Text=L.T("Ingresso ")+Format(s.Get("UPS.PowerConverter.Input[1].Voltage")," V")+L.T("     Uscita ")+Format(s.Get("UPS.PowerConverter.Output.Voltage")," V")+L.T("     Batteria ")+Format(s.Get(Summary+"Voltage")," V")+L.T("     Frequenza uscita ")+Format(s.Get("UPS.PowerConverter.Output.Frequency")," Hz");
        updated.Text=L.T("Aggiornato alle ")+s.Captured.ToString("HH:mm:ss");alarm.Text=L.T("Cicalino: ")+AlarmText(s.Get(Summary+"AudibleAlarmControl"));
        string tip="Eaton · "+L.T(ac?"Rete":"A batteria")+" · "+battery.Text+" · "+power.Text;
        tray.Text=tip.Length<=63?tip:tip.Substring(0,63);
        rowCursor=0;
        Row(L.T("Alimentazione"),ac?L.T("Rete elettrica"):L.T("Batteria UPS"));Row(L.T("Batteria"),Format(s.Get(Summary+"RemainingCapacity"),"%"));Row(L.T("Autonomia stimata"),Runtime(s.Get(Summary+"RunTimeToEmpty")));
        Row(L.T("In carica"),Flag(s.Get(Summary+"PresentStatus.Charging")));Row(L.T("In scarica"),Flag(s.Get(Summary+"PresentStatus.Discharging")));
        Row(L.T("Carico"),Format(s.Get(Summary+"PercentLoad"),"%"));Row(L.T("Potenza attiva"),Format(s.Get("UPS.PowerConverter.Output.ActivePower")," W"));Row(L.T("Potenza apparente"),Format(s.Get("UPS.PowerConverter.Output.ApparentPower")," VA"));
        Row(L.T("Potenza nominale"),Format(s.Get("UPS.Flow[4].ConfigActivePower")," W")+" / "+Format(s.Get("UPS.Flow[4].ConfigApparentPower")," VA"));
        Row(L.T("Tensione ingresso"),Format(s.Get("UPS.PowerConverter.Input[1].Voltage")," V"));Row(L.T("Tensione uscita"),Format(s.Get("UPS.PowerConverter.Output.Voltage")," V"));Row(L.T("Tensione batteria"),Format(s.Get(Summary+"Voltage")," V"));Row(L.T("Frequenza uscita"),Format(s.Get("UPS.PowerConverter.Output.Frequency")," Hz"));
        Row(L.T("Cicalino"),AlarmText(s.Get(Summary+"AudibleAlarmControl")));Row(L.T("Batteria da sostituire"),Flag(s.Get(Summary+"PresentStatus.NeedReplacement")));Row(L.T("Batteria sotto soglia"),Flag(s.Get(Summary+"PresentStatus.BelowRemainingCapacityLimit")));
        Row(L.T("Sovraccarico"),Flag(s.Get(Summary+"PresentStatus.Overload")));Row(L.T("Guasto interno"),Flag(s.Get(Summary+"PresentStatus.InternalFailure")));Row(L.T("Spegnimento UPS imminente"),Flag(s.Get(Summary+"PresentStatus.ShutdownImminent")));
        Row(L.T("AVR Boost attivo"),Flag(s.Get("UPS.PowerConverter.Input[1].PresentStatus.Boost")));Row(L.T("AVR Buck attivo"),Flag(s.Get("UPS.PowerConverter.Input[1].PresentStatus.Buck")));
        Row(L.T("Modello"),string.IsNullOrEmpty(model)?product:product+" / "+model);Row("Firmware",firmware);Row(L.T("Codice prodotto"),s.GetText(Summary+"iPartNumber"));Row(L.T("Riferimento"),s.GetText(Summary+"iReferenceNumber"));Row(L.T("Numero di serie"),s.GetText(Summary+"iSerialNumber"));Row(L.T("Chimica batteria"),s.GetText(Summary+"iDeviceChemistry"));
        if(reconnect)Log(L.T(serverAddress==null?"Collegamento USB disponibile.":"Collegamento al server disponibile."));
        bool transition=!lastAc.HasValue || lastAc.Value!=ac;
        if(transition)Log(ac?L.T("Alimentazione di rete presente."):L.T("Mancanza rete: funzionamento a batteria."));
        if(overlays.Checked) {
            if(!ac)overlay.Display(this,"battery",s,transition || reconnect,false,serverAddress!=null,CanSetBeeper);
            else if(lastAc.HasValue && !lastAc.Value)overlay.Display(this,"mains",s,true,false);
            else if(reconnect)overlay.HideBattery();
        }
        lastAc=ac;
    }
    void Cleanup(bool releaseUsb){if(cleaned)return;cleaned=true;timer.Stop();timer.Dispose();if(releaseUsb && session!=null){session.Dispose();session=null;}overlay.AllowClose=true;overlay.Dispose();tray.Visible=false;tray.Dispose();}
    protected override void Dispose(bool disposing){base.Dispose(disposing);}
    [STAThread] public static void Main(string[] args)
    {
        L.Initialize(args);Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
        try {
            if(Array.IndexOf(args,"--uninstall")>=0){AppIntegration.StartUninstall();return;}
            if(args.Length==2 && args[0]=="--cleanup-uninstall"){AppIntegration.FinishUninstall(int.Parse(args[1],CultureInfo.InvariantCulture));return;}
            if(Array.IndexOf(args,"--setup")>=0 || string.Equals(Path.GetFileName(Application.ExecutablePath),"EatonMonitorSetup.exe",StringComparison.OrdinalIgnoreCase)){
                Application.Run(new SetupForm());
                return;
            }
        }catch(Exception ex){MessageBox.Show(ex.Message,"Eaton Monitor");return;}
        Uri address=null;
        try{
            int option=Array.IndexOf(args,"--server");
            string saved=option<0?AppIntegration.ServerAddress:option+1<args.Length?args[option+1]:"";
            if(option>=0 || !string.IsNullOrWhiteSpace(saved))address=ServerTelemetry.ParseAddress(saved);
        }catch(Exception ex){MessageBox.Show(ex.Message,"Eaton Monitor");return;}
        bool created;using(var mutex=new Mutex(true,"Local\\EatonMonitor5E1600UD",out created)) {
            if(!created){AppIntegration.Signal(false);return;}
            Application.Run(new MonitorForm(mutex,Array.IndexOf(args,"--tray")>=0,address));
        }
    }
}
