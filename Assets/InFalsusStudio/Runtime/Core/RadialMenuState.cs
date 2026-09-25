using System;

namespace InFalsusStudio.Core
{
    public enum RadialLevel { Home, Point, TapWidth, HoldWidth, FlickDirection, Selection }
    public enum RadialItem { Blank, EnterPoint, Grid, Tap, Hold, Sky, Flick, NoCreate, Exit, Width1, Width2, Width3, Width4, Left, Right, Delete, Mirror, Cut, Copy, Align }
    public struct RadialChoice
    {public bool Valid;public RadialLevel Level;public RadialItem Item;}

    // Right-button protocol. The view supplies hit rectangles; no left click can execute.
    public sealed class RadialMenuState
    {
        public const double LongPressSeconds=0, SubmenuSeconds=.5;
        public bool InPointMode {get;set;}
        public bool Pressed {get;private set;}
        public bool Visible {get;private set;}
        public RadialLevel Level {get;private set;}
        public RadialItem Hovered {get;private set;}
        private double hoverAt;
        public void Press(double now){Press(now,false);}
        public void Press(double now,bool selected)
        {Pressed=true;Visible=true;hoverAt=now;Hovered=RadialItem.Blank;Level=selected?RadialLevel.Selection:InPointMode?RadialLevel.Point:RadialLevel.Home;}
        // true means a submenu opened; the view must move its CENTER to the old button's center.
        public bool Tick(double now,RadialItem hover)
        {
            if(!Pressed)return false;
            if(!Allowed(Level,hover))hover=RadialItem.Blank;
            if(hover!=Hovered){Hovered=hover;hoverAt=now;}
            if(Level==RadialLevel.Point && now-hoverAt>=SubmenuSeconds &&
                (hover==RadialItem.Tap||hover==RadialItem.Hold||hover==RadialItem.Flick))
            {
                Level=hover==RadialItem.Tap?RadialLevel.TapWidth:hover==RadialItem.Hold?RadialLevel.HoldWidth:RadialLevel.FlickDirection;
                Hovered=RadialItem.Blank;hoverAt=now;return true;
            }
            return false;
        }
        public double HoverProgress(double now)
        {return Level==RadialLevel.Point&&(Hovered==RadialItem.Tap||Hovered==RadialItem.Hold||Hovered==RadialItem.Flick)?Math.Max(0,Math.Min(1,(now-hoverAt)/SubmenuSeconds)):0;}
        public RadialChoice Release(RadialItem hover)
        {
            var result=new RadialChoice{Valid=Pressed&&Visible&&hover!=RadialItem.Blank&&Allowed(Level,hover),Level=Level,Item=hover};
            Cancel();
            if(result.Valid && (result.Level==RadialLevel.Home||result.Level==RadialLevel.Selection) && hover==RadialItem.EnterPoint)InPointMode=true;
            if(result.Valid && result.Level==RadialLevel.Point && hover==RadialItem.Exit)InPointMode=false;
            return result;
        }
        public void Cancel(){Pressed=false;Visible=false;Hovered=RadialItem.Blank;}
        public static bool Allowed(RadialLevel level,RadialItem item)
        {
            if(item==RadialItem.Blank)return true;
            switch(level)
            {
                case RadialLevel.Selection:return item==RadialItem.EnterPoint||item==RadialItem.Grid||item==RadialItem.Delete||item==RadialItem.Mirror||item==RadialItem.Cut||item==RadialItem.Copy||item==RadialItem.Align;
                case RadialLevel.Home:return item==RadialItem.EnterPoint||item==RadialItem.Grid;
                case RadialLevel.Point:return item==RadialItem.Tap||item==RadialItem.Hold||item==RadialItem.Sky||item==RadialItem.Flick||item==RadialItem.NoCreate||item==RadialItem.Exit||item==RadialItem.Grid;
                case RadialLevel.TapWidth:case RadialLevel.HoldWidth:return item>=RadialItem.Width1&&item<=RadialItem.Width4;
                default:return item==RadialItem.Left||item==RadialItem.Right;
            }
        }
    }
}
