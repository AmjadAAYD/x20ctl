using System;
using System.Drawing;
using System.Windows.Forms;

namespace InputDiagnostic {
    public sealed class ControllerView : Control {
        public State State;public bool Connected;public PressTracker Tracker;public double Now;
        public ControllerView(){DoubleBuffered=true;BackColor=Color.FromArgb(17,32,50);}
        protected override void OnPaint(PaintEventArgs e) {
            base.OnPaint(e);var g=e.Graphics;g.SmoothingMode=System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.ScaleTransform(1,Math.Min(1,Height/355f));
            var keys=new[]{"A","B","X","Y","LB","RB","L3","R3","Start","Back","dpad_up","dpad_right","dpad_down","dpad_left"};
            float cell=Width/7f;
            using(var font=new Font("Segoe UI",10,FontStyle.Bold))using(var small=new Font("Segoe UI",8))
            using(var text=new SolidBrush(Color.FromArgb(229,239,249)))using(var muted=new SolidBrush(Color.FromArgb(167,186,208))) {
                for(int i=0;i<keys.Length;i++) {
                    string key=keys[i];bool down=Connected && (State.Pad.Buttons&Analysis.Masks[key])==Analysis.Masks[key];
                    var box=new RectangleF(i%7*cell+4,i/7*65+8,cell-8,57);
                    using(var fill=new SolidBrush(down?Color.FromArgb(30,121,94):Color.FromArgb(29,49,73)))g.FillRectangle(fill,box);
                    g.DrawString(key.Replace("dpad_",""),font,text,box.X+6,box.Y+5);
                    g.DrawString(Connected && Tracker!=null?Tracker.Duration(key,Now):"No signal",small,muted,box.X+6,box.Y+29);
                }
                for(int i=0;i<2;i++) {
                    short x=i==0?State.Pad.LX:State.Pad.RX,y=i==0?State.Pad.LY:State.Pad.RY;
                    float cx=Width*(i==0?.25f:.75f),cy=198;
                    using(var pen=new Pen(Color.FromArgb(98,130,165),2)){g.DrawEllipse(pen,cx-34,cy-34,68,68);g.DrawLine(pen,cx-34,cy,cx+34,cy);g.DrawLine(pen,cx,cy-34,cx,cy+34);}
                    using(var dot=new SolidBrush(Color.FromArgb(59,203,163)))g.FillEllipse(dot,cx+x/32768f*28-5,cy-y/32768f*28-5,10,10);
                    g.DrawString((i==0?"Left":"Right")+" stick: "+x+", "+y,small,text,cx-105,239);
                    g.DrawString((x/32768.0).ToString("0.00")+", "+(y/32768.0).ToString("0.00")+" normalized",small,muted,cx-105,257);
                }
                for(int i=0;i<2;i++) {
                    int value=i==0?State.Pad.LT:State.Pad.RT;float left=Width*(i==0?.02f:.52f),width=Width*.46f;
                    using(var empty=new SolidBrush(Color.FromArgb(38,59,81)))using(var fill=new SolidBrush(Color.FromArgb(59,203,163))) {
                        g.FillRectangle(empty,left,290,width,12);g.FillRectangle(fill,left,290,width*value/255,12);
                    }
                    string key=i==0?"LT":"RT";
                    g.DrawString(key+"  "+value+" / 255  |  "+(100*value/255)+"%",font,text,left,310);
                    g.DrawString(Connected && Tracker!=null?Tracker.Duration(key,Now):"No signal",small,muted,left,330);
                }
            }
        }
    }
}
