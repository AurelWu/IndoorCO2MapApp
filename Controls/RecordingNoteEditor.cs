using Microsoft.Maui.Controls;

namespace IndoorCO2MapAppV2.Controls
{
    /// <summary>
    /// The custom-note field on the measurement pages. A plain Editor; the type exists so
    /// the Android Enter-key customisation (see NoteEditorEnterKey) can target exactly these
    /// two fields and no other Editor.
    /// </summary>
    public class RecordingNoteEditor : Editor
    {
    }
}
