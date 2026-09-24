// Runs a short WinForms shell check against a built application assembly.
// No keyboard/mouse events are generated and startup registration is unchanged.
// Usage: AppShellChecks.exe <Dafeiyu.exe> <isolated-output-directory>
using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using System.Drawing;

static class AppShellChecks {
    [StructLayout(LayoutKind.Sequential)]struct Device{public ushort Page,Usage;public uint Flags;public IntPtr Target;}
    [DllImport("user32.dll",SetLastError=true)]static extern uint GetRegisteredRawInputDevices([Out]Device[] devices,ref uint count,uint size);
    [DllImport("user32.dll")]static extern bool PostMessage(IntPtr window,int message,IntPtr w,IntPtr l);
    [DllImport("user32.dll",EntryPoint="GetWindowLongW")]static extern int GetWindowStyle(IntPtr window,int index);
    static Form pet;
    static Type petType,settingsType;
    static NotifyIcon tray;
    static Timer steps;
    static string settingsPath,output;
    static Exception failure;
    static int phase,checks;
    const BindingFlags Members=BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public;
    static object Field(string name){return petType.GetField(name,Members).GetValue(pet);}
    static void PutField(string name,object value){petType.GetField(name,Members).SetValue(pet,value);}
    static void Call(string name,params object[] args){petType.GetMethod(name,Members).Invoke(pet,args);}
    static bool PassesMouse{get{return (GetWindowStyle(pet.Handle,-20)&0x20)!=0;}}
    static void CheckHover(){
        object settings=Field("settings");
        Point inside=new Point(pet.Left+pet.ClientSize.Width/2,pet.Top+pet.ClientSize.Height/2);
        Point outside=new Point(pet.Left-10,pet.Top-10);
        var opacity=(ToolStripMenuItem)Field("opacityItem");
        foreach(ToolStripMenuItem item in opacity.DropDownItems)if((int)item.Tag==50)item.PerformClick();
        Check((int)Saved("OpacityPercent")==50,"Opacity menu did not persist 50 percent");
        Call("RefreshMenu");
        foreach(ToolStripMenuItem item in opacity.DropDownItems)Check(item.Checked==((int)item.Tag==50),"Opacity menu checkmark does not match its value");
        Call("Tick");
        Check((byte)Field("renderedOpacity")>=127&&(byte)Field("renderedOpacity")<=128,"Opacity was not applied to the rendered frame");
        Call("UpdateHover",inside);
        Check(!(bool)Field("hoverHidden")&&!PassesMouse,"Disabled hover hiding changed the window");
        ((ToolStripMenuItem)Field("hoverHideItem")).PerformClick();
        Check((bool)Saved("HideOnHover"),"Hover-hiding menu did not persist its option");
        Call("RefreshMenu");Check(((ToolStripMenuItem)Field("hoverHideItem")).Checked,"Hover-hiding menu checkmark was not enabled");
        Call("UpdateHover",inside);
        Check((bool)Field("hoverHidden")&&PassesMouse&&pet.Visible&&tray.Visible,"Hover entry did not set pass-through while retaining the shell");
        Check(!pet.Capture,"Hover hiding captured the mouse");
        for(int repeat=0;repeat<6;repeat++)Call("UpdateHover",inside);
        Check((bool)Field("hoverHidden")&&PassesMouse,"Repeated hover checks flickered visible");
        Call("UpdateHover",outside);
        Check(!(bool)Field("hoverHidden")&&!PassesMouse,"Hover exit did not restore window hit testing");
        PutField("dragging",true);Call("UpdateHover",inside);
        Check(!(bool)Field("hoverHidden")&&!PassesMouse,"Dragging was hidden by hover logic");
        PutField("dragging",false);
        var menu=(ContextMenuStrip)Field("menu");
        try{
            menu.Show(outside);Call("UpdateHover",inside);
            Check(menu.Visible&&!(bool)Field("hoverHidden")&&!PassesMouse,"An open menu was hidden by hover logic");
        }finally{menu.Close();}
        Call("UpdateHover",inside);
        Check((bool)Field("hoverHidden")&&PassesMouse,"Hover hiding did not resume after closing the menu");
        ((ToolStripMenuItem)Field("hoverHideItem")).PerformClick();
        Check(!(bool)Saved("HideOnHover")&&!(bool)Field("hoverHidden")&&!PassesMouse,"Disabling hover hiding did not restore hit testing");
        Call("RefreshMenu");Check(!((ToolStripMenuItem)Field("hoverHideItem")).Checked,"Hover-hiding menu checkmark stayed enabled");
        Call("Tick");
        Check((byte)Field("renderedOpacity")>=127&&(byte)Field("renderedOpacity")<=128,"Leaving hover mode lost the normal opacity");
        ((ToolStripMenuItem)Field("hoverHideItem")).PerformClick();
        Call("UpdateHover",inside);
        Check((bool)Saved("HideOnHover")&&(bool)Field("hoverHidden"),"Hover hiding could not be enabled a second time");
    }
    static void Check(bool value,string reason){if(!value)throw new Exception(reason);checks++;}
    static object LoadSettings(){return settingsType.GetMethod("Load").Invoke(null,new object[]{settingsPath});}
    static object Saved(string name){return settingsType.GetField(name).GetValue(LoadSettings());}
    static void Set(object settings,string field,object value){settingsType.GetField(field).SetValue(settings,value);}
    static int DeviceCount(){
        uint count=0,size=(uint)Marshal.SizeOf(typeof(Device));
        if(GetRegisteredRawInputDevices(null,ref count,size)==uint.MaxValue)throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
        if(count==0)return 0;
        var devices=new Device[count];
        if(GetRegisteredRawInputDevices(devices,ref count,size)==uint.MaxValue)throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
        int result=0;
        foreach(Device device in devices)if(device.Page==1&&(device.Usage==2||device.Usage==6)){
            Check(device.Flags==0x100,"Input device did not use read-only INPUTSINK flags");
            Check(device.Target==pet.Handle,"Input sink target differs from the pet window");result++;
        }
        return result;
    }
    [STAThread]static int Main(string[] args){
        try{
            if(args.Length!=2)throw new ArgumentException("Expected application path and isolated output directory");
            output=Path.GetFullPath(args[1]);Directory.CreateDirectory(output);
            settingsPath=Path.Combine(output,"shell-settings.xml");
            Assembly application=Assembly.LoadFrom(Path.GetFullPath(args[0]));
            petType=application.GetType("Pet",true);settingsType=application.GetType("AppSettings",true);
            File.WriteAllText(settingsPath,"<AppSettings><SizeScale>1.45</SizeScale></AppSettings>");
            Check((int)Saved("OpacityPercent")==100&&!(bool)Saved("HideOnHover"),"Older XML did not retain new setting defaults");
            File.WriteAllText(settingsPath,"<AppSettings><OpacityPercent>0</OpacityPercent><HideOnHover>true</HideOnHover></AppSettings>");
            Check((int)Saved("OpacityPercent")==25&&(bool)Saved("HideOnHover"),"Minimum opacity normalization or hover setting failed");
            File.WriteAllText(settingsPath,"<AppSettings><OpacityPercent>250</OpacityPercent></AppSettings>");
            Check((int)Saved("OpacityPercent")==100,"Maximum opacity normalization failed");
            object saved=Activator.CreateInstance(settingsType);
            Rectangle work=Screen.PrimaryScreen.WorkingArea;
            Set(saved,"SizeScale",1.0875);Set(saved,"TopMost",false);Set(saved,"Locked",true);
            Set(saved,"HasLocation",true);Set(saved,"X",work.Left+20);Set(saved,"Y",work.Top+20);
            settingsType.GetMethod("Save").Invoke(saved,new object[]{settingsPath});
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException);
            Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
            object scene=Activator.CreateInstance(application.GetType("Scene",true));
            ConstructorInfo constructor=petType.GetConstructor(new[]{scene.GetType(),typeof(string)});
            if(constructor==null)throw new Exception("Pet needs an optional settingsPath constructor argument for isolated shell checks");
            pet=(Form)constructor.Invoke(new[]{scene,(object)settingsPath});
            tray=(NotifyIcon)Field("tray");
            Check(!tray.Visible&&!pet.Visible,"Constructing the shell showed it before Application.Run");
            Check(!pet.TopMost&&(bool)Field("settings").GetType().GetField("Locked").GetValue(Field("settings")),"Saved topmost/lock settings were not loaded");
            Check(Math.Abs((double)Field("sizeScale")-1.0875)<.00001,"Saved scale was not loaded");
            Check(pet.Location==new Point(work.Left+20,work.Top+20),"Saved location was not loaded");
            steps=new Timer{Interval=250};
            steps.Tick+=delegate{
                try{
                    if(phase==0){
                        Check(pet.Visible&&tray.Visible&&!pet.ShowInTaskbar,"Shown shell has incorrect tray/taskbar visibility");
                        Check((bool)Field("rawInputRegistered")&&((Timer)Field("timer")).Enabled&&DeviceCount()==2,"Shown shell did not register both input sinks");
                        Check((bool)Field("hasFrame"),"Shown shell never rendered a frame");
                        ((ToolStripMenuItem)Field("lockedItem")).PerformClick();
                        Check(!(bool)Saved("Locked"),"Lock menu action was not persisted");
                        var size=(ToolStripMenuItem)Field("sizeItem");
                        foreach(ToolStripMenuItem item in size.DropDownItems)if((int)item.Tag==125)item.PerformClick();
                        Check(Math.Abs((double)Saved("SizeScale")-1.8125)<.00001,"Size menu action was not persisted");
                        ((ToolStripMenuItem)Field("topMostItem")).PerformClick();
                        Check(pet.TopMost&&(bool)Saved("TopMost"),"Topmost menu action was not persisted");
                        CheckHover();
                        ((ToolStripMenuItem)Field("visibilityItem")).PerformClick();
                        Check(!pet.Visible&&tray.Visible,"Hide action lost the tray or kept the pet visible");
                        Check(!(bool)Field("hoverHidden")&&!PassesMouse,"Manual hide retained hover pass-through state");
                        Check(!(bool)Field("rawInputRegistered")&&!((Timer)Field("timer")).Enabled&&DeviceCount()==0,"Hide action retained input sinks or rendering timer");
                        phase=1;
                    }else if(phase==1){
                        // Exercise the same window message used by second launch;
                        // no secondary application or input injection is needed.
                        int message=(int)petType.GetField("ShowPetMessage").GetRawConstantValue();
                        Check(PostMessage(pet.Handle,message,IntPtr.Zero,IntPtr.Zero),"Could not post the existing-instance show request");phase=2;
                    }else if(phase==2){
                        Check(pet.Visible&&tray.Visible&&DeviceCount()==2,"Existing-instance message did not restore the hidden pet");
                        Check(((Timer)Field("timer")).Enabled,"Showing the pet did not restart rendering");
                        Call("UpdateHover",new Point(pet.Left-10,pet.Top-10));
                        Check(!(bool)Field("hoverHidden")&&!PassesMouse,"Restoring the shell broke hover exit");
                        Check((int)Saved("OpacityPercent")==50&&(bool)Saved("HideOnHover"),"Hide/show lost opacity or hover preferences");
                        pet.Location=new Point(work.Left+35,work.Top+45);
                        steps.Stop();pet.Close();
                    }
                }catch(Exception e){failure=e;steps.Stop();pet.Close();}
            };
            pet.Shown+=delegate{steps.Start();};
            Application.Run(pet);
            if(failure!=null)throw failure;
            Check(pet.IsDisposed&&!tray.Visible,"Closing did not dispose the pet and remove the tray icon");
            Check(DeviceCount()==0,"Closing retained raw input registration");
            Check((int)Saved("X")==work.Left+35&&(int)Saved("Y")==work.Top+45,"Close did not persist the latest window location");
            // Construct a second shell to check settings reload without showing,
            // registering devices or entering a second message loop.
            scene=Activator.CreateInstance(application.GetType("Scene",true));
            using(Form restored=(Form)constructor.Invoke(new[]{scene,(object)settingsPath})){
                Check(restored.TopMost,"Next session lost topmost setting");
                Check(restored.Location==new Point(work.Left+35,work.Top+45),"Next session lost position setting");
                double scale=(double)petType.GetField("sizeScale",Members).GetValue(restored);
                Check(Math.Abs(scale-1.8125)<.00001,"Next session lost size setting");
                object restoredSettings=petType.GetField("settings",Members).GetValue(restored);
                Check((int)settingsType.GetField("OpacityPercent").GetValue(restoredSettings)==50&&(bool)settingsType.GetField("HideOnHover").GetValue(restoredSettings),"Next session lost opacity or hover-hiding settings");
            }
            string report="PASS: "+checks+" actual WinForms shell checks: tray, show/hide, INPUTSINK lifecycle, existing-instance message, menu settings, opacity, hover pass-through/restore, close and reload. No keyboard/mouse injection or startup-registry changes.";
            File.WriteAllText(Path.Combine(output,"shell-checks.txt"),report+Environment.NewLine);Console.WriteLine(report);return 0;
        }catch(Exception e){
            failure=e;Console.Error.WriteLine(e);
            if(output!=null)File.WriteAllText(Path.Combine(output,"shell-checks-error.txt"),e.ToString());
            return 1;
        }finally{if(steps!=null)steps.Dispose();if(pet!=null&&!pet.IsDisposed)pet.Dispose();}
    }
}