﻿namespace hajk.Models.DefaultPrefSettings
{
    public class DefaultPrefSettings
    {
        public bool BackupPreferences { get; set; }
        public bool BackupRouteTrackData { get; set; }
        public bool BackupPOIData { get; set; }
        public bool BackupMapTiles { get; set; }
        public bool BackupElevationData { get; set; }
        public bool MapLockNorth { get; set; }
        public bool TrackLocation { get; set; }
        public bool RecordingTrack { get; set; }
        public bool DrawPOIOnGui { get; set; }
        public string? mapUTMZone { get; set; }
        public long mapScale { get; set; }
        public int freq_s_OffRoute { get; set; }
        public int KeepNBackups { get; set; }
        public bool EnableOffRouteWarning { get; set; }
        public int OffTrackDistanceWarning_m { get; set; }
        public int OffTrackRouteSnooze_m { get; set; }
        public bool DrawTracksOnGui { get; set; }
        public bool DrawTrackOnGui { get; set; }
        public int freq { get; set; }
        public string? OSM_Browse_Source { get; set; }
        public string? OSM_BulkDownload_Source { get; set; }
        public string? MapboxToken { get; set; }
        public string? ThunderforestToken { get; set; }
        public string? CustomServerURL { get; set; }
        public string? CustomToken { get; set; }
    }
}