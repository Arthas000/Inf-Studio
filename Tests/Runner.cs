using System;
using System.IO;
using InFalsusStudio.Core;
public static class Runner
{
    public static int Main(string[] args)
    {
        try { string path=args.Length>0?args[0]:"Assets/InFalsusStudio/Resources/InFalsusStudio/LightsOut.spc.txt";Console.WriteLine(CoreSelfTests.Run(File.ReadAllBytes(path)));
            if(Directory.Exists("Research/CountSamples"))Console.WriteLine(Release05SelfTests.RunSamples("Research",File.ReadAllText("Assets/InFalsusStudio/Resources/InFalsusStudio/ComboReferences.json")));
            return 0; }
        catch(Exception ex){Console.Error.WriteLine(ex);return 1;}
    }
}
