using System;
using System.Collections.Generic;
using System.Linq;

namespace InFalsusStudio.Core
{
    /// <summary>Browse source Sky start times, never rendered Z or combo tick times.
    /// Missing/negative IDs are intentionally not aggregated into a synthetic group.</summary>
    public static class SkyGroupNavigation
    {
        public static SpcEvent[] Members(IEnumerable<SpcEvent> events,int group)
        {
            if(group<0)return new SpcEvent[0];
            return events.Where(n=>n.Kind==EventKind.SkyArea&&SkyGroups.GroupOf(n)==group)
                .OrderBy(n=>n.TimeMs).ThenBy(n=>n.SourceId).ToArray();
        }
        public static SpcEvent Neighbor(IEnumerable<SpcEvent> events,int group,double now,int direction)
        {
            if(double.IsNaN(now)||double.IsInfinity(now))throw new ArgumentOutOfRangeException("now");
            var members=Members(events,group);
            if(direction>0)return members.FirstOrDefault(n=>n.TimeMs>now+1e-7);
            if(direction<0)return members.Where(n=>n.TimeMs<now-1e-7).OrderByDescending(n=>n.TimeMs).ThenBy(n=>n.SourceId).FirstOrDefault();
            return null;
        }
    }
    /// <summary>Trigger times follow chart transport; sample playback speed is ALWAYS 1x.</summary>
    public static class KeySoundClock
    {
        public const float Pitch=1f;
        public static double OneShotEnd(double startDsp,double clipSeconds)
        {return startDsp+Math.Max(0,clipSeconds);}
        public static int LoopSample(double chartElapsedMs,double chartRate,int frequency,int sampleCount)
        {
            if(chartRate<=0||double.IsNaN(chartRate)||double.IsInfinity(chartRate)||frequency<=0||sampleCount<=0||double.IsNaN(chartElapsedMs)||double.IsInfinity(chartElapsedMs))return 0;
            // Reconstruct elapsed real seconds within this constant-rate audition epoch.
            double frames=Math.Max(0,chartElapsedMs)*.001/chartRate*frequency;
            if(double.IsInfinity(frames))return 0;return (int)(frames%sampleCount);
        }
    }
    /// <summary>Check allocation bounds before Unity decodes user PNG/JPEG data.</summary>
    public static class LocalImageSize
    {
        public static void Validate(byte[] data,int maxSide,long maxPixels)
        {
            if(data==null||data.Length<24)throw new System.IO.InvalidDataException("Image header is missing.");
            long width=0,height=0;
            if(data[0]==137&&data[1]==80&&data[2]==78&&data[3]==71&&data[12]==73&&data[13]==72&&data[14]==68&&data[15]==82)
            {width=U32(data,16);height=U32(data,20);}
            else if(data[0]==255&&data[1]==216)
            {
                int i=2;
                while(i+3<data.Length)
                {
                    if(data[i++]!=255)continue;
                    while(i<data.Length&&data[i]==255)i++;
                    if(i>=data.Length)break;
                    int marker=data[i++];
                    if(marker==0||marker==216||marker==1||marker>=208&&marker<=215)continue;
                    if(marker==217||marker==218||i+1>=data.Length)break;
                    int len=data[i]*256+data[i+1];if(len<2||len>data.Length-i)break;
                    bool sof=marker>=192&&marker<=207&&marker!=196&&marker!=200&&marker!=204;
                    if(sof&&len>=8){height=data[i+3]*256+data[i+4];width=data[i+5]*256+data[i+6];break;}
                    i+=len;
                }
            }
            if(width<=0||height<=0||width>maxSide||height>maxSide||width*height>maxPixels)
                throw new System.IO.InvalidDataException("PNG/JPEG dimensions missing or image exceeds supported 8192-side / 32MP bounds.");
        }
        private static long U32(byte[] data,int i){return ((long)data[i]<<24)|((long)data[i+1]<<16)|((long)data[i+2]<<8)|data[i+3];}
    }
    public struct UvCrop
    {public double X,Y,Width,Height;public UvCrop(double x,double y,double w,double h){X=x;Y=y;Width=w;Height=h;}}
    public static class BackgroundFit
    {
        /// <summary>Centered cover, no distortion: a matching 16:9 texture maps to [0,1]^2.</summary>
        public static UvCrop Cover(double textureAspect,double viewportAspect)
        {
            if(textureAspect<=0||viewportAspect<=0||double.IsNaN(textureAspect)||double.IsNaN(viewportAspect)||double.IsInfinity(textureAspect)||double.IsInfinity(viewportAspect))throw new ArgumentOutOfRangeException("aspect");
            if(textureAspect>viewportAspect){double w=viewportAspect/textureAspect;return new UvCrop((1-w)*.5,0,w,1);}
            double h=textureAspect/viewportAspect;return new UvCrop(0,(1-h)*.5,1,h);
        }
    }
}
