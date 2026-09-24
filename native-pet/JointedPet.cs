// Character-specific desktop renderer. Peripheral artwork is from BongoCat.
// .NET Framework / Win32 desktop application with embedded character artwork.
using System;
using System.IO;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Globalization;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Reflection;
using System.Windows.Forms;

struct V {
    public double X,Y;
    public V(double x,double y){X=x;Y=y;}
    public static V operator +(V a,V b){return new V(a.X+b.X,a.Y+b.Y);}
    public static V operator -(V a,V b){return new V(a.X-b.X,a.Y-b.Y);}
    public static V operator *(V a,double b){return new V(a.X*b,a.Y*b);}
    public double Length {get{return Math.Sqrt(X*X+Y*Y);}}
    public V Unit {get{return this*(1/Math.Max(.00001,Length));}}
    public PointF P {get{return new PointF((float)X,(float)Y);}}
    public static V Lerp(V a,V b,double t){return a+(b-a)*t;}
}

static class MouseMotion {
    public static readonly V Center=new V(235,221);
    public const int HalfWidth=10,HalfHeight=8;
    public static V Target(Point cursor,Rectangle monitor){
        double x=monitor.Width>1?(cursor.X-(double)monitor.Left)/(monitor.Width-1):.5;
        double y=monitor.Height>1?(cursor.Y-(double)monitor.Top)/(monitor.Height-1):.5;
        return Center+new V((Anatomy.Clamp(x,0,1)-.5)*HalfWidth*2,(Anatomy.Clamp(y,0,1)-.5)*HalfHeight*2);
    }
}

class ArmPose {
    public V Shoulder,Elbow,Wrist,Tip,Direction;
    public double Angle,ElbowDepth,WristDepth;
}

static class Anatomy {
    public const double KeyUpper=41,KeyLower=39;
    public static double Clamp(double x,double a,double b){return Math.Max(a,Math.Min(b,x));}
    // Neutral wrist: forearm and hand share a forward direction in shallow 3D.
    // Solving the extended forearm avoids unstable iterative wrist corrections.
    public static ArmPose Solve(V shoulder,V tip,double upper,double lower,bool keyboard){
        double handLength=keyboard?17:16;
        double extended=lower+handLength;
        V delta=tip-shoulder;
        double depth=keyboard?30:20;
        double distance=Math.Sqrt(delta.Length*delta.Length+depth*depth);
        double d=Clamp(distance,Math.Abs(upper-extended)+8,(upper+extended)*.985);
        delta=delta*(d/distance);depth*=d/distance;tip=shoulder+delta;
        V u=delta*(1/d);double uz=depth/d;
        double along=(upper*upper-extended*extended+d*d)/(2*d);
        double height=Math.Sqrt(Math.Max(0,upper*upper-along*along));
        // Character's right elbow (screen-left mouse arm) opens away from the
        // torso. A mostly downward pole tucks it inward and reverses the bend.
        // Keep the elbow outward over the larger pointer range by bending
        // more in depth, rather than opening the upper arm away from the torso.
        V pole=keyboard?new V(.10,.20):new V(-.64,1);double pz=-1;
        double dot=pole.X*u.X+pole.Y*u.Y+pz*uz;
        V normal=pole-u*dot;double nz=pz-uz*dot;
        double norm=Math.Sqrt(normal.Length*normal.Length+nz*nz);normal=normal*(1/norm);nz/=norm;
        V elbow=shoulder+u*along+normal*height;
        double ez=uz*along+nz*height;
        V wrist=elbow+(tip-elbow)*(lower/extended);
        double wz=ez+(depth-ez)*(lower/extended);
        V direction=(tip-wrist).Unit;
        double angle=Math.Acos(Clamp((upper*upper+extended*extended-d*d)/(2*upper*extended),-1,1))*180/Math.PI;
        return new ArmPose{Shoulder=shoulder,Elbow=elbow,Wrist=wrist,Tip=tip,Direction=direction,Angle=angle,ElbowDepth=ez,WristDepth=wz};
    }
    public static List<V> SleevePath(ArmPose p){
        double radius=Math.Min(17,Math.Min((p.Shoulder-p.Elbow).Length,(p.Wrist-p.Elbow).Length)*.46);
        V a=p.Elbow+(p.Shoulder-p.Elbow).Unit*radius;
        V b=p.Elbow+(p.Wrist-p.Elbow).Unit*radius;
        var result=new List<V>();
        for(int i=0;i<18;i++)result.Add(V.Lerp(p.Shoulder,a,i/18.0));
        for(int i=0;i<20;i++){
            double t=i/20.0;result.Add(a*((1-t)*(1-t))+p.Elbow*(2*t*(1-t))+b*(t*t));
        }
        for(int i=0;i<=20;i++)result.Add(V.Lerp(b,p.Wrist,i/20.0));
        return result;
    }
}

class Scene : IDisposable {
    sealed class Keycap {
        public readonly string Id,Label;
        public readonly RectangleF Bounds;
        public Keycap(string id,string label,float x,float y,float width,float height){Id=id;Label=label;Bounds=new RectangleF(x,y,width,height);}
        public V Center{get{return KeyboardPoint(Bounds.X+Bounds.Width/2,Bounds.Y+Bounds.Height/2);}}
    }
    static readonly Keycap[] keycaps={
        new Keycap("Tab","Tab",7,8,23,15),
        new Keycap("KeyQ","Q",34,7,17,17),
        new Keycap("KeyW","W",54,7,17,17),
        new Keycap("KeyE","E",74,7,17,17),
        new Keycap("KeyR","R",94,7,17,17),
        new Keycap("Shift","Shift",7,27,23,15),
        new Keycap("KeyA","A",34,27,17,17),
        new Keycap("KeyS","S",54,27,17,17),
        new Keycap("KeyD","D",74,27,17,17),
        new Keycap("Control","Ctrl",7,49,23,13),
        new Keycap("Space","Space",34,49,56,13),
        new Keycap("Return","↵",95,29,17,32),
    };
    // Key legends face the character. The same transform places the fingertip,
    // so a change to the keyboard perspective cannot detach input from its key.
    static V KeyboardPoint(double x,double y){return new V(367-.85*x+.43*y,256-.15*x-.58*y);}
    readonly Dictionary<string,Bitmap> images=new Dictionary<string,Bitmap>();
    public readonly Dictionary<string,V> Targets=new Dictionary<string,V>();
    public readonly V MouseShoulder=new V(248,169),KeyShoulder=new V(316,170);
    public readonly V Rest=KeyboardPoint(43,57);
    // The small keyboard shows twelve representative keys. Other supported
    // inputs use a general typing pose without lighting an unrelated key.
    public static string VisibleKey(string key){
        switch(key){
            case "KeyQ":case "KeyW":case "KeyE":case "KeyR":case "KeyA":case "KeyS":case "KeyD":case "Tab":case "Space":case "Return":return key;
            case "Shift":case "ShiftLeft":case "ShiftRight":return "Shift";
            case "Control":case "ControlLeft":case "ControlRight":return "Control";
            default:return null;
        }
    }
    V TargetFor(string key){
        string visible=VisibleKey(key);
        foreach(Keycap cap in keycaps)if(cap.Id==visible)return cap.Center;
        return KeyboardPoint(62,35);
    }
    public V TargetForInput(string key){V target;return Targets.TryGetValue(key,out target)?target:TargetFor(key);}
    public Scene(){
        Assembly assembly=Assembly.GetExecutingAssembly();
        const string prefix="Dafeiyu.Assets.";
        foreach(string resource in assembly.GetManifestResourceNames()){
            if(!resource.StartsWith(prefix,StringComparison.Ordinal)||!resource.EndsWith(".png",StringComparison.Ordinal))continue;
            using(Stream stream=assembly.GetManifestResourceStream(resource))
            using(var source=new Bitmap(stream))images[Path.GetFileNameWithoutExtension(resource.Substring(prefix.Length))]=new Bitmap(source);
        }
        foreach(string name in new[]{"body","front-hair","cuff-hand","fabric","pad","mouse","mouse-left","mouse-right","face-blink","face-happy","face-surprised"})
            if(!images.ContainsKey(name))throw new InvalidDataException("Missing embedded artwork: "+name);
        for(char c='A';c<='Z';c++){string key="Key"+c;Targets[key]=TargetFor(key);}
        for(int i=0;i<10;i++){string key="Num"+i;Targets[key]=TargetFor(key);}
        foreach(string key in new[]{"Alt","AltGr","BackQuote","Backspace","CapsLock","Control","ControlLeft","ControlRight","Delete","Escape","Fn","Meta","Return","Shift","ShiftLeft","ShiftRight","Slash","Space","Tab"})Targets[key]=TargetFor(key);
    }
    void ImageAt(Graphics g,string name,float x,float y,float heightScale=1){
        Bitmap image=images[name];
        float resolution=(name=="body"||name=="front-hair"||name.StartsWith("face-"))?image.Width/612f:name=="pad"?image.Width/116f:name.StartsWith("mouse")?image.Width/46f:1;
        g.DrawImage(image,new RectangleF(x,y,image.Width/resolution,image.Height/resolution*heightScale),new RectangleF(0,0,image.Width,image.Height),GraphicsUnit.Pixel);
    }
    static GraphicsPath RoundedRect(RectangleF bounds,float radius){
        var path=new GraphicsPath();float diameter=radius*2;
        path.AddArc(bounds.Left,bounds.Top,diameter,diameter,180,90);
        path.AddArc(bounds.Right-diameter,bounds.Top,diameter,diameter,270,90);
        path.AddArc(bounds.Right-diameter,bounds.Bottom-diameter,diameter,diameter,0,90);
        path.AddArc(bounds.Left,bounds.Bottom-diameter,diameter,diameter,90,90);
        path.CloseFigure();return path;
    }
    void DrawKeyboard(Graphics g,IEnumerable<string> lit){
        var active=new HashSet<string>();
        foreach(string key in lit){string visible=VisibleKey(key);if(visible!=null)active.Add(visible);}
        GraphicsState state=g.Save();
        using(var plane=new Matrix(-.85f,-.15f,.43f,-.58f,367,256))g.MultiplyTransform(plane);
        using(var lower=RoundedRect(new RectangleF(0,-5,120,70),17))
        using(var fill=new SolidBrush(Color.FromArgb(112,157,175)))
        using(var outline=new Pen(Color.FromArgb(47,54,61),2.2f)){
            g.FillPath(fill,lower);g.DrawPath(outline,lower);
        }
        using(var shell=RoundedRect(new RectangleF(0,0,120,70),17))
        using(var fill=new SolidBrush(Color.FromArgb(175,217,230)))
        using(var outline=new Pen(Color.FromArgb(47,54,61),2.2f)){
            g.FillPath(fill,shell);g.DrawPath(outline,shell);
        }
        using(var rim=RoundedRect(new RectangleF(3,3,114,64),14))
        using(var light=new Pen(Color.FromArgb(219,237,236),1.6f))g.DrawPath(light,rim);
        using(var letterFont=new Font("Comic Sans MS",11.5f,FontStyle.Bold,GraphicsUnit.Pixel))
        using(var labelFont=new Font("Comic Sans MS",8.5f,FontStyle.Bold,GraphicsUnit.Pixel))
        using(var format=new StringFormat{Alignment=StringAlignment.Center,LineAlignment=StringAlignment.Center,FormatFlags=StringFormatFlags.NoWrap}){
            foreach(Keycap cap in keycaps){
                bool pressed=active.Contains(cap.Id);
                RectangleF face=cap.Bounds;
                RectangleF shadow=face;shadow.Offset(0,-1.8f);
                using(var shape=RoundedRect(shadow,Math.Min(face.Width,face.Height)/2))
                using(var brush=new SolidBrush(Color.FromArgb(73,122,147)))g.FillPath(brush,shape);
                if(pressed)face.Offset(0,-1.0f);
                using(var shape=RoundedRect(face,Math.Min(face.Width,face.Height)/2))
                using(var brush=new SolidBrush(pressed?Color.FromArgb(238,218,147):Color.FromArgb(94,166,194)))
                using(var edge=new Pen(Color.FromArgb(243,244,223),1.9f)){
                    g.FillPath(brush,shape);g.DrawPath(edge,shape);
                }
                using(var text=new SolidBrush(pressed?Color.FromArgb(83,99,110):Color.FromArgb(249,248,224)))g.DrawString(cap.Label,cap.Label.Length==1?letterFont:labelFont,text,face,format);
            }
        }
        g.Restore(state);
    }
    void Arm(Graphics g,ArmPose pose,bool mirror){
        List<V> path=Anatomy.SleevePath(pose);
        V cuffStart=pose.Wrist-pose.Direction*4;
        Bitmap hand=images["cuff-hand"];
        double handWidth=(pose.Tip-cuffStart).Length*hand.Width/hand.Height/2;
        var leftEdge=new List<PointF>();var rightEdge=new List<PointF>();
        for(int i=0;i<path.Count;i++){
            double t=i/(double)(path.Count-1);
            V d=(path[Math.Min(path.Count-1,i+1)]-path[Math.Max(0,i-1)]).Unit;
            V n=new V(-d.Y,d.X);
            double width=(15+7*Math.Sin(t*Math.PI)+3*t)*(mirror?1:.82);
            // A foreshortened cuff gets narrower with the hand; a full-width
            // sleeve end would cover the knuckles and look like a bent wrist.
            double cuffBlend=Anatomy.Clamp((t-.68)/.32,0,1);
            width=width*(1-cuffBlend)+Math.Min(width,handWidth*1.45)*cuffBlend;
            leftEdge.Add((path[i]-n*(width/2)).P);rightEdge.Add((path[i]+n*(width/2)).P);
        }
        rightEdge.Reverse();leftEdge.AddRange(rightEdge);
        using(var outline=new GraphicsPath()){
            outline.AddPolygon(leftEdge.ToArray());
            RectangleF bounds=outline.GetBounds();
            using(var fabric=new LinearGradientBrush(bounds,Color.FromArgb(47,64,124),Color.FromArgb(25,36,84),25))g.FillPath(fabric,outline);
            using(var cloth=new TextureBrush(images["fabric"],WrapMode.TileFlipXY)){
                using(var transform=new Matrix()){
                    transform.Translate(bounds.X,bounds.Y);transform.Scale(bounds.Width/images["fabric"].Width,bounds.Height/images["fabric"].Height);cloth.Transform=transform;
                    g.FillPath(cloth,outline);
                }
            }
            using(var edge=new Pen(Color.FromArgb(22,32,72),1)){edge.LineJoin=LineJoin.Round;g.DrawPath(edge,outline);}
            V inner=((pose.Shoulder-pose.Elbow).Unit+(pose.Wrist-pose.Elbow).Unit).Unit;
            V crease=pose.Elbow+inner*6,across=new V(-inner.Y,inner.X);
            using(var fold=new GraphicsPath()){
                fold.AddBezier((crease-across*5).P,(crease+inner*2-across*2).P,(crease+inner*2+across*2).P,(crease+across*5).P);
                using(var shadow=new Pen(Color.FromArgb(90,16,26,64),.8f))g.DrawPath(shadow,fold);
            }
        }
        using(var attributes=new ImageAttributes()){
            attributes.SetWrapMode(WrapMode.TileFlipXY);
            V normal=new V(-pose.Direction.Y,pose.Direction.X);
            // Anchor the cuff to the actual wrist. A minimum sprite length used to
            // move it up the forearm on foreshortened lower-key poses.
            V start=cuffStart;
            V left=start-normal*handWidth,right=start+normal*handWidth,end=pose.Tip-normal*handWidth;
            if(mirror){left=start+normal*handWidth;right=start-normal*handWidth;end=pose.Tip+normal*handWidth;}
            g.DrawImage(hand,new PointF[]{left.P,right.P,end.P},new RectangleF(0,0,hand.Width,hand.Height),GraphicsUnit.Pixel,attributes);
        }
    }
    public string[] Expressions {get {var names=new List<string>();foreach(string name in images.Keys)if(name.StartsWith("face-"))names.Add(name);names.Sort(StringComparer.Ordinal);return names.ToArray();}}
    public Bitmap Render(V mouse,V key,IEnumerable<string> lit,bool left,bool right,int scale,string expression=null){
        var bitmap=new Bitmap(612*scale,354*scale,PixelFormat.Format32bppArgb);
        using(Graphics g=Graphics.FromImage(bitmap)){
            g.Clear(Color.Transparent);g.ScaleTransform(scale,scale);
            g.SmoothingMode=SmoothingMode.AntiAlias;g.InterpolationMode=InterpolationMode.HighQualityBicubic;g.PixelOffsetMode=PixelOffsetMode.HighQuality;
            ImageAt(g,"body",0,0);
            if(expression!=null&&images.ContainsKey(expression))ImageAt(g,expression,0,0);
            // Extend the front edge to support the larger mouse travel.
            ImageAt(g,"pad",189,193,1.2f);DrawKeyboard(g,lit);
            ImageAt(g,left?"mouse-left":right?"mouse-right":"mouse",(float)mouse.X-23,(float)mouse.Y-21);
            Arm(g,Anatomy.Solve(MouseShoulder,mouse+new V(-1,-4),32,31,false),false);
            Arm(g,Anatomy.Solve(KeyShoulder,key,Anatomy.KeyUpper,Anatomy.KeyLower,true),true);
            ImageAt(g,"front-hair",0,0);
        }
        return bitmap;
    }
    public void Dispose(){foreach(Bitmap b in images.Values)b.Dispose();}
}

sealed class TypingExpression {
    readonly string[] choices;
    readonly Random random;
    int previous=-1;
    string active;
    double until,nextTrigger;
    public TypingExpression(string[] choices,Random random=null){
        this.choices=(string[])choices.Clone();this.random=random??new Random();
    }
    public bool Trigger(double now){
        if(now<nextTrigger||choices.Length==0)return false;
        int pick=random.Next(previous>=0&&choices.Length>1?choices.Length-1:choices.Length);
        if(previous>=0&&choices.Length>1&&pick>=previous)pick++;
        previous=pick;active=choices[pick];
        until=now+.9+random.NextDouble()*.45;
        nextTrigger=now+1.7+random.NextDouble()*1.1;
        return true;
    }
    // Returning to neutral does not forget the last selected expression.
    public string Current(double now){return now<until?active:null;}
}

class Pet : Form {
    public const string WindowTitle="大肥鱼桌宠";
    public const int ShowPetMessage=0x8000+73;
    readonly Scene scene;
    readonly AppSettings settings;
    readonly string settingsPath;
    readonly Timer timer=new Timer{Interval=16};
    readonly Stopwatch clock=Stopwatch.StartNew();
    readonly Dictionary<int,string> held=new Dictionary<int,string>();
    readonly List<int> order=new List<int>();
    bool rawInputRegistered;
    bool leftHeld,rightHeld;
    double leftDown=-10,rightDown=-10;
    string lastKey;
    double lastDown=-10,lastTick;
    readonly TypingExpression expressions;
    V mouse=MouseMotion.Center,hand;
    V renderedMouse,renderedHand;
    bool hasFrame,renderedLeft,renderedRight;
    string renderedKeys,renderedExpression;
    double renderedScale;
    byte renderedOpacity;
    bool hoverHidden;
    Point renderedLocation;
    double sizeScale=1.45;
    bool dragging;
    Point dragStart,windowStart;
    readonly ContextMenuStrip menu=new ContextMenuStrip();
    readonly NotifyIcon tray=new NotifyIcon();
    readonly Icon appIcon;
    ToolStripMenuItem visibilityItem,topMostItem,lockedItem,startupItem,sizeItem,opacityItem,hoverHideItem;
    bool closing,disposed;
    public Pet(Scene model,string settingsPath=null){
        this.settingsPath=settingsPath;
        scene=model;hand=scene.Rest;expressions=new TypingExpression(scene.Expressions);Text=WindowTitle;
        settings=AppSettings.Load(settingsPath);sizeScale=settings.SizeScale;
        FormBorderStyle=FormBorderStyle.None;ShowInTaskbar=false;TopMost=settings.TopMost;
        StartPosition=FormStartPosition.Manual;
        Rectangle screen=Screen.PrimaryScreen.WorkingArea;
        Location=new Point(screen.Right-490,screen.Bottom-450);
        if(settings.HasLocation)Location=new Point(settings.X,settings.Y);
        ClientSize=new Size((int)(300*sizeScale),(int)(290*sizeScale));
        KeepOnScreen();
        appIcon=Icon.ExtractAssociatedIcon(Application.ExecutablePath);Icon=appIcon;
        BuildMenu();
        tray.Icon=appIcon;tray.Text="大肥鱼桌宠";tray.ContextMenuStrip=menu;
        tray.DoubleClick+=delegate{SetPetVisible(true);};
        MouseDown+=delegate(object sender,MouseEventArgs e){
            if(e.Button==MouseButtons.Right){menu.Show(Cursor.Position);return;}
            if(e.Button==MouseButtons.Left&&!settings.Locked){dragging=true;dragStart=Cursor.Position;windowStart=Location;Capture=true;}
        };
        MouseMove+=delegate{if(dragging){Point p=Cursor.Position;Location=new Point(windowStart.X+p.X-dragStart.X,windowStart.Y+p.Y-dragStart.Y);}};
        MouseUp+=delegate{if(dragging){dragging=false;Capture=false;SaveSettings();}};
        Shown+=delegate{tray.Visible=true;StartTracking();};
        timer.Tick+=delegate{Tick();};
        FormClosing+=delegate{closing=true;SaveSettings();tray.Visible=false;StopTracking();};
    }
    void BuildMenu(){
        menu.Items.Add(new ToolStripMenuItem("大肥鱼桌宠 v1.1.1"){Enabled=false});
        menu.Items.Add(new ToolStripSeparator());
        visibilityItem=new ToolStripMenuItem("隐藏桌宠",null,delegate{SetPetVisible(!Visible);});menu.Items.Add(visibilityItem);
        sizeItem=new ToolStripMenuItem("大小");menu.Items.Add(sizeItem);
        foreach(int percentage in new[]{50,75,100,125,150}){
            int value=percentage;
            var item=new ToolStripMenuItem(value+"%",null,delegate{sizeScale=1.45*value/100;ClientSize=new Size((int)(300*sizeScale),(int)(290*sizeScale));KeepOnScreen();hasFrame=false;SaveSettings();});
            item.Tag=value;sizeItem.DropDownItems.Add(item);
        }
        opacityItem=new ToolStripMenuItem("透明度");menu.Items.Add(opacityItem);
        foreach(int percentage in new[]{25,50,75,100}){
            int value=percentage;
            string label=value+"%"+(value==100?"（不透明）":value==25?"（更透明）":"");
            var item=new ToolStripMenuItem(label,null,delegate{settings.OpacityPercent=value;hasFrame=false;SaveSettings();});
            item.Tag=value;opacityItem.DropDownItems.Add(item);
        }
        hoverHideItem=new ToolStripMenuItem("鼠标悬停时隐藏",null,delegate{settings.HideOnHover=!settings.HideOnHover;UpdateHover(Cursor.Position);hasFrame=false;SaveSettings();});menu.Items.Add(hoverHideItem);
        topMostItem=new ToolStripMenuItem("总在最前",null,delegate{TopMost=!TopMost;SaveSettings();});menu.Items.Add(topMostItem);
        lockedItem=new ToolStripMenuItem("锁定位置",null,delegate{settings.Locked=!settings.Locked;SaveSettings();});menu.Items.Add(lockedItem);
        menu.Items.Add("重置位置",null,delegate{Rectangle area=Screen.PrimaryScreen.WorkingArea;Location=new Point(area.Right-ClientSize.Width-20,area.Bottom-ClientSize.Height-20);KeepOnScreen();SetPetVisible(true);SaveSettings();});
        menu.Items.Add(new ToolStripSeparator());
        startupItem=new ToolStripMenuItem("开机启动",null,delegate{
            try{StartupRegistration.SetEnabled(!StartupRegistration.IsEnabled);}
            catch(Exception e){MessageBox.Show("无法修改开机启动："+e.Message,WindowTitle,MessageBoxButtons.OK,MessageBoxIcon.Information);}
        });menu.Items.Add(startupItem);
        menu.Items.Add("关于",null,delegate{MessageBox.Show("大肥鱼桌宠 v1.1.1\r\n\r\n拖动角色移动位置，右键角色或托盘图标打开菜单。\r\n双击托盘图标可以找回隐藏的角色。\r\n\r\n基于 ayangweb/BongoCat 素材制作。\r\n输入仅用于本地动画，不记录文本或控制系统鼠标。",WindowTitle,MessageBoxButtons.OK,MessageBoxIcon.Information);});
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("退出",null,delegate{Close();});
        menu.Opening+=delegate{RefreshMenu();};
    }
    void RefreshMenu(){
        visibilityItem.Text=Visible?"隐藏桌宠":"显示桌宠";
        topMostItem.Checked=TopMost;lockedItem.Checked=settings.Locked;hoverHideItem.Checked=settings.HideOnHover;
        foreach(ToolStripMenuItem item in opacityItem.DropDownItems)item.Checked=(int)item.Tag==settings.OpacityPercent;
        try{startupItem.Checked=StartupRegistration.IsEnabled;}catch{startupItem.Checked=false;}
        foreach(ToolStripMenuItem item in sizeItem.DropDownItems)item.Checked=Math.Abs(sizeScale-1.45*(int)item.Tag/100)<.001;
    }
    void UpdateHover(Point cursor){
        // Test the same screen rectangle even while invisible, so making the
        // pet transparent cannot generate a false mouse-leave and flicker.
        bool hide=settings.HideOnHover&&Visible&&!dragging&&!menu.Visible
            &&new Rectangle(Location,ClientSize).Contains(cursor);
        if(hide==hoverHidden)return;
        hoverHidden=hide;hasFrame=false;
        if(IsHandleCreated)Native.SetMousePassthrough(Handle,hide);
    }
    void KeepOnScreen(){
        Rectangle area=Screen.FromRectangle(new Rectangle(Location,ClientSize)).WorkingArea;
        Location=new Point(Math.Max(area.Left,Math.Min(Location.X,area.Right-ClientSize.Width)),Math.Max(area.Top,Math.Min(Location.Y,area.Bottom-ClientSize.Height)));
    }
    void SaveSettings(){
        settings.SizeScale=sizeScale;settings.TopMost=TopMost;settings.HasLocation=true;settings.X=Location.X;settings.Y=Location.Y;
        try{settings.Save(settingsPath);}catch(Exception e){Program.LogError(e);}
    }
    void StartTracking(){
        if(!rawInputRegistered){Native.RegisterInputSink(Handle);rawInputRegistered=true;}
        hasFrame=false;lastTick=clock.Elapsed.TotalSeconds;timer.Start();
    }
    void StopTracking(){
        timer.Stop();if(rawInputRegistered){Native.RemoveInputSink();rawInputRegistered=false;}
        held.Clear();order.Clear();lastKey=null;lastDown=-10;leftHeld=false;rightHeld=false;leftDown=-10;rightDown=-10;
    }
    void SetPetVisible(bool show){
        if(closing)return;
        if(show){Show();KeepOnScreen();UpdateHover(Cursor.Position);StartTracking();}
        else{dragging=false;Capture=false;StopTracking();Hide();UpdateHover(Cursor.Position);}
        RefreshMenu();
    }
    protected override bool ShowWithoutActivation{get{return true;}}
    protected override void Dispose(bool disposing){
        if(disposing&&!disposed){disposed=true;StopTracking();timer.Dispose();tray.Visible=false;tray.Dispose();menu.Dispose();scene.Dispose();if(appIcon!=null)appIcon.Dispose();}
        base.Dispose(disposing);
    }
    protected override CreateParams CreateParams{get{var p=base.CreateParams;p.ExStyle|=0x80000;return p;}}
    protected override void WndProc(ref Message m){
        if(m.Msg==0x21){m.Result=new IntPtr(3);return;}
        if(m.Msg==ShowPetMessage){SetPetVisible(true);m.Result=IntPtr.Zero;return;}
        if(m.Msg==0xff&&rawInputRegistered){
            Native.RawInputEvent input;
            if(Native.TryReadInput(m.LParam,out input)){
                if(input.IsKeyboard)Keyboard(input.VirtualKey,input.ScanCode,input.KeyFlags,input.IsDown);
                else MouseInput(input.MouseButtons);
            }
        }
        // Let DefWindowProc perform WM_INPUT cleanup, including foreground input.
        base.WndProc(ref m);
    }
    public static int PhysicalKeyId(int vk,int scan,int flags){
        // Scan code and E0/E1 prefixes stay stable across key-up and layout
        // changes. Devices with no scan code use their virtual key.
        return (scan!=0?scan:(0x1000000|vk))|((flags&1)<<16)|((flags&2)<<16);
    }
    public static string MapKey(int vk,int scan,int flags){
        if(vk>=65&&vk<=90)return "Key"+(char)vk;
        if(vk>=48&&vk<=57)return "Num"+(char)vk;
        if(vk>=96&&vk<=105)return "Num"+(vk-96);
        if(vk>=112&&vk<=135)return "Fn";
        switch(vk){
            case 16:return scan==54?"ShiftRight":"ShiftLeft";case 160:return "ShiftLeft";case 161:return "ShiftRight";
            case 17:return (flags&1)!=0?"ControlRight":"ControlLeft";case 162:return "ControlLeft";case 163:return "ControlRight";
            case 18:return (flags&1)!=0?"AltGr":"Alt";case 164:return "Alt";case 165:return "AltGr";
            case 91:case 92:return "Meta";case 8:return "Backspace";case 9:return "Tab";case 13:return "Return";
            case 20:return "CapsLock";case 27:return "Escape";case 32:return "Space";case 46:return "Delete";
            case 192:return "BackQuote";case 191:return "Slash";
            default:return "Typing";
        }
    }
    void Keyboard(int vk,int scan,int flags,bool down){
        string key=MapKey(vk,scan,flags);
        int physical=PhysicalKeyId(vk,scan,flags);
        // Pause/E1 can be make-only: give it a short typing response without
        // leaving a permanently held key when the device sends no BREAK.
        if(vk==19&&(flags&2)!=0){
            if(down){lastKey=key;lastDown=clock.Elapsed.TotalSeconds;expressions.Trigger(lastDown);}
            return;
        }
        if(down){
            if(!held.ContainsKey(physical)){
                double now=clock.Elapsed.TotalSeconds;
                held[physical]=key;order.Remove(physical);order.Add(physical);lastKey=key;lastDown=now;
                expressions.Trigger(now);
            }
        }else{held.Remove(physical);order.Remove(physical);}
    }
    void MouseInput(ushort buttons){
        double now=clock.Elapsed.TotalSeconds;
        if((buttons&0x0001)!=0){leftHeld=true;leftDown=now;}
        if((buttons&0x0002)!=0)leftHeld=false;
        if((buttons&0x0004)!=0){rightHeld=true;rightDown=now;}
        if((buttons&0x0008)!=0)rightHeld=false;
    }
    void Tick(){
        double now=clock.Elapsed.TotalSeconds,dt=Math.Min(.1,now-lastTick);lastTick=now;
        Point cursor=Cursor.Position;UpdateHover(cursor);
        byte opacity=hoverHidden?(byte)0:(byte)Math.Round(settings.OpacityPercent*255/100.0);
        if(hoverHidden&&hasFrame&&renderedOpacity==0)return;
        Rectangle monitor=Screen.FromPoint(cursor).Bounds;
        V desired=MouseMotion.Target(cursor,monitor);
        mouse=V.Lerp(mouse,desired,1-Math.Exp(-dt/.045));
        string current=order.Count>0?held[order[order.Count-1]]:now-lastDown<.13?lastKey:null;
        V target=current!=null?scene.TargetForInput(current):scene.Rest;
        hand=V.Lerp(hand,target,1-Math.Exp(-dt/.028));
        var lit=new List<string>(held.Values);
        if(lastKey!=null&&now-lastDown<.055&&!lit.Contains(lastKey))lit.Add(lastKey);
        bool left=leftHeld||now-leftDown<.055,right=rightHeld||now-rightDown<.055;
        string face=expressions.Current(now),keyState=string.Join(",",lit.ToArray());
        // A still pet needs no new bitmap or layered-window update. Keeping
        // animation work out of idle ticks also leaves the input queue responsive.
        if(hasFrame&&(mouse-renderedMouse).Length<.015&&(hand-renderedHand).Length<.015
            &&left==renderedLeft&&right==renderedRight&&keyState==renderedKeys
            &&face==renderedExpression&&sizeScale==renderedScale&&Location==renderedLocation&&opacity==renderedOpacity)return;
        int renderScale=Math.Max(2,(int)Math.Ceiling(sizeScale));
        using(Bitmap full=scene.Render(mouse,hand,lit,left,right,renderScale,face)){
            // Crop unused transparent margins; the whole window remains per-pixel transparent.
            using(Bitmap frame=new Bitmap((int)(300*sizeScale),(int)(290*sizeScale),PixelFormat.Format32bppArgb)){
                using(Graphics g=Graphics.FromImage(frame)){g.InterpolationMode=InterpolationMode.HighQualityBicubic;g.PixelOffsetMode=PixelOffsetMode.HighQuality;g.DrawImage(full,new Rectangle(0,0,frame.Width,frame.Height),new Rectangle(140*renderScale,0,300*renderScale,290*renderScale),GraphicsUnit.Pixel);}
                Native.Present(Handle,frame,Location,opacity);
            }
        }
        hasFrame=true;renderedMouse=mouse;renderedHand=hand;renderedLeft=left;renderedRight=right;
        renderedKeys=keyState;renderedExpression=face;renderedScale=sizeScale;renderedLocation=Location;renderedOpacity=opacity;
    }
}

static class Native {
    public struct RawInputEvent {
        public bool IsKeyboard,IsDown;
        public int VirtualKey,ScanCode,KeyFlags;
        public ushort MouseButtons;
    }
    [StructLayout(LayoutKind.Sequential)]struct RawInputDevice {
        public ushort UsagePage,Usage;
        public uint Flags;
        public IntPtr Target;
    }
    [StructLayout(LayoutKind.Sequential)]struct POINT{public int x,y;public POINT(int a,int b){x=a;y=b;}}
    [StructLayout(LayoutKind.Sequential)]struct SIZE{public int cx,cy;public SIZE(int x,int y){cx=x;cy=y;}}
    [StructLayout(LayoutKind.Sequential,Pack=1)]struct BLEND{public byte op,flags,alpha,format;}
    [DllImport("user32.dll",SetLastError=true)]static extern bool RegisterRawInputDevices([In]RawInputDevice[] devices,uint count,uint size);
    [DllImport("user32.dll",SetLastError=true)]static extern uint GetRawInputData(IntPtr input,uint command,[Out]byte[] data,ref uint size,uint headerSize);
    [DllImport("user32.dll")]public static extern bool SetProcessDPIAware();
    [DllImport("user32.dll",EntryPoint="GetWindowLongW")]static extern int GetWindowStyle(IntPtr window,int index);
    [DllImport("user32.dll",EntryPoint="SetWindowLongW")]static extern int SetWindowStyle(IntPtr window,int index,int value);
    public static void SetMousePassthrough(IntPtr window,bool enabled){
        int style=GetWindowStyle(window,-20);
        SetWindowStyle(window,-20,enabled?style|0x20:style&~0x20);
    }
    [DllImport("user32.dll",CharSet=CharSet.Unicode)]static extern IntPtr FindWindow(string className,string title);
    [DllImport("user32.dll")]static extern bool PostMessage(IntPtr window,int message,IntPtr w,IntPtr l);
    public static void ShowExistingPet(){
        IntPtr window=FindWindow(null,Pet.WindowTitle);
        if(window!=IntPtr.Zero)PostMessage(window,Pet.ShowPetMessage,IntPtr.Zero,IntPtr.Zero);
    }
    [DllImport("user32.dll")]static extern IntPtr GetDC(IntPtr window);
    [DllImport("user32.dll")]static extern int ReleaseDC(IntPtr window,IntPtr dc);
    [DllImport("gdi32.dll")]static extern IntPtr CreateCompatibleDC(IntPtr dc);
    [DllImport("gdi32.dll")]static extern bool DeleteDC(IntPtr dc);
    [DllImport("gdi32.dll")]static extern IntPtr SelectObject(IntPtr dc,IntPtr obj);
    [DllImport("gdi32.dll")]static extern bool DeleteObject(IntPtr obj);
    [DllImport("user32.dll",SetLastError=true)]static extern bool UpdateLayeredWindow(IntPtr window,IntPtr dc,ref POINT dst,ref SIZE size,IntPtr src,ref POINT source,uint key,ref BLEND blend,uint flags);
    static RawInputDevice[] InputDevices(IntPtr window,uint flags){
        return new[]{new RawInputDevice{UsagePage=1,Usage=6,Flags=flags,Target=window},new RawInputDevice{UsagePage=1,Usage=2,Flags=flags,Target=window}};
    }
    public static void RegisterInputSink(IntPtr window){
        // INPUTSINK only: receive copies of input without suppressing normal
        // keyboard/mouse messages, grabbing a device, or moving the cursor.
        if(!RegisterRawInputDevices(InputDevices(window,0x100),2,(uint)Marshal.SizeOf(typeof(RawInputDevice))))throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
    }
    public static void RemoveInputSink(){
        RegisterRawInputDevices(InputDevices(IntPtr.Zero,0x1),2,(uint)Marshal.SizeOf(typeof(RawInputDevice)));
    }
    public static bool TryReadInput(IntPtr handle,out RawInputEvent input){
        input=new RawInputEvent();
        uint size=0,header=(uint)(8+IntPtr.Size*2);
        if(GetRawInputData(handle,0x10000003,null,ref size,header)==uint.MaxValue||size<header||size>4096)return false;
        var bytes=new byte[(int)size];
        uint copied=GetRawInputData(handle,0x10000003,bytes,ref size,header);
        if(copied==uint.MaxValue||copied>bytes.Length)return false;
        return TryParseInput(bytes,(int)copied,IntPtr.Size,out input);
    }
    // Pure decoder, shared with offline tests. Payload offsets are defined by
    // RAWKEYBOARD/RAWMOUSE; the header alone differs between x86 and x64.
    public static bool TryParseInput(byte[] bytes,int count,int pointerSize,out RawInputEvent input){
        input=new RawInputEvent();
        if(bytes==null||(pointerSize!=4&&pointerSize!=8))return false;
        int header=8+pointerSize*2;
        if(count<header||count>bytes.Length)return false;
        uint kind=BitConverter.ToUInt32(bytes,0),declared=BitConverter.ToUInt32(bytes,4);
        if(declared<header||declared>count)return false;
        if(kind==1){
            if(declared<header+16)return false;
            int scan=BitConverter.ToUInt16(bytes,header),flags=BitConverter.ToUInt16(bytes,header+2),vk=BitConverter.ToUInt16(bytes,header+6);
            if(vk==255||vk==0||scan==255)return false;
            input.IsKeyboard=true;input.IsDown=(flags&1)==0;
            input.VirtualKey=vk;input.ScanCode=scan;
            input.KeyFlags=((flags&2)!=0?1:0)|((flags&4)!=0?2:0);
            return true;
        }
        if(kind==0){
            if(declared<header+24)return false;
            input.MouseButtons=BitConverter.ToUInt16(bytes,header+4);
            return true;
        }
        return false;
    }
    public static void Present(IntPtr window,Bitmap bitmap,Point location,byte opacity=255){
        IntPtr screen=GetDC(IntPtr.Zero),memory=CreateCompatibleDC(screen),handle=bitmap.GetHbitmap(Color.FromArgb(0));
        IntPtr old=SelectObject(memory,handle);
        try{
            POINT dst=new POINT(location.X,location.Y),source=new POINT(0,0);SIZE size=new SIZE(bitmap.Width,bitmap.Height);
            BLEND blend=new BLEND{op=0,flags=0,alpha=opacity,format=1};
            if(!UpdateLayeredWindow(window,screen,ref dst,ref size,memory,ref source,0,ref blend,2))throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
        }finally{SelectObject(memory,old);DeleteObject(handle);DeleteDC(memory);ReleaseDC(IntPtr.Zero,screen);}
    }
}

static class Program {
    public static void LogError(Exception e){
        try{
            string folder=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Dafeiyu");
            Directory.CreateDirectory(folder);File.WriteAllText(Path.Combine(folder,"error.log"),e.ToString());
        }catch{}
    }
    [STAThread]static void Main(string[] args){
        try{Run(args);}catch(Exception e){
            if(args.Length>0&&args[0]=="--render")File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"error.log"),e.ToString());
            else{LogError(e);MessageBox.Show("桌宠启动失败："+e.Message,Pet.WindowTitle,MessageBoxButtons.OK,MessageBoxIcon.Error);}
            Environment.ExitCode=1;
        }
    }
    static void ValidateRawInputPackets(string destination){
        foreach(int pointerSize in new[]{4,8}){
            int header=8+pointerSize*2;
            var keyboard=new byte[header+16];
            Buffer.BlockCopy(BitConverter.GetBytes((uint)1),0,keyboard,0,4);
            Buffer.BlockCopy(BitConverter.GetBytes((uint)keyboard.Length),0,keyboard,4,4);
            Buffer.BlockCopy(BitConverter.GetBytes((ushort)29),0,keyboard,header,2);
            Buffer.BlockCopy(BitConverter.GetBytes((ushort)2),0,keyboard,header+2,2);
            Buffer.BlockCopy(BitConverter.GetBytes((ushort)17),0,keyboard,header+6,2);
            Native.RawInputEvent input;
            if(!Native.TryParseInput(keyboard,keyboard.Length,pointerSize,out input)||!input.IsKeyboard||!input.IsDown||input.KeyFlags!=1||Pet.MapKey(input.VirtualKey,input.ScanCode,input.KeyFlags)!="ControlRight")throw new Exception("Raw keyboard E0 packet parsing failed");
            int downIdentity=Pet.PhysicalKeyId(input.VirtualKey,input.ScanCode,input.KeyFlags);
            Buffer.BlockCopy(BitConverter.GetBytes((ushort)3),0,keyboard,header+2,2);
            if(!Native.TryParseInput(keyboard,keyboard.Length,pointerSize,out input)||input.IsDown||Pet.PhysicalKeyId(input.VirtualKey,input.ScanCode,input.KeyFlags)!=downIdentity)throw new Exception("Raw keyboard BREAK changed identity");
            Buffer.BlockCopy(BitConverter.GetBytes((ushort)4),0,keyboard,header+2,2);
            Buffer.BlockCopy(BitConverter.GetBytes((ushort)19),0,keyboard,header+6,2);
            if(!Native.TryParseInput(keyboard,keyboard.Length,pointerSize,out input)||input.KeyFlags!=2||Pet.PhysicalKeyId(input.VirtualKey,input.ScanCode,input.KeyFlags)==Pet.PhysicalKeyId(17,29,0))throw new Exception("Raw Pause/E1 identity collided with Control");
            Buffer.BlockCopy(BitConverter.GetBytes((ushort)255),0,keyboard,header+6,2);
            if(Native.TryParseInput(keyboard,keyboard.Length,pointerSize,out input))throw new Exception("Fake raw virtual key was accepted");
            Buffer.BlockCopy(BitConverter.GetBytes((ushort)65),0,keyboard,header+6,2);
            if(Native.TryParseInput(keyboard,keyboard.Length-1,pointerSize,out input)||Native.TryParseInput(keyboard,header-1,pointerSize,out input))throw new Exception("Truncated raw packet was accepted");
            Buffer.BlockCopy(BitConverter.GetBytes(uint.MaxValue),0,keyboard,4,4);
            if(Native.TryParseInput(keyboard,keyboard.Length,pointerSize,out input))throw new Exception("Oversized raw packet was accepted");
            Buffer.BlockCopy(BitConverter.GetBytes((uint)header),0,keyboard,4,4);
            if(Native.TryParseInput(keyboard,keyboard.Length,pointerSize,out input))throw new Exception("Incomplete raw keyboard payload was accepted");

            var mouse=new byte[header+24];
            Buffer.BlockCopy(BitConverter.GetBytes((uint)mouse.Length),0,mouse,4,4);
            Buffer.BlockCopy(BitConverter.GetBytes((ushort)1),0,mouse,header,2); // Absolute movement, deliberately ignored.
            Buffer.BlockCopy(BitConverter.GetBytes((ushort)15),0,mouse,header+4,2); // Both buttons down and up in a short packet.
            Buffer.BlockCopy(BitConverter.GetBytes(int.MaxValue),0,mouse,header+12,4);
            Buffer.BlockCopy(BitConverter.GetBytes(int.MinValue),0,mouse,header+16,4);
            if(!Native.TryParseInput(mouse,mouse.Length,pointerSize,out input)||input.IsKeyboard||input.MouseButtons!=15)throw new Exception("Raw mouse button flags were not preserved");
            if(Native.TryParseInput(mouse,mouse.Length-1,pointerSize,out input))throw new Exception("Truncated raw mouse payload was accepted");
            Buffer.BlockCopy(BitConverter.GetBytes((uint)2),0,mouse,0,4);
            if(Native.TryParseInput(mouse,mouse.Length,pointerSize,out input))throw new Exception("Unregistered HID payload was accepted");
        }
        File.WriteAllText(Path.Combine(destination,"raw-input-checks.txt"),"PASS: offline x86/x64 keyboard and mouse packets, E0/BREAK/E1 identity, fake keys, short clicks, movement ignored, malformed packet bounds. No devices registered or live input generated.\r\n");
    }
    static void Run(string[] args){
        string root=AppDomain.CurrentDomain.BaseDirectory;
        if(args.Length>0&&args[0]=="--render"){
            using(var scene=new Scene()){
                string destination=args.Length>1?args[1]:root;Directory.CreateDirectory(destination);
                ValidateRawInputPackets(destination);
                var lines=new List<string>();
                int[][] inputPairs={
                    new[]{49,2,0,97,79,0}, // Number row and numeric keypad.
                    new[]{112,59,0,113,60,0}, // Different function keys.
                    new[]{91,91,1,92,92,1}, // Left and right Windows keys.
                    new[]{13,28,0,13,28,1}, // Main and keypad Enter.
                    new[]{160,42,0,161,54,0}, // Left and right Shift.
                    new[]{162,29,0,163,29,1}, // Left and right Control.
                };
                foreach(int[] pair in inputPairs){
                    int first=Pet.PhysicalKeyId(pair[0],pair[1],pair[2]);
                    int second=Pet.PhysicalKeyId(pair[3],pair[4],pair[5]);
                    if(first==second)throw new Exception("Distinct physical keys share held state");
                    if(first!=Pet.PhysicalKeyId(pair[0],pair[1],pair[2]|128|16|32))throw new Exception("Physical key identity changed on key-up");
                }
                foreach(int vk in new[]{186,187,188,189,190,219,220,221,222,37,38,39,40,35,36}){
                    string mapped=Pet.MapKey(vk,0,0);
                    if(mapped!="Typing"||Scene.VisibleKey(mapped)!=null)throw new Exception("Unmapped input must use generic typing feedback");
                    V generic=scene.TargetForInput(mapped);
                    if((Anatomy.Solve(scene.KeyShoulder,generic,Anatomy.KeyUpper,Anatomy.KeyLower,true).Tip-generic).Length>.01)throw new Exception("Unreachable generic typing pose");
                }
                File.WriteAllText(Path.Combine(destination,"input-checks.txt"),"PASS: distinct physical keys, stable key-up identity, generic punctuation and navigation feedback.\r\n");
                var expressionCheck=new TypingExpression(scene.Expressions,new Random(73));
                if(expressionCheck.Current(0)!=null||expressionCheck.Current(100)!=null)throw new Exception("Idle expression must stay neutral");
                if(!expressionCheck.Trigger(0))throw new Exception("Typing must trigger an expression");
                string firstExpression=expressionCheck.Current(0);
                for(int step=1;step<=89;step++){
                    double now=step/100.0;
                    if(expressionCheck.Trigger(now)||expressionCheck.Current(now)!=firstExpression)throw new Exception("Rapid typing changed an active expression");
                }
                if(expressionCheck.Current(1.35)!=null||expressionCheck.Trigger(1.69))throw new Exception("Expression dwell or cooldown exceeded its bounds");
                if(expressionCheck.Current(100)!=null)throw new Exception("Expression did not return to neutral without input");

                // Repeated key events every 10 ms exercise the real cooldown,
                // dwell and selection logic over many neutral/active cycles.
                expressionCheck=new TypingExpression(scene.Expressions,new Random(73));
                var seenExpressions=new HashSet<string>();
                string lastExpression=null;double started=-10;bool wasActive=false;
                int expressionCount=0;
                for(int step=0;step<=12000;step++){
                    double now=step/100.0;
                    if(expressionCheck.Trigger(now)){
                        string selected=expressionCheck.Current(now);
                        if(expressionCount>0&&(now-started<1.7-1e-8||now-started>2.81))throw new Exception("Typing expression cooldown is outside its range");
                        if(scene.Expressions.Length>1&&selected==lastExpression)throw new Exception("Expression repeated after neutral reset");
                        started=now;lastExpression=selected;seenExpressions.Add(selected);expressionCount++;wasActive=true;
                    }
                    string active=expressionCheck.Current(now);
                    if(active!=null&&active!=lastExpression)throw new Exception("Expression flickered before cooldown ended");
                    if(wasActive&&active==null){
                        double dwell=now-started;
                        if(dwell<.9-1e-8||dwell>1.36)throw new Exception("Typing expression dwell is outside its range");
                        wasActive=false;
                    }
                }
                if(seenExpressions.Count!=scene.Expressions.Length)throw new Exception("Not all supplied expressions are reachable");
                if(expressionCheck.Current(125)!=null)throw new Exception("Stopped typing must return to neutral");
                var noExpressions=new TypingExpression(new string[0],new Random(73));
                if(noExpressions.Trigger(0)||noExpressions.Current(0)!=null)throw new Exception("Empty expression set must remain neutral");
                File.WriteAllText(Path.Combine(destination,"expression-timing-checks.txt"),"PASS: 120 seconds of rapid input, stable dwell and cooldown, no consecutive repeat, all supplied expressions reached, idle returns to neutral.\r\n");
                var visibleKeys=new HashSet<string>();
                foreach(string key in scene.Targets.Keys){string visible=Scene.VisibleKey(key);if(visible!=null)visibleKeys.Add(visible);}
                if(scene.Targets.Count!=55||visibleKeys.Count!=12)throw new Exception("Compact keyboard input mapping is incomplete");
                if(Scene.VisibleKey("KeyM")!=null||Scene.VisibleKey("Num1")!=null)throw new Exception("General typing must not light a different key");
                if((scene.Targets["ShiftLeft"]-scene.Targets["ShiftRight"]).Length>.001||(scene.Targets["ControlLeft"]-scene.Targets["ControlRight"]).Length>.001)throw new Exception("Modifier aliases must share a keycap");
                if((Anatomy.Solve(scene.KeyShoulder,scene.Rest,Anatomy.KeyUpper,Anatomy.KeyLower,true).Tip-scene.Rest).Length>.01)throw new Exception("Unreachable resting hand");
                foreach(var pair in scene.Targets){
                    ArmPose pose=Anatomy.Solve(scene.KeyShoulder,pair.Value,Anatomy.KeyUpper,Anatomy.KeyLower,true);
                    if((pose.Tip-pair.Value).Length>.01)throw new Exception("Unreachable key: "+pair.Key);
                    double a=(pose.Elbow-pose.Shoulder).Length,b=(pose.Wrist-pose.Elbow).Length;
                    if(Math.Abs(Math.Sqrt(a*a+pose.ElbowDepth*pose.ElbowDepth)-Anatomy.KeyUpper)>1e-7||Math.Abs(Math.Sqrt(b*b+Math.Pow(pose.WristDepth-pose.ElbowDepth,2))-Anatomy.KeyLower)>1e-7)throw new Exception("Bone length drift");
                    V f=(pose.Wrist-pose.Elbow).Unit;
                    double wristAngle=Math.Acos(Anatomy.Clamp(f.X*pose.Direction.X+f.Y*pose.Direction.Y,-1,1))*180/Math.PI;
                    if(wristAngle>20)throw new Exception("Excess wrist bend: "+pair.Key+" "+wristAngle);
                    lines.Add(pair.Key+"\t"+pose.Angle.ToString("F3",CultureInfo.InvariantCulture)+"\t"+wristAngle.ToString("F3",CultureInfo.InvariantCulture));
                }
                double maxElbowStep=0,maxHandTurn=0;
                foreach(V from in scene.Targets.Values)foreach(V to in scene.Targets.Values){
                    ArmPose previous=Anatomy.Solve(scene.KeyShoulder,from,Anatomy.KeyUpper,Anatomy.KeyLower,true);
                    for(int step=1;step<=60;step++){
                        ArmPose current=Anatomy.Solve(scene.KeyShoulder,V.Lerp(from,to,step/60.0),Anatomy.KeyUpper,Anatomy.KeyLower,true);
                        maxElbowStep=Math.Max(maxElbowStep,(current.Elbow-previous.Elbow).Length);
                        double turn=Math.Acos(Anatomy.Clamp(current.Direction.X*previous.Direction.X+current.Direction.Y*previous.Direction.Y,-1,1))*180/Math.PI;
                        maxHandTurn=Math.Max(maxHandTurn,turn);previous=current;
                    }
                }
                if(maxElbowStep>5||maxHandTurn>12)throw new Exception("Discontinuous joint solution: "+maxElbowStep+" / "+maxHandTurn);
                File.WriteAllText(Path.Combine(destination,"continuity.txt"),"55 keys, 3025 transitions, 181500 intermediate poses.\r\nMaximum elbow step: "+maxElbowStep+" px\r\nMaximum wrist orientation step: "+maxHandTurn+" degrees\r\n");
                if(scene.Expressions.Length!=3)throw new Exception("The three supplied expressions are required");
                foreach(string face in scene.Expressions)using(var b=scene.Render(MouseMotion.Center,scene.Targets["KeyS"],new[]{"KeyS"},false,false,2,face))b.Save(Path.Combine(destination,"jointed-"+face+".png"));
                using(var b=scene.Render(MouseMotion.Center,scene.Rest,new string[0],false,false,2))b.Save(Path.Combine(destination,"jointed-idle.png"));
                using(var b=scene.Render(MouseMotion.Center,scene.Targets["KeyW"],new[]{"ControlLeft","KeyW"},false,false,2))b.Save(Path.Combine(destination,"jointed-compact-combo.png"));
                foreach(string key in scene.Targets.Keys)using(var b=scene.Render(MouseMotion.Center,scene.Targets[key],new[]{key},false,false,2))b.Save(Path.Combine(destination,"jointed-"+key+".png"));
                for(int i=0;i<24;i++){
                    double t=i*Math.PI*2/24;V m=MouseMotion.Center+new V(Math.Cos(t)*MouseMotion.HalfWidth,Math.Sin(t)*MouseMotion.HalfHeight);
                    ArmPose p=Anatomy.Solve(scene.MouseShoulder,m+new V(-1,-4),32,31,false);
                    if((p.Tip-(m+new V(-1,-4))).Length>.01)throw new Exception("Unreachable mouse pose");
                    V reach=p.Wrist-p.Shoulder,elbow=p.Elbow-p.Shoulder;
                    if(reach.X*elbow.Y-reach.Y*elbow.X<=0||p.Elbow.X>=p.Shoulder.X-8)throw new Exception("Mouse elbow must open outward");
                    using(var b=scene.Render(m,scene.Rest,new string[0],false,false,2))b.Save(Path.Combine(destination,"jointed-motion-"+i.ToString("D2")+".png"));
                }
                // Verify actual screen mapping, including a monitor left of the
                // primary display. No system input is generated by these checks.
                foreach(Rectangle screen in new[]{new Rectangle(0,0,1920,1080),new Rectangle(-2560,-1440,2560,1440)}){
                    V first=MouseMotion.Target(screen.Location,screen);
                    V last=MouseMotion.Target(new Point(screen.Right-1,screen.Bottom-1),screen);
                    if((first-(MouseMotion.Center-new V(MouseMotion.HalfWidth,MouseMotion.HalfHeight))).Length>.001
                        ||(last-(MouseMotion.Center+new V(MouseMotion.HalfWidth,MouseMotion.HalfHeight))).Length>.001)throw new Exception("Mouse screen mapping lost its full movement range");
                }
                if((MouseMotion.Target(new Point(10,10),new Rectangle(10,10,1,1))-MouseMotion.Center).Length>.001)throw new Exception("Degenerate screen mapping is not centered");
                double minAbduction=90,maxAbduction=0;
                int mousePositions=0;
                for(double dx=-MouseMotion.HalfWidth;dx<=MouseMotion.HalfWidth;dx+=.5)for(double dy=-MouseMotion.HalfHeight;dy<=MouseMotion.HalfHeight;dy+=.5){
                    V tip=MouseMotion.Center+new V(dx-1,dy-4);
                    ArmPose p=Anatomy.Solve(scene.MouseShoulder,tip,32,31,false);
                    mousePositions++;
                    V arm=p.Elbow-p.Shoulder,reach=p.Wrist-p.Shoulder;
                    double abduction=Math.Atan2(Math.Abs(arm.X),arm.Y)*180/Math.PI;
                    minAbduction=Math.Min(minAbduction,abduction);maxAbduction=Math.Max(maxAbduction,abduction);
                    if(abduction>35||arm.X>=0||reach.X*arm.Y-reach.Y*arm.X<=0)throw new Exception("Mouse shoulder exceeds its relaxed outward range");
                    if((p.Tip-tip).Length>.01)throw new Exception("Mouse corner unreachable");
                    V forearm=(p.Wrist-p.Elbow).Unit;
                    if(forearm.X*p.Direction.X+forearm.Y*p.Direction.Y<.99999)throw new Exception("Mouse wrist lost alignment");
                }
                File.WriteAllText(Path.Combine(destination,"mouse-shoulder-check.txt"),mousePositions+" mouse positions including all corners. Travel: "+(MouseMotion.HalfWidth*2)+" x "+(MouseMotion.HalfHeight*2)+" scene pixels. Projected upper-arm angle from downward: "+minAbduction+" to "+maxAbduction+" degrees.\r\n");
                for(int horizontal=-1;horizontal<=1;horizontal+=2)for(int vertical=-1;vertical<=1;vertical+=2){
                    V m=MouseMotion.Center+new V(horizontal*MouseMotion.HalfWidth,vertical*MouseMotion.HalfHeight);
                    using(var b=scene.Render(m,scene.Rest,new string[0],false,false,2))b.Save(Path.Combine(destination,"mouse-corner-"+horizontal+"-"+vertical+".png"));
                }
                string[] sequence={"KeyA","Space","Return","KeyW","ShiftLeft","KeyM"};
                for(int i=0;i<72;i++){
                    int part=i/12;double t=(i%12)/12.0;t=t*t*(3-2*t);
                    V key=V.Lerp(scene.Targets[sequence[part]],scene.Targets[sequence[(part+1)%sequence.Length]],t);
                    V m=MouseMotion.Center+new V(Math.Cos(i*Math.PI/36)*MouseMotion.HalfWidth,Math.Sin(i*Math.PI/36)*MouseMotion.HalfHeight);
                    string face=i%24<16?scene.Expressions[(i/24)%scene.Expressions.Length]:null;
                    using(var b=scene.Render(m,key,new[]{sequence[part]},false,false,2,face))b.Save(Path.Combine(destination,"jointed-demo-"+i.ToString("D2")+".png"));
                }
                File.WriteAllLines(Path.Combine(destination,"jointed-validation.tsv"),lines);
            }
            return;
        }
        bool created;
        using(var mutex=new System.Threading.Mutex(true,"Local\\DafeiyuJointedPet",out created)){
            if(!created){Native.ShowExistingPet();return;}
            Native.SetProcessDPIAware();Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
            Application.ThreadException+=delegate(object sender,System.Threading.ThreadExceptionEventArgs e){LogError(e.Exception);MessageBox.Show("桌宠遇到错误："+e.Exception.Message,Pet.WindowTitle,MessageBoxButtons.OK,MessageBoxIcon.Error);Application.Exit();};
            var app=new Pet(new Scene());
            Application.Run(app);
        }
    }
}
