using System;
using System.IO;
using System.Linq;
using UnityEngine;
using InFalsusStudio.Core;

namespace InFalsusStudio
{
    public sealed partial class StudioBootstrap
    {
        private ManualSongSession projectSession;
        private bool projectDirty;
        private ProvisionalComboTimeline comboTimeline;
        private bool HasUnsavedEdits {get{return songProject!=null?projectDirty:documentDirty;}}
        private void SyncActiveSongDraft()
        {
            if(songProject==null||History==null)return;
            int index=songProject.ActiveDifficulty;
            songProject.Difficulties[index].Document=History.Document.Clone();
            songHistories[index]=History;songProject.AudioOffsetMs=transport.AudioOffsetMs;
        }
        private void RefreshProjectDirty(){projectDirty=projectSession!=null&&projectSession.HasChanges;}
        private bool HasPendingMetadataInputs()
        {
            if(songProject==null)return false;
            if(hudField!=HudField.None)return hudEditValue!=(hudField==HudField.Title?songProject.Title:hudField==HudField.Artist?songProject.Artist:songProject.Difficulties[songProject.ActiveDifficulty].Rating);
            return showSongPanel&&(songTitleInput!=songProject.Title||songArtistInput!=songProject.Artist||songRatingInput!=songProject.Difficulties[songProject.ActiveDifficulty].Rating);
        }
        private void StageMetadataInputsForSave()
        {
            if(songProject==null)return;
            int index=songProject.ActiveDifficulty;
            if(hudField!=HudField.None)
            {
                if(hudEditProject!=songProject||hudEditDifficulty!=index)throw new InvalidOperationException("Metadata draft belongs to another song/difficulty.");
                ApplyMetadata(songProject,index,hudField==HudField.Title?hudEditValue:songProject.Title,
                    hudField==HudField.Artist?hudEditValue:songProject.Artist,hudField==HudField.Level?hudEditValue:songProject.Difficulties[index].Rating);
                CancelHudField();GUI.FocusControl(null);
            }
            else if(showSongPanel&&HasPendingMetadataInputs())ApplyMetadata(songProject,index,songTitleInput,songArtistInput,songRatingInput);
        }
        private void DrawComboDiagnostics()
        {
            // Fixed number of GUILayout calls regardless of playback time or selection.
            var timeline=comboTimeline;bool valid=timeline!=null&&timeline.Supported;
            var now=valid?timeline.At(CurrentMs):new ComboAtTime();var selected=valid?timeline.Find(selectedId):null;
            GUILayout.Label(valid?"EST. COMBO @ "+CurrentMs.ToString("0.###",CI)+" ms: "+now.Total+" / "+timeline.Totals.Total:"Provisional count unavailable for this chart.");
            GUILayout.Label("At playhead: Tap "+now.Tap+" | Hold "+now.Hold+" | Sky "+now.SkyArea+" | Flick "+now.Flick);
            GUILayout.Label(selected==null?"Select a note for candidate ticks / start BPM.":
                "Selected ID "+selected.Id+": "+selected.At(CurrentMs)+" / "+selected.Total+" | onset BPM "+selected.StartBpm.ToString("0.###",CI)+" | interval "+selected.IntervalMs+" ms"+(selected.CrossesBpm?" | crosses BPM (start-BPM hypothesis)":""));
            GUILayout.Label("Profile A: Hold=head+periodic+tail; Sky=start+periodic. Tick TIMES and BPM-crossing behavior are provisional. track affects visuals only.");
            if(GUILayout.Button("Export count report at playhead..."))QueueGui(ExportComboReport);
        }
        private void ExportComboReport()
        {
            if(comboTimeline==null||!comboTimeline.Supported){status="No supported provisional count to export.";return;}
            string path=StudioFileDialogs.Save("Export provisional combo report","","combo_"+CurrentMs.ToString("0",CI)+"ms","json");
            if(string.IsNullOrEmpty(path))return;
            try
            {
                string full=Path.GetFullPath(path);
                // Do not let a diagnostic export overwrite song metadata or any project source.
                if(songProject!=null&&full.StartsWith(songProject.Root+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))
                    throw new IOException("Choose a diagnostics location OUTSIDE the song folder.");
                if(loadedPath.Length>0&&full.Equals(Path.GetFullPath(loadedPath),StringComparison.OrdinalIgnoreCase))throw new IOException("Cannot overwrite the loaded chart.");
                File.WriteAllText(full,comboTimeline.Report(CurrentMs,SongFolderProject.Sha256(History.Document.ToBytes())));
                status="Exported provisional count report (not a project save): "+full;
            }
            catch(Exception ex){status="Report export failed: "+ex.Message;}
        }
    }
}
