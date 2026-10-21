using Android.Content;
using Android.Text;
using Android.Views;
using Android.Views.InputMethods;
using Android.Widget;
using IndoorCO2MapAppV2.Controls;
using IndoorCO2MapAppV2.PersistentData;
using Microsoft.Maui.Handlers;

namespace IndoorCO2MapAppV2.Platforms.Android.Handlers
{
    /// <summary>
    /// Makes Enter in the recording note close the keyboard instead of adding a line break.
    /// A multi-line field on Android gets no "Done" key, so system back was the only way out
    /// — which users without a visible back button (gesture navigation) couldn't find.
    /// The "Enter adds a line break" setting restores the old behaviour.
    /// </summary>
    internal static class NoteEditorEnterKey
    {
        public static void Register()
        {
            // Appended to the Keyboard mapping so it runs after MAUI has set the input type,
            // rather than being overwritten by it. Pages call UpdateValue(Keyboard) on
            // appearing, so a changed setting takes effect without a restart.
            EditorHandler.Mapper.AppendToMapping(nameof(IEditor.Keyboard), (handler, view) =>
            {
                if (view is not RecordingNoteEditor || handler.PlatformView is not EditText et)
                    return;

                if (UserSettings.Instance.NoteEnterAddsLineBreak)
                {
                    // MAUI's own mapping just restored the multi-line input type; only our
                    // IME action needs undoing.
                    et.ImeOptions = ImeAction.Unspecified;
                }
                else
                {
                    // Tell the keyboard it's a single-line field so it shows ✓/Done, while the
                    // view itself stays multi-line and long notes still wrap.
                    et.SetRawInputType(et.InputType & ~InputTypes.TextFlagMultiLine);
                    et.ImeOptions = ImeAction.Done;
                }

                et.EditorAction -= OnEditorAction;
                et.EditorAction += OnEditorAction;
            });
        }

        private static void OnEditorAction(object? sender, TextView.EditorActionEventArgs e)
        {
            // Xamarin defaults Handled to true; anything we don't consume must be passed on.
            e.Handled = false;
            if (UserSettings.Instance.NoteEnterAddsLineBreak || sender is not EditText et)
                return;

            // Some keyboards send a raw Enter key event instead of the IME action.
            bool done = e.ActionId == ImeAction.Done
                || (e.Event is { KeyCode: Keycode.Enter, Action: KeyEventActions.Down });
            if (!done) return;

            e.Handled = true;
            var imm = et.Context?.GetSystemService(Context.InputMethodService) as InputMethodManager;
            imm?.HideSoftInputFromWindow(et.WindowToken, HideSoftInputFlags.None);
            et.ClearFocus();
        }
    }
}
