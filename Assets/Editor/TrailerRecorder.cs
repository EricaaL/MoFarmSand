using System.IO;
using UnityEditor;
using UnityEditor.Recorder;
using UnityEditor.Recorder.Encoder;
using UnityEditor.Recorder.Input;
using UnityEngine;

/// <summary>Records the Game view to H.264 MP4 via Unity Recorder. Output: Recordings/MoFarmSand_Take_*.mp4</summary>
public static class TrailerRecorder
{
    private const string PendingKey = "MoFarmSand.RecordOnPlay";
    private static RecorderController controller;

    [InitializeOnLoadMethod]
    private static void Hook()
    {
        EditorApplication.playModeStateChanged += state =>
        {
            if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(PendingKey, false))
            {
                SessionState.SetBool(PendingKey, false);
                StartRecording();
            }
        };
    }

    [MenuItem("MoFarmSand/Record Trailer Take")]
    public static void RecordTake()
    {
        if (EditorApplication.isPlaying) { StartRecording(); return; }
        SessionState.SetBool(PendingKey, true);
        EditorApplication.isPlaying = true;
    }

    [MenuItem("MoFarmSand/Stop Recording")]
    public static void StopRecording()
    {
        if (controller != null && controller.IsRecording()) controller.StopRecording();
        controller = null;
    }

    private static void StartRecording()
    {
        if (controller != null && controller.IsRecording()) return;

        string dir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Recordings"));
        Directory.CreateDirectory(dir);
        string file = Path.Combine(dir, "MoFarmSand_Take_" + System.DateTime.Now.ToString("yyyyMMdd_HHmmss"));

        RecorderControllerSettings settings = ScriptableObject.CreateInstance<RecorderControllerSettings>();
        MovieRecorderSettings movie = ScriptableObject.CreateInstance<MovieRecorderSettings>();
        movie.name = "MoFarmSand Trailer";
        movie.Enabled = true;
        movie.EncoderSettings = new CoreEncoderSettings
        {
            Codec = CoreEncoderSettings.OutputCodec.MP4,   // H.264 in MP4
            EncodingQuality = CoreEncoderSettings.VideoEncodingQuality.High
        };
        movie.ImageInputSettings = new GameViewInputSettings { OutputWidth = 1920, OutputHeight = 1080 };
        movie.AudioInputSettings.PreserveAudio = true;
        movie.OutputFile = file;

        settings.AddRecorderSettings(movie);
        settings.SetRecordModeToManual();
        settings.FrameRate = 30f;

        controller = new RecorderController(settings);
        controller.PrepareRecording();
        controller.StartRecording();
        Debug.Log("[TrailerRecorder] Recording to " + file + ".mp4");
    }
}
