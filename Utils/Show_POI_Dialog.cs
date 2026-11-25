// Reusable "POI" dialog: title + message + Name (single line) + Description (multiline) + symbol dropdown (Spinner)
// + up to three buttons.
// Entries and icons come from POISymbols (the single source of truth), so nothing is duplicated here.
// The icons are loaded from embedded resources of the assembly this class lives in.

using Android.App;
using Android.Content;
using Android.Util;
using Android.Views;
using Android.Widget;
using Microsoft.Maui.ApplicationModel;
using AView = Android.Views.View;
using AViewGroup = Android.Views.ViewGroup;

/// <summary>
/// Confirmed   = true when the positive button was pressed.
/// Selected    = the entry selected in the dropdown (null when cancelled).
/// KeptCurrent = true when editing and the "current symbol" entry was left selected, i.e. the POI's
///               own symbol (null or not in POISymbols) must NOT be overwritten.
/// Saved       = true when the neutral button was pressed (only if neutralText was supplied).
/// Name        = text in the Name field, exactly as entered (empty when cancelled).
/// Description = text in the Description field, exactly as entered (empty when cancelled).
/// Cancel / back = Confirmed false, Saved false.
/// </summary>
public sealed record PoiDialogResult(
    bool Confirmed,
    POISymbol? Selected,
    bool KeptCurrent = false,
    bool Saved = false,
    string Name = "",
    string Description = "");

public class Show_POI_Dialog
{
    private readonly Activity? activity;

    public Show_POI_Dialog(Activity? activity)
    {
        this.activity = activity;
    }

    /// <summary>
    /// Shows the dialog. Returns null if no usable activity or no symbols were available.
    /// selectedSymbol: symbol to preselect (use the POI's current Symbol when editing).
    /// editing: when true and selectedSymbol is null or not in POISymbols, an extra first entry
    ///          representing the current value is added and preselected, so pressing a button without
    ///          touching the dropdown does not change the POI's symbol (see PoiDialogResult.KeptCurrent).
    /// neutralText: optional third button (e.g. "SAVE"); reported as PoiDialogResult.Saved.
    /// initialName: text the Name field (single line) starts with (use the POI's current Name when editing).
    /// initialDescription: text the Description field (multiline) starts with (use the POI's current Description when editing).
    /// When not editing and selectedSymbol is null or not found, the first entry in POISymbols.All is preselected.
    /// </summary>
    public async Task<PoiDialogResult?> ShowPoiDialog(
        string title,
        string message,
        string? selectedSymbol = null,
        bool editing = false,
        string positiveText = "OK",
        string negativeText = "Cancel",
        string? neutralText = null,
        bool setCancelable = false,
        string? initialName = null,
        string? initialDescription = null)
    {
        var registry = POISymbols.All;
        if (registry.Count == 0)
        {
            Serilog.Log.Error("POI dialog: POISymbols is empty");
            return null;
        }

        //Work on a copy, the editing placeholder must not end up in POISymbols
        var symbols = new List<POISymbol>(registry);

        //Preselect
        int index = 0;
        bool found = false;
        if (selectedSymbol != null)
        {
            for (int i = 0; i < symbols.Count; i++)
            {
                if (string.Equals(symbols[i].Symbol, selectedSymbol, StringComparison.OrdinalIgnoreCase))
                {
                    index = i;
                    found = true;
                    break;
                }
            }
        }

        //Editing a POI whose symbol is null or unknown: add an entry for it, so a button press keeps it as is
        bool placeholder = false;
        if (editing && !found)
        {
            symbols.Insert(0, new POISymbol(selectedSymbol ?? "(no symbol)", POISymbols.DefaultIcon));
            index = 0;
            placeholder = true;
        }

        //Same intent as Show_Dialog (wait for a usable activity), but without blocking a thread
        Activity? act = activity;
        for (int i = 0; i < 100 && (act == null || act.IsFinishing); i++)
        {
            act = await Platform.WaitForActivityAsync();
            if (act != null && act.IsFinishing)
            {
                await Task.Delay(10);
            }
        }

        if (act == null || act.IsFinishing)
        {
            Serilog.Log.Fatal("POI dialog: no usable activity");
            return null;
        }

        var ctx = act;
        var tcs = new TaskCompletionSource<PoiDialogResult?>();

        //All views must be created on the UI thread
        MainThread.BeginInvokeOnMainThread(() =>
        {
            try
            {
                //Name: single line
                var nameEdit = new EditText(ctx)
                {
                    Hint = "Name",
                    Text = initialName ?? "",
                    InputType = Android.Text.InputTypes.ClassText | Android.Text.InputTypes.TextFlagCapSentences
                };
                nameEdit.SetSingleLine(true);

                //Description: multiline, starts at 3 lines, grows to 6 and then scrolls inside the field
                var descriptionEdit = new EditText(ctx)
                {
                    Hint = "Description",
                    Text = initialDescription ?? "",
                    InputType = Android.Text.InputTypes.ClassText | Android.Text.InputTypes.TextFlagMultiLine | Android.Text.InputTypes.TextFlagCapSentences,
                    Gravity = GravityFlags.Top | GravityFlags.Start
                };
                descriptionEdit.SetMinLines(3);
                descriptionEdit.SetMaxLines(6);

                //Scrollbar: an EditText created in code has no scrollbar drawable (normally it is created from XML attributes),
                //so by default no scrollbar is ever drawn. Turning the fading off without a scrollbar drawable crashes when the
                //scrollbar is drawn (ScrollBarDrawable.mutate() on null). Setting a thumb drawable creates the scrollbar drawable
                //(Android 10 / API 29 and later), so do that first, then keep the scrollbar visible while the text is longer than the field.
                //Before Android 10 the scrollbar properties are left alone: no scrollbar, but no crash either.
                if (OperatingSystem.IsAndroidVersionAtLeast(29))
                {
                    var thumb = new Android.Graphics.Drawables.GradientDrawable();
                    thumb.SetShape(Android.Graphics.Drawables.ShapeType.Rectangle);
                    thumb.SetCornerRadius(Dp(ctx, 2));
                    thumb.SetColor(Android.Graphics.Color.Argb(160, 128, 128, 128).ToArgb());
                    thumb.SetSize(Dp(ctx, 4), -1);

                    descriptionEdit.VerticalScrollbarThumbDrawable = thumb;
                    descriptionEdit.VerticalScrollBarEnabled = true;
                    descriptionEdit.ScrollBarStyle = ScrollbarStyles.InsideInset;
                    descriptionEdit.ScrollbarFadingEnabled = false;
                }

                //The dialog content sits in a ScrollView, which would take over a vertical drag.
                //When the text is longer than the field, let the field scroll its own text instead.
                descriptionEdit.Touch += (s, e) =>
                {
                    if (descriptionEdit.LineCount > descriptionEdit.MaxLines)
                    {
                        descriptionEdit.Parent?.RequestDisallowInterceptTouchEvent(true);
                    }
                    e.Handled = false;
                };

                var spinner = new Spinner(ctx)
                {
                    Adapter = new PoiSymbolAdapter(ctx, symbols)
                };
                spinner.SetSelection(index);

                var layout = new LinearLayout(ctx)
                {
                    Orientation = Android.Widget.Orientation.Vertical
                };
                layout.SetPadding(Dp(ctx, 24), Dp(ctx, 12), Dp(ctx, 24), Dp(ctx, 8));

                //A LayoutParams object can only be used for one view, so create a new one each time
                LinearLayout.LayoutParams Wide(int topMarginDp = 0)
                {
                    return new LinearLayout.LayoutParams(AViewGroup.LayoutParams.MatchParent, AViewGroup.LayoutParams.WrapContent)
                    {
                        TopMargin = Dp(ctx, topMarginDp)
                    };
                }

                TextView MakeLabel(string text)
                {
                    var label = new TextView(ctx)
                    {
                        Text = text
                    };
                    label.SetTextSize(ComplexUnitType.Sp, 12);
                    return label;
                }

                var messageView = new TextView(ctx)
                {
                    Text = message
                };
                layout.AddView(messageView, new LinearLayout.LayoutParams(AViewGroup.LayoutParams.MatchParent, AViewGroup.LayoutParams.WrapContent)
                {
                    BottomMargin = Dp(ctx, 8)
                });

                layout.AddView(MakeLabel("Name"), Wide(8));
                layout.AddView(nameEdit, Wide());

                layout.AddView(MakeLabel("Description"), Wide(12));
                layout.AddView(descriptionEdit, Wide());

                layout.AddView(MakeLabel("Symbol"), Wide(12));
                layout.AddView(spinner, Wide());

                //The dialog can get taller than the screen (message + three fields + keyboard), so let it scroll
                var scroll = new Android.Widget.ScrollView(ctx);
                scroll.AddView(layout, new FrameLayout.LayoutParams(AViewGroup.LayoutParams.MatchParent, AViewGroup.LayoutParams.WrapContent));

                //Result for a button that carries the dialog contents (positive and neutral)
                PoiDialogResult BuildResult(bool confirmed, bool saved)
                {
                    int pos = spinner.SelectedItemPosition;
                    POISymbol? selected = (pos >= 0 && pos < symbols.Count) ? symbols[pos] : null;
                    return new PoiDialogResult(confirmed, selected, placeholder && pos == 0, saved, nameEdit.Text ?? "", descriptionEdit.Text ?? "");
                }

                var builder = new AlertDialog.Builder(ctx);
                builder.SetTitle(title);
                builder.SetView(scroll);
                builder.SetCancelable(setCancelable);

                builder.SetPositiveButton(positiveText, (senderAlert, args) =>
                {
                    tcs.TrySetResult(BuildResult(true, false));
                });

                if (!string.IsNullOrEmpty(neutralText))
                {
                    builder.SetNeutralButton(neutralText, (senderAlert, args) =>
                    {
                        tcs.TrySetResult(BuildResult(false, true));
                    });
                }

                builder.SetNegativeButton(negativeText, (senderAlert, args) =>
                {
                    tcs.TrySetResult(new PoiDialogResult(false, null));
                });

                var dialog = builder.Create();
                dialog.CancelEvent += (s, e) =>
                {
                    tcs.TrySetResult(new PoiDialogResult(false, null));
                };

                //Keep the buttons and the focused field visible when the keyboard opens
                dialog.Window?.SetSoftInputMode(SoftInput.AdjustResize);
                dialog.Show();
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, "POI dialog failed");
                tcs.TrySetResult(null);
            }
        });

        return await tcs.Task;
    }

    private static int Dp(Context ctx, int dp)
    {
        return (int)TypedValue.ApplyDimension(ComplexUnitType.Dip, dp, ctx.Resources!.DisplayMetrics);
    }

    //Spinner adapter: icon + symbol name, same row layout for the closed and the open spinner
    private sealed class PoiSymbolAdapter : BaseAdapter<POISymbol>
    {
        private readonly Context context;
        private readonly IReadOnlyList<POISymbol> items;

        //Decoded once per dialog instead of on every GetView call (icons repeat, e.g. RoundFlag)
        private readonly Dictionary<string, Android.Graphics.Bitmap?> bitmaps = new(StringComparer.Ordinal);

        public PoiSymbolAdapter(Context context, IReadOnlyList<POISymbol> items)
        {
            this.context = context;
            this.items = items;
        }

        public override POISymbol this[int position] => items[position];

        public override int Count => items.Count;

        public override long GetItemId(int position) => position;

        public override AView GetView(int position, AView? convertView, AViewGroup? parent)
        {
            return BuildRow(position);
        }

        public override AView GetDropDownView(int position, AView? convertView, AViewGroup? parent)
        {
            return BuildRow(position);
        }

        private Android.Graphics.Bitmap? GetBitmap(string resourceName)
        {
            if (bitmaps.TryGetValue(resourceName, out var cached))
            {
                return cached;
            }

            Android.Graphics.Bitmap? bitmap = null;
            try
            {
                using var stream = typeof(Show_POI_Dialog).Assembly.GetManifestResourceStream(resourceName);
                if (stream != null)
                {
                    bitmap = Android.Graphics.BitmapFactory.DecodeStream(stream);
                }
                else
                {
                    Serilog.Log.Warning($"POI dialog: embedded resource '{resourceName}' not found");
                }
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, $"POI dialog: failed to load '{resourceName}'");
            }

            bitmaps[resourceName] = bitmap;
            return bitmap;
        }

        private AView BuildRow(int position)
        {
            var item = items[position];

            var row = new LinearLayout(context)
            {
                Orientation = Android.Widget.Orientation.Horizontal
            };
            row.SetGravity(GravityFlags.CenterVertical);
            row.SetPadding(Dp(context, 8), Dp(context, 8), Dp(context, 8), Dp(context, 8));

            var bitmap = GetBitmap(item.Icon);
            if (bitmap != null)
            {
                var icon = new ImageView(context);
                icon.SetScaleType(ImageView.ScaleType.FitCenter);
                icon.SetImageBitmap(bitmap);
                row.AddView(icon, new LinearLayout.LayoutParams(Dp(context, 32), Dp(context, 32))
                {
                    RightMargin = Dp(context, 12)
                });
            }

            var label = new TextView(context)
            {
                Text = item.Symbol
            };
            label.SetTextSize(ComplexUnitType.Sp, 16);
            row.AddView(label, new LinearLayout.LayoutParams(AViewGroup.LayoutParams.WrapContent, AViewGroup.LayoutParams.WrapContent));

            return row;
        }
    }
}