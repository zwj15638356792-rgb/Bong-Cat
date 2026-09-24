using System;
using System.IO;
using System.Text;
using System.Windows.Forms;
using System.Xml;
using System.Xml.Serialization;
using Microsoft.Win32;

// Settings are written only after an explicit UI change or a user move.
public sealed class AppSettings {
    public double SizeScale=1.45;
    public bool TopMost=true;
    public bool Locked=false;
    public int OpacityPercent=100;
    public bool HideOnHover=false;
    public bool HasLocation=false;
    public int X=0,Y=0;

    public static string DefaultPath {
        get{return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Dafeiyu","settings.xml");}
    }

    void Normalize(){
        if(double.IsNaN(SizeScale)||double.IsInfinity(SizeScale))SizeScale=1.45;
        SizeScale=Math.Max(.7,Math.Min(2.4,SizeScale));
        OpacityPercent=Math.Max(25,Math.Min(100,OpacityPercent));
        // Negative coordinates are valid on monitors left/above the primary.
        // Absurd positions indicate corrupt settings, not a useful placement.
        if(X < -100000 || X > 100000 || Y < -100000 || Y > 100000){HasLocation=false;X=0;Y=0;}
    }

    public static AppSettings Load(string path=null){
        path=path??DefaultPath;
        try{
            if(!File.Exists(path)||new FileInfo(path).Length>65536)return new AppSettings();
            var options=new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=65536};
            using(var stream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete))
            using(var reader=XmlReader.Create(stream,options)){
                var result=new XmlSerializer(typeof(AppSettings)).Deserialize(reader) as AppSettings;
                if(result==null)return new AppSettings();
                result.Normalize();return result;
            }
        }catch(IOException){return new AppSettings();}
        catch(UnauthorizedAccessException){return new AppSettings();}
        catch(InvalidOperationException){return new AppSettings();}
        catch(XmlException){return new AppSettings();}
        catch(System.Security.SecurityException){return new AppSettings();}
    }

    public void Save(string path=null){
        Normalize();path=Path.GetFullPath(path??DefaultPath);
        string directory=Path.GetDirectoryName(path);
        Directory.CreateDirectory(directory);
        // Same-directory replacement keeps a partially written XML file from
        // becoming the active settings file if the process exits mid-write.
        string temporary=Path.Combine(directory,"."+Path.GetFileName(path)+"."+Guid.NewGuid().ToString("N")+".tmp");
        try{
            var options=new XmlWriterSettings{Encoding=new UTF8Encoding(false),Indent=true,CloseOutput=false};
            using(var stream=new FileStream(temporary,FileMode.CreateNew,FileAccess.Write,FileShare.None)){
                using(var writer=XmlWriter.Create(stream,options))new XmlSerializer(typeof(AppSettings)).Serialize(writer,this);
                stream.Flush(true);
            }
            if(File.Exists(path))File.Replace(temporary,path,null);
            else File.Move(temporary,path);
        }finally{
            try{if(File.Exists(temporary))File.Delete(temporary);}catch(IOException){}catch(UnauthorizedAccessException){}
        }
    }
}

public static class StartupRegistration {
    const string RunKey="Software\\Microsoft\\Windows\\CurrentVersion\\Run";
    const string ValueName="DafeiyuDesktopPet";
    static string Command{get{return "\""+Application.ExecutablePath+"\" --startup";}}

    public static bool IsEnabled {
        get{
            using(RegistryKey key=Registry.CurrentUser.OpenSubKey(RunKey,false)){
                string value=key==null?null:key.GetValue(ValueName,null,RegistryValueOptions.DoNotExpandEnvironmentNames) as string;
                return string.Equals(value,Command,StringComparison.OrdinalIgnoreCase);
            }
        }
    }

    // Called only by the user's startup menu action; installing/building the
    // application never changes Windows startup settings.
    public static void SetEnabled(bool enabled){
        if(enabled){
            using(RegistryKey key=Registry.CurrentUser.CreateSubKey(RunKey)){
                if(key==null)throw new IOException("Could not open the current-user startup settings.");
                key.SetValue(ValueName,Command,RegistryValueKind.String);
            }
        }else{
            using(RegistryKey key=Registry.CurrentUser.OpenSubKey(RunKey,true)){
                if(key!=null)key.DeleteValue(ValueName,false);
            }
        }
    }
}
