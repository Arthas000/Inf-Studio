using UnityEngine;
using InFalsusStudio.Core;

namespace InFalsusStudio
{
    public sealed partial class StudioBootstrap
    {
        private float lastWheelDelta;
        private int lastWheelSteps;
        private readonly WheelStepAccumulator browseWheel=new WheelStepAccumulator(3);
        private void BrowseWheel(float deltaY)
        {
            lastWheelDelta=deltaY;int steps=browseWheel.Consume(deltaY);lastWheelSteps=steps;
            if(steps!=0)BrowseSteps(steps);
        }
        private void BrowseSteps(int steps)
        {
            if(authoringGrid==null||steps==0)return;
            exactGroupJump=double.NaN;transport.Pause();
            // Strict next/previous grid line, even if playback stopped between lines.
            // No first rounding to nearest here: that can skip the desired first line.
            double target=authoringGrid.Step(CurrentMs,steps,Division);
            transport.Seek(target);timeText=target.ToString("0",CI);
            status="Grid navigation: "+target.ToString("0",CI)+" ms | 1/"+Division+" beat. Wheel UP = forward.";
        }
        private void EnsureBrowseGridLock()
        {
            if(transport==null||transport.Playing||authoringGrid==null||!showGrid)return;
            if(PreserveExactGroupJump())return;
            double target=authoringGrid.Nearest(CurrentMs,Division).TimeMs;
            if(System.Math.Abs(target-CurrentMs)>.0001){transport.Seek(target);timeText=target.ToString("0",CI);}
            // No source note is changed. A raw Apply is still allowed off-grid.
        }
        private void DrawNavigationOptions()
        {
            GUILayout.Space(8);
            GUILayout.Label("Wheel: up = next grid line; down = previous");
            GUILayout.BeginHorizontal();
            GUILayout.Label("Input units per detent",GUILayout.Width(174));
            if(GUILayout.Button("1"))QueueGui(()=>browseWheel.Configure(1));
            if(GUILayout.Button("3 (Windows)"))QueueGui(()=>browseWheel.Configure(3));
            GUILayout.EndHorizontal();
            GUILayout.Label("Current scale: "+browseWheel.UnitsPerStep.ToString("0.##",CI)+" | partial deltas are accumulated.");
            GUILayout.Label("Last wheel delta: "+lastWheelDelta.ToString("0.###",CI)+" => "+lastWheelSteps+" grid steps");
            GUILayout.Label("Playback is continuous. With Beat grid ON, pause/seek returns the playhead to a grid line. Raw source time is not rounded.");
        }
    }
}
