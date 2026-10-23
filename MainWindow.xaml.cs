using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Microsoft.Win32;
using Forms = System.Windows.Forms;
using Brush = System.Windows.Media.Brush;
using Color = System.Windows.Media.Color;
using ColorConverter = System.Windows.Media.ColorConverter;
using Cursors = System.Windows.Input.Cursors;
using DataFormats = System.Windows.DataFormats;
using DragEventArgs = System.Windows.DragEventArgs;
using DragDropEffects = System.Windows.DragDropEffects;
using FontFamily = System.Windows.Media.FontFamily;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;
using Point = System.Windows.Point;
using Size = System.Windows.Size;
using WpfPath = System.Windows.Shapes.Path;

namespace OrbitConvert;

public partial class MainWindow : Window
{
    private const double Center = 224;
    private const double InnerRadius = 78;
    private const double OuterRadius = 198;
    private string? _currentFile;
    private List<FormatChoice> _choices = [];
    private bool _working;

    private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
        { ".png", ".jpg", ".jpeg", ".bmp", ".gif", ".tif", ".tiff", ".ico" };
    private static readonly HashSet<string> MediaExtensions = new(StringComparer.OrdinalIgnoreCase)
        { ".mp4", ".mkv", ".mov", ".avi", ".webm", ".wmv", ".m4v", ".mp3", ".m4a", ".wav", ".flac", ".ogg", ".aac", ".wma" };
    private static readonly HashSet<string> DocumentExtensions = new(StringComparer.OrdinalIgnoreCase)
        { ".doc", ".docx", ".odt", ".rtf", ".txt", ".xls", ".xlsx", ".ods", ".csv", ".ppt", ".pptx", ".odp" };

    private sealed record FormatChoice(string Label, string Extension, string Color);

    public MainWindow()
    {
        InitializeComponent();
        UpdateMenu();
    }

    public void ShowCentered(bool activate = false)
    {
        var point = System.Windows.Forms.Cursor.Position;
        var work = Forms.Screen.FromPoint(point).WorkingArea;
        Left = work.Left + (work.Width - Width) / 2;
        Top = work.Top + (work.Height - Height) / 2;

        if (!IsVisible)
        {
            Opacity = 0;
            PopScale.ScaleX = PopScale.ScaleY = 0.88;
            Show();
            BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(210))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            });
            var pop = new DoubleAnimation(0.88, 1, TimeSpan.FromMilliseconds(260))
            {
                EasingFunction = new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.18 }
            };
            PopScale.BeginAnimation(ScaleTransform.ScaleXProperty, pop);
            PopScale.BeginAnimation(ScaleTransform.ScaleYProperty, pop);
        }
        else
        {
            UpdateLayout();
        }
        if (activate)
            Activate();
    }

    private void Window_DragEnter(object sender, DragEventArgs e) => PrepareDraggedFile(e);
    private void Window_DragOver(object sender, DragEventArgs e) => PrepareDraggedFile(e);

    private void PrepareDraggedFile(DragEventArgs e)
    {
        if (e.Data.GetData(DataFormats.FileDrop) is string[] files && files.Length > 0 &&
            !string.Equals(_currentFile, files[0], StringComparison.OrdinalIgnoreCase))
        {
            SetCurrentFile(files[0]);
        }
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private async void Window_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(DataFormats.FileDrop) is not string[] files || files.Length == 0)
            return;
        if (!string.Equals(_currentFile, files[0], StringComparison.OrdinalIgnoreCase))
            SetCurrentFile(files[0]);
        Activate();
        e.Handled = true;

        if (GetChoiceAt(e.GetPosition(MenuCanvas)) is { } choice)
            await ConvertToAsync(choice);
    }

    private FormatChoice? GetChoiceAt(Point position)
    {
        if (_choices.Count == 0)
            return null;
        var dx = position.X - Center;
        var dy = position.Y - Center;
        var radius = Math.Sqrt(dx * dx + dy * dy);
        if (radius < InnerRadius || radius > OuterRadius)
            return null;

        var angle = (Math.Atan2(dy, dx) * 180 / Math.PI + 90 + 360) % 360;
        var segment = 360.0 / _choices.Count;
        var index = (int)(angle / segment);
        var withinSegment = angle - index * segment;
        return withinSegment < 2.2 || withinSegment > segment - 2.2 ? null : _choices[index];
    }

    private void CenterPlate_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_working)
            return;
        if (_currentFile is not null)
        {
            _currentFile = null;
            UpdateMenu();
            return;
        }

        var dialog = new OpenFileDialog { Title = "Choose a file to convert", CheckFileExists = true };
        if (dialog.ShowDialog(this) == true)
            SetCurrentFile(dialog.FileName);
    }

    private void SetCurrentFile(string path)
    {
        if (!File.Exists(path))
        {
            CenterTitle.Text = "File not found";
            CenterHint.Text = "Drop a file from Explorer";
            return;
        }
        _currentFile = path;
        UpdateMenu();
    }

    private void UpdateMenu()
    {
        MenuCanvas.Children.Clear();
        _choices = _currentFile is null ? [] : GetChoices(_currentFile);

        if (_choices.Count > 0)
        {
            var segment = 360.0 / _choices.Count;
            for (var index = 0; index < _choices.Count; index++)
            {
                var choice = _choices[index];
                var start = -90 + index * segment + 2.2;
                var sweep = segment - 4.4;
                var path = new WpfPath
                {
                    Data = CreateWedge(start, sweep),
                    Fill = BrushFromHex(choice.Color),
                    Stroke = new SolidColorBrush(Color.FromArgb(125, 255, 255, 255)),
                    StrokeThickness = 1.4,
                    Cursor = Cursors.Hand,
                    ToolTip = $"Convert to {choice.Label}",
                    Opacity = 0,
                    RenderTransformOrigin = new Point(0.5, 0.5),
                    RenderTransform = new ScaleTransform(0.86, 0.86)
                };
                path.MouseLeftButtonUp += async (_, _) => await ConvertToAsync(choice);
                path.MouseEnter += (_, _) => AnimateWedge(path, 1.045, 125);
                path.MouseLeave += (_, _) => AnimateWedge(path, 1, 145);
                MenuCanvas.Children.Add(path);

                var pop = new DoubleAnimation(0.86, 1, TimeSpan.FromMilliseconds(260))
                {
                    BeginTime = TimeSpan.FromMilliseconds(index * 28),
                    EasingFunction = new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.13 }
                };
                var fade = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(190))
                {
                    BeginTime = TimeSpan.FromMilliseconds(index * 28),
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                };
                path.BeginAnimation(OpacityProperty, fade);
                ((ScaleTransform)path.RenderTransform).BeginAnimation(ScaleTransform.ScaleXProperty, pop);
                ((ScaleTransform)path.RenderTransform).BeginAnimation(ScaleTransform.ScaleYProperty, pop);

                var midpoint = start + sweep / 2;
                var radians = midpoint * Math.PI / 180;
                var labelRadius = (InnerRadius + OuterRadius) / 2;
                var label = new TextBlock
                {
                    Text = choice.Label,
                    Width = 72,
                    Height = 24,
                    FontFamily = new FontFamily("Segoe UI Semibold"),
                    FontSize = 13,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = new SolidColorBrush(Color.FromRgb(49, 43, 56)),
                    TextAlignment = TextAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    IsHitTestVisible = false
                };
                Canvas.SetLeft(label, Center + Math.Cos(radians) * labelRadius - label.Width / 2);
                Canvas.SetTop(label, Center + Math.Sin(radians) * labelRadius - label.Height / 2);
                MenuCanvas.Children.Add(label);
            }
        }

        if (_currentFile is null)
        {
            CenterGlyph.Text = "✦";
            CenterTitle.Text = "Drop a file";
            CenterHint.Text = "Shift + drag · or browse";
        }
        else
        {
            CenterGlyph.Text = _working ? "◌" : "⇄";
            CenterTitle.Text = _working ? "Converting…" : Truncate(Path.GetFileName(_currentFile), 17);
            CenterHint.Text = _working ? "Running on this PC" : _choices.Count > 0
                ? "Release on a format · or click"
                : MissingEngineMessage(_currentFile);
        }
    }

    private static string Truncate(string value, int max)
    {
        if (value.Length <= max) return value;
        return value[..(max - 1)] + "…";
    }

    private static string MissingEngineMessage(string file)
    {
        var ext = Path.GetExtension(file);
        if (MediaExtensions.Contains(ext)) return "Add FFmpeg to enable media formats";
        if (DocumentExtensions.Contains(ext)) return "Add LibreOffice to enable documents";
        return "This file format is not supported yet";
    }

    private static List<FormatChoice> GetChoices(string file)
    {
        var ext = Path.GetExtension(file);
        if (ImageExtensions.Contains(ext))
        {
            var formats = new[]
            {
                new FormatChoice("PNG", ".png", "#FFFFF0C9"),
                new FormatChoice("JPG", ".jpg", "#FFFFE7C8"),
                new FormatChoice("BMP", ".bmp", "#FFF7D5CB"),
                new FormatChoice("GIF", ".gif", "#FFFFE0D2"),
                new FormatChoice("TIFF", ".tiff", "#FFFFECD5")
            };
            return formats.Where(x => !SameExtension(ext, x.Extension)).ToList();
        }

        if (MediaExtensions.Contains(ext) && ConverterTools.FindFfmpeg() is not null)
        {
            if (new[] { ".mp3", ".m4a", ".wav", ".flac", ".ogg", ".aac", ".wma" }.Contains(ext, StringComparer.OrdinalIgnoreCase))
            {
                return new[]
                {
                    new FormatChoice("MP3", ".mp3", "#FFFFEBCB"),
                    new FormatChoice("M4A", ".m4a", "#FFFFDCCB"),
                    new FormatChoice("WAV", ".wav", "#FFFFE8CF"),
                    new FormatChoice("FLAC", ".flac", "#FFF9D7D2"),
                    new FormatChoice("OGG", ".ogg", "#FFFFE3D2")
                }.Where(x => !SameExtension(ext, x.Extension)).ToList();
            }

            return new[]
            {
                new FormatChoice("MP4", ".mp4", "#FFFFEBCB"),
                new FormatChoice("MKV", ".mkv", "#FFFFDCCB"),
                new FormatChoice("M4A", ".m4a", "#FFFFE8CF"),
                new FormatChoice("MP3", ".mp3", "#FFF9D7D2"),
                new FormatChoice("GIF", ".gif", "#FFFFE3D2")
            }.Where(x => !SameExtension(ext, x.Extension)).ToList();
        }

        if (DocumentExtensions.Contains(ext) && ConverterTools.FindLibreOffice() is not null)
        {
            var targets = ext is ".xls" or ".xlsx" or ".ods" or ".csv"
                ? new[] { ("PDF", ".pdf"), ("XLSX", ".xlsx"), ("ODS", ".ods"), ("CSV", ".csv"), ("HTML", ".html") }
                : ext is ".ppt" or ".pptx" or ".odp"
                    ? new[] { ("PDF", ".pdf"), ("PPTX", ".pptx"), ("ODP", ".odp"), ("HTML", ".html"), ("JPG", ".jpg") }
                    : new[] { ("PDF", ".pdf"), ("DOCX", ".docx"), ("ODT", ".odt"), ("RTF", ".rtf"), ("TXT", ".txt") };
            return targets.Select((x, i) => new FormatChoice(x.Item1, x.Item2,
                    new[] { "#FFFFEBCB", "#FFFFDCCB", "#FFFFE8CF", "#FFF9D7D2", "#FFFFE3D2" }[i]))
                .Where(x => !SameExtension(ext, x.Extension)).ToList();
        }

        return [];
    }

    private static bool SameExtension(string a, string b) =>
        string.Equals(a, b, StringComparison.OrdinalIgnoreCase) ||
        (a.Equals(".jpeg", StringComparison.OrdinalIgnoreCase) && b == ".jpg") ||
        (a.Equals(".jpg", StringComparison.OrdinalIgnoreCase) && b == ".jpeg");

    private static Geometry CreateWedge(double startDegrees, double sweepDegrees)
    {
        var start = PointAt(OuterRadius, startDegrees);
        var end = PointAt(OuterRadius, startDegrees + sweepDegrees);
        var innerEnd = PointAt(InnerRadius, startDegrees + sweepDegrees);
        var innerStart = PointAt(InnerRadius, startDegrees);
        var figure = new PathFigure { StartPoint = start, IsClosed = true };
        figure.Segments.Add(new ArcSegment(end, new Size(OuterRadius, OuterRadius), 0, sweepDegrees > 180,
            SweepDirection.Clockwise, true));
        figure.Segments.Add(new LineSegment(innerEnd, true));
        figure.Segments.Add(new ArcSegment(innerStart, new Size(InnerRadius, InnerRadius), 0, sweepDegrees > 180,
            SweepDirection.Counterclockwise, true));
        figure.Segments.Add(new LineSegment(start, true));
        return new PathGeometry([figure]);
    }

    private static Point PointAt(double radius, double degrees)
    {
        var radians = degrees * Math.PI / 180;
        return new Point(Center + Math.Cos(radians) * radius, Center + Math.Sin(radians) * radius);
    }

    private static Brush BrushFromHex(string value) =>
        new SolidColorBrush((Color)ColorConverter.ConvertFromString(value));

    private static void AnimateWedge(WpfPath path, double scale, int durationMs)
    {
        if (path.RenderTransform is not ScaleTransform transform)
            return;
        var animation = new DoubleAnimation(scale, TimeSpan.FromMilliseconds(durationMs))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        transform.BeginAnimation(ScaleTransform.ScaleXProperty, animation);
        transform.BeginAnimation(ScaleTransform.ScaleYProperty, animation);
    }

    private async Task ConvertToAsync(FormatChoice choice)
    {
        if (_working || _currentFile is null)
            return;

        var source = _currentFile;
        _working = true;
        CenterHint.Text = "Running on this PC";
        UpdateMenu();
        try
        {
            var output = BuildOutputPath(source, choice.Extension);
            var inputExt = Path.GetExtension(source);
            if (ImageExtensions.Contains(inputExt))
                await Task.Run(() => ConvertImage(source, output, choice.Extension));
            else if (MediaExtensions.Contains(inputExt))
                await ConverterTools.RunFfmpegAsync(source, output, choice.Extension);
            else if (DocumentExtensions.Contains(inputExt))
                await ConverterTools.RunLibreOfficeAsync(source, output, choice.Extension);
            else
                throw new NotSupportedException("This input format is not supported.");

            CenterGlyph.Text = "✓";
            CenterTitle.Text = "Finished";
            CenterHint.Text = $"Saved as {Path.GetFileName(output)}";
            _currentFile = output;
            UpdateMenu();
            CenterGlyph.Text = "✓";
            CenterTitle.Text = "Finished";
            CenterHint.Text = $"Saved as {Path.GetFileName(output)}";
        }
        catch (Exception ex)
        {
            CenterGlyph.Text = "!";
            CenterTitle.Text = "Could not convert";
            CenterHint.Text = ex.Message.Length > 55 ? ex.Message[..52] + "…" : ex.Message;
        }
        finally
        {
            _working = false;
        }
    }

    private static string BuildOutputPath(string source, string targetExtension)
    {
        var directory = Path.GetDirectoryName(source)!;
        var stem = Path.GetFileNameWithoutExtension(source) + ".converted";
        var output = Path.Combine(directory, stem + targetExtension);
        var number = 1;
        while (File.Exists(output))
            output = Path.Combine(directory, $"{stem} ({number++}){targetExtension}");
        return output;
    }

    private static void ConvertImage(string source, string destination, string extension)
    {
        var decoder = System.Windows.Media.Imaging.BitmapDecoder.Create(
            new Uri(source), System.Windows.Media.Imaging.BitmapCreateOptions.PreservePixelFormat,
            System.Windows.Media.Imaging.BitmapCacheOption.OnLoad);
        if (decoder.Frames.Count == 0)
            throw new InvalidDataException("Windows could not read this image.");

        System.Windows.Media.Imaging.BitmapEncoder encoder = extension.ToLowerInvariant() switch
        {
            ".jpg" or ".jpeg" => new System.Windows.Media.Imaging.JpegBitmapEncoder { QualityLevel = 92 },
            ".png" => new System.Windows.Media.Imaging.PngBitmapEncoder(),
            ".bmp" => new System.Windows.Media.Imaging.BmpBitmapEncoder(),
            ".gif" => new System.Windows.Media.Imaging.GifBitmapEncoder(),
            ".tif" or ".tiff" => new System.Windows.Media.Imaging.TiffBitmapEncoder(),
            _ => throw new NotSupportedException("That image output format is not supported.")
        };
        encoder.Frames.Add(decoder.Frames[0]);
        using var stream = File.Create(destination);
        encoder.Save(stream);
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Hide();
            e.Handled = true;
        }
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Hide();

    private static class ConverterTools
    {
        public static string? FindFfmpeg() => FindExecutable("ffmpeg.exe");
        public static string? FindLibreOffice() => FindExecutable("soffice.exe") ?? FindExecutable("soffice.com");

        private static string? FindExecutable(string name)
        {
            var local = Path.Combine(AppContext.BaseDirectory, "engines", name);
            if (File.Exists(local)) return local;
            foreach (var directory in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator))
            {
                try
                {
                    var candidate = Path.Combine(directory.Trim(), name);
                    if (File.Exists(candidate)) return candidate;
                }
                catch { /* Ignore malformed PATH entries. */ }
            }
            return null;
        }

        public static async Task RunFfmpegAsync(string input, string output, string targetExtension)
        {
            var executable = FindFfmpeg() ?? throw new FileNotFoundException("FFmpeg was not found.");
            var start = new ProcessStartInfo(executable)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardError = true,
                RedirectStandardOutput = true
            };
            start.ArgumentList.Add("-y");
            start.ArgumentList.Add("-i");
            start.ArgumentList.Add(input);
            if (targetExtension.Equals(".gif", StringComparison.OrdinalIgnoreCase))
            {
                start.ArgumentList.Add("-vf");
                start.ArgumentList.Add("fps=12,scale=640:-1:flags=lanczos");
            }
            start.ArgumentList.Add(output);
            using var process = Process.Start(start) ?? throw new InvalidOperationException("FFmpeg did not start.");
            var errorTask = process.StandardError.ReadToEndAsync();
            var outputTask = process.StandardOutput.ReadToEndAsync();
            await process.WaitForExitAsync();
            var error = await errorTask;
            _ = await outputTask;
            if (process.ExitCode != 0)
                throw new InvalidOperationException(LastUsefulLine(error) ?? "FFmpeg could not convert this file.");
        }

        public static async Task RunLibreOfficeAsync(string input, string output, string targetExtension)
        {
            var executable = FindLibreOffice() ?? throw new FileNotFoundException("LibreOffice was not found.");
            var tempDirectory = Path.Combine(Path.GetTempPath(), "OrbitConvert-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDirectory);
            try
            {
                var start = new ProcessStartInfo(executable)
                {
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardError = true,
                    RedirectStandardOutput = true
                };
                start.ArgumentList.Add("--headless");
                start.ArgumentList.Add("--convert-to");
                start.ArgumentList.Add(targetExtension.TrimStart('.'));
                start.ArgumentList.Add("--outdir");
                start.ArgumentList.Add(tempDirectory);
                start.ArgumentList.Add(input);
                using var process = Process.Start(start) ?? throw new InvalidOperationException("LibreOffice did not start.");
                var errorTask = process.StandardError.ReadToEndAsync();
                var outputTask = process.StandardOutput.ReadToEndAsync();
                await process.WaitForExitAsync();
                var error = await errorTask;
                _ = await outputTask;
                if (process.ExitCode != 0)
                    throw new InvalidOperationException(LastUsefulLine(error) ?? "LibreOffice could not convert this file.");

                var generated = Path.Combine(tempDirectory, Path.GetFileNameWithoutExtension(input) + targetExtension);
                if (!File.Exists(generated))
                    throw new InvalidOperationException("LibreOffice did not create the expected output file.");
                File.Move(generated, output);
            }
            finally
            {
                try { Directory.Delete(tempDirectory, recursive: true); } catch { }
            }
        }

        private static string? LastUsefulLine(string text) => text.Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Trim()).LastOrDefault(line => line.Length > 0);
    }
}
