using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices.Java;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Xml;
using System.Xml.Serialization;
Console.InputEncoding = Encoding.Unicode;
Console.OutputEncoding = Encoding.Unicode;


#region vars
List<translationEntry> entries = new();
IEnumerable<(translationEntry entry, int index)> untranslated()
{
    return entries
        .Select((entry, index) => (entry, index))
        .Where(x => !x.entry.isTranslated);
}
List<translationEntry> imported = new();
bool running = true;
var rnd = new Random();
int currentEntryIndex = 0;
string tempBuffer = "";
screenStates state =screenStates.mainMenu;
bool redraw = true;
bool translated()
{
    var untranslatedEntries = untranslated().ToList();

    return (untranslatedEntries.Count == 0);
}
translationEntry entry()
{
    return entries[currentEntryIndex];
}
#endregion

wl("scav_translation_tool v0.idkitworks");
wl("-----------------------------------");
wl("");

while (running)
{
    if (redraw)
    {
        redraw = false;
        clear();
        if (state == screenStates.translating)
        {
            wrt("[ESC] Exit, [ENTER] Next, [^R] Random, [^S] Save, [ALT+M] Mark" + Environment.NewLine + (entry().isMarked ? "Entry marked" : "Entry unmarked"), 0, Console.WindowHeight - 2, 0, 3);

            {
                if (!entries.Where(e => !e.isTranslated).Any()) { setState(screenStates.save); break; }
                string progress = $"{entries.Where(e => e.isTranslated).Count()}/{entries.Count}";
                string progressPercent = (entries.Where(e => e.isTranslated).Count() * 100 / entries.Count).ToString() + "%";
                Console.Write(progress);
                Console.Write(new string(' ', (26 - progress.Length - progressPercent.Length)));
                wl(progressPercent);
                int progressChars = (entries.Where(e => e.isTranslated).Count() * 26) / entries.Count;
                wl($"{new string('█', progressChars)}{new string('░', 26 - progressChars)}");
                wl("");
            }

            wl($"Path: {entry()._path}");
            wl("");
            wl($"Original: \"{entry().Original}\"");
            wl("");
            if (entry().isTranslated)
            {
                wl($"Translation: {entry().Translation}");
                wl("");
            }
            wl("Enter translation (or leave empty to skip)");
            Console.Write("> ");
        }
    }

    switch (state)
    {
        case screenStates.mainMenu:
            {
                string path = ask(true, "Enter path to either a serialized XML or the JSON file to translate");
                if (path.EndsWith(".xml")) entries = importFile(path);
                else if (path.EndsWith(".json"))
                {
                    entries = parseData(JsonObject.Parse(File.ReadAllText(path)));
                    wl("");
                    path = ask(true, "Enter path to the original JSON to cross-reference for translation progress, or leave empty");
                    if (path.EndsWith(".json"))
                    {
                        var original = parseData(JsonObject.Parse(File.ReadAllText(path)));
                        foreach (var entr in entries)
                        {
                            var match = original.Find(x => x._path == entr._path);
                            if (match != null)
                            {
                                if (entr.Original != match.Original)
                                {
                                    entr.Translation = entr.Original;
                                    entr.Original = match.Original;
                                }
                            }
                        }
                    }
                }
                else
                {
                    wl("Invalid file type.");
                    wl("Press ENTER to try again...");
                    Console.ReadLine();
                    break;
                }

                {
                    var untranslatedEntries = untranslated().ToList();

                    if (untranslatedEntries.Count == 0)
                        setState(screenStates.save);
                    else
                    {
                        currentEntryIndex =
                            untranslatedEntries.First().index;

                        tempBuffer = "";
                        setState(screenStates.translating);
                    }
                }
            }
            break;

        case screenStates.import:
            {
                string path = ask(true, "Enter path to a serialized XML to import and merge");
                if (path.EndsWith(".xml")) imported = importFile(path);
                else if (path.EndsWith(".json"))
                {
                    imported = parseData(JsonObject.Parse(File.ReadAllText(path)));
                    wl("");
                    path = ask(true, "Enter path to the original JSON to cross-reference");
                    if (path.EndsWith(".json"))
                    {
                        var original = parseData(JsonObject.Parse(File.ReadAllText(path)));
                        foreach (var entr in imported)
                        {
                            var match = original.Find(x => x._path == entr._path);
                            if (match != null) entr.Original = match.Original;
                        }
                    }
                }
                else
                {
                    wl("Invalid file type.");
                    wl("Press ENTER to try again...");
                    Console.ReadLine();
                    redraw = true;
                    break;
                }
                setState(screenStates.merge);
            }
            redraw = true;
            break;

        case screenStates.merge:
            {
                var entriesToMerge = imported.Where(e => e.isTranslated);
                var atFirst = entriesToMerge.Count();
                int i = 0;
                {
                    foreach (var entryToMerge in entriesToMerge)
                    {
                        clear();
                        {
                            string progress = $"{i}/{atFirst}";
                            string progressPercent = (i * 100 / atFirst).ToString() + "%";
                            Console.Write(progress);
                            Console.Write(new string(' ', (26 - progress.Length - progressPercent.Length)));
                            wl(progressPercent);
                            int progressChars = (i * 26) / atFirst;
                            Console.WriteLine($"{new string('█', progressChars)}{new string('░', 26 - progressChars)}");
                            wl("");
                        }
                        wl($"Path: {entryToMerge._path}");
                        wl("");
                        wl($"Original: {entryToMerge.Original}");
                        wl("");
                        wl($"Existing translation: {entries.Where(e => e._path == entryToMerge._path).Select(e => e.Translation).FirstOrDefault()}");
                        wl($"Imported translation: {entryToMerge.Translation}");
                        wl("");
                        
                        string decision = ask(true, $"Which one do you want to keep? [Existing/Imported/EMPTY]");
                        i++;
                        switch (decision.ToLower())
                        {
                            case "i":
                                entries.Where(e => e._path == entryToMerge._path).First().Translation = entryToMerge.Translation;
                                break;
                            case "empty":
                                entries.Where(e => e._path == entryToMerge._path).First().Translation = "";
                                break;
                            default:
                                break;
                        }
                    }
                }
                setState(screenStates.translating);
            }
            break;

        case screenStates.translating:
            var key = Console.ReadKey(true);

            switch (key.Key)
            {
                case ConsoleKey.Escape:
                    setState(screenStates.quitConfirmation);
                    break;
                //case ConsoleKey.F1:
                //    setState(screenStates.help);
                //    break;
                case ConsoleKey.S when key.Modifiers.HasFlag(ConsoleModifiers.Control):
                    setState(screenStates.save);
                    break;
                case ConsoleKey.M when key.Modifiers.HasFlag(ConsoleModifiers.Alt):
                    entry().switchMark();
                    wrt((entry().isMarked ? "Entry   marked" : "Entry unmarked"), 0, Console.WindowHeight - 1, Console.GetCursorPosition().Left, Console.GetCursorPosition().Top);
                    break;
                case ConsoleKey.Backspace:
                    if (tempBuffer.Length > 0)
                    {
                        tempBuffer = tempBuffer[..^1];
                        Console.Write("\b \b");
                    }
                    break;

                case ConsoleKey.Enter:
                    {
                        entries[currentEntryIndex].Translation = tempBuffer;

                        if (!untranslated().Any())
                        {
                            setState(screenStates.save);
                            break;
                        }

                        var next = untranslated()
                            .Where(e => e.index > currentEntryIndex)
                            .FirstOrDefault();

                        if (next != default) currentEntryIndex = next.index;
                        else currentEntryIndex = untranslated().First().index;

                        tempBuffer = "";

                        setState(screenStates.translating);
                    }
                    break;

                case ConsoleKey.R when key.Modifiers.HasFlag(ConsoleModifiers.Control):
                    {
                        var untranslatedEntries = untranslated().ToList();
                        if (untranslatedEntries.Count == 0)
                        {
                            setState(screenStates.save);
                            break;
                        }
                        currentEntryIndex = untranslatedEntries[rnd.Next(untranslatedEntries.Count)].index;
                        tempBuffer = "";
                        setState(screenStates.translating);
                    }
                    break;

                default:
                    if (key.KeyChar != '\0')
                    {
                        tempBuffer += key.KeyChar;
                        Console.Write(key.KeyChar);
                    }
                    break;
            }

            break;

        case screenStates.search:

            break;

        case screenStates.goTo:

            break;

        case screenStates.save:
            {
                string question = translated()
                    ? "Everything is translated! Enter path to save either a serialized XML or a JSON file"
                    : "Enter path to save either a serialized XML or a JSON file";

                string path = ask(true, question);
                if (path.EndsWith(".xml") || path.EndsWith(".json"))
                {
                    if (path.EndsWith(".xml")) saveToFile(path, entries);
                    else if (path.EndsWith(".json")) exportDataBackToJSON(entries, path);

                    if (translated()) setState(screenStates.quitConfirmation);
                    else setState(screenStates.translating);
                }
                else if (string.IsNullOrEmpty(path))
                {
                    setState(screenStates.translating);
                }
                else
                {
                    wl("Invalid file type.");
                    wl("Press ENTER to try again...");
                    Console.ReadLine();
                }
            }
            break;

        case screenStates.quitConfirmation:
            {
                string question = translated()
                    ? "Are you sure you want to quit? [Y/N]"
                    : "Are you sure you want to quit without saving? [Y/N/SAVE]";

                string decision = ask(true, question);

                if (decision.ToLower() == "n")
                {
                    setState(screenStates.translating);
                }
                else if (decision.ToLower() == "y" || decision.ToLower() == "save")
                {
                    if (decision.ToLower() == "y")
                    {
                        running = false;
                    }
                    else if (decision.ToLower() == "save")
                    {
                        string path = ask(true, "Enter path to save either a serialized XML or a JSON file");
                        if (path.EndsWith(".xml")) saveToFile(path, entries);
                        else if (path.EndsWith(".json")) exportDataBackToJSON(entries, path);
                    }
                    running = false;
                }
            }
            break;
    }
}

string ask(bool removeQuotes = false, string ?question = null)
{
    if (!string.IsNullOrEmpty(question)) wl(question);
    Console.Write("> ");
    var input = Console.ReadLine();
    return removeQuotes ? input.Replace("\"", "") : input;
}
void wl(string text) { Console.WriteLine(text); }
void wrt(string text, int x, int y, int originalX, int originalY)
{
    goTo(x, y);
    Console.Write(text);
    goTo(originalX, originalY);
}
void goTo(int x, int y)
{
    Console.SetCursorPosition(x, y);
}
void clearLine()
{
    Console.Write(new string(' ', Console.WindowWidth));
}
void clear()
{
    for (int i = 3; i < Console.WindowHeight; i++)
    {
        goTo(0, i);
        clearLine();
    }
    goTo(0, 3);

    redraw = false;
}

List<translationEntry> parseData(JsonNode? node, string path = "")
{
    var parsed = new List<translationEntry>();

    if (node == null) return null;
    if (node is JsonObject obj)
    {
        foreach (var kvp in obj)
        {
            var newPath = string.IsNullOrEmpty(path)
                ? kvp.Key
                : $"{path}.{kvp.Key}";

            parsed.AddRange(parseData(kvp.Value, newPath));
        }
    }
    else if (node is JsonArray arr)
    {
        for (int i = 0; i < arr.Count; i++)
        {
            var newPath = string.IsNullOrEmpty(path)
                ? $"[{i}]"
                : $"{path}.[{i}]";

            parsed.AddRange(parseData(arr[i], newPath));
        }
    }
    else if (node is JsonValue value)
    {
        parsed.Add(new translationEntry
        {
            _path = path,
            Original = value.ToString()
        });
    }

    return parsed;
}

void exportDataBackToJSON(List<translationEntry> toExport, string path2file)
{
    JsonNode root = new JsonObject();
    bool rootInitialized = false;

    foreach (var entry in toExport)
    {
        if (string.IsNullOrWhiteSpace(entry._path))
            continue;

        var parts = entry.Path;
        if (parts.Length == 0)
            continue;

        if (!rootInitialized)
        {
            root = IsArrayIndexSegment(parts[0]) ? new JsonArray() : new JsonObject();
            rootInitialized = true;
        }

        JsonNode? cursor = root;

        for (int i = 0; i < parts.Length; i++)
        {
            var part = parts[i];
            bool isLast = i == parts.Length - 1;

            if (IsArrayIndexSegment(part))
            {
                if (cursor is not JsonArray array)
                    throw new InvalidOperationException($"Expected JsonArray while processing '{entry._path}'.");

                int index = ParseArrayIndex(part);
                while (array.Count <= index)
                    array.Add(null);

                if (isLast)
                {
                    array[index] = CreateNodeValue(entry.isTranslated ? entry.Translation : entry.Original);
                    break;
                }

                JsonNode? next = array[index];
                if (next is null)
                {
                    next = (i + 1 < parts.Length && IsArrayIndexSegment(parts[i + 1]))
                        ? new JsonArray()
                        : new JsonObject();

                    array[index] = next;
                }

                cursor = next;
            }
            else
            {
                if (cursor is not JsonObject obj)
                    throw new InvalidOperationException($"Expected JsonObject while processing '{entry._path}'.");

                if (isLast)
                {
                    obj[part] = CreateNodeValue(entry.isTranslated ? entry.Translation : entry.Original);
                    break;
                }

                JsonNode? next = obj[part];
                if (next is null)
                {
                    next = (i + 1 < parts.Length && IsArrayIndexSegment(parts[i + 1]))
                        ? new JsonArray()
                        : new JsonObject();

                    obj[part] = next;
                }

                cursor = next;
            }
        }
    }

    File.WriteAllText(path2file, root.ToJsonString(new JsonSerializerOptions
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        IndentSize = 4,
        NewLine = "\n"
    }));
}

JsonNode? CreateNodeValue(string? value)
{
    return value is null ? null : JsonValue.Create(value);
}

bool IsArrayIndexSegment(string part) =>
    !string.IsNullOrWhiteSpace(part) &&
    part.StartsWith('[') &&
    part.EndsWith(']') &&
    int.TryParse(part[1..^1], out _);

int ParseArrayIndex(string part) => int.Parse(part[1..^1]);

void saveToFile(string filename, List<translationEntry> toSave)
{
    XmlSerializer xmlSer = new XmlSerializer(typeof(List<translationEntry>));

    using (var sww = new StringWriter())
    {
        using (XmlWriter writer = XmlWriter.Create(sww))
        {
            xmlSer.Serialize(writer, toSave);
            File.WriteAllText(filename, sww.ToString());
        }
    }
}

List<translationEntry> importFile(string filename)
{
    var data = new List<translationEntry>();
    XmlSerializer xmlSer = new XmlSerializer(typeof(List<translationEntry>));
    using (var reader = new StreamReader(filename))
    {
        var importedEntries = (List<translationEntry>)xmlSer.Deserialize(reader);
        data.AddRange(importedEntries);
    }
    return data;
}

void setState(screenStates newState)
{
    state = newState;
    redraw = true;
}

public class translationEntry
{
    public string _path { get; set; }
    public string[] Path =>
        string.IsNullOrWhiteSpace(_path)
            ? Array.Empty<string>()
            : _path.Split('.', StringSplitOptions.RemoveEmptyEntries);

    public string Original { get; set; }
    public string? Translation { get; set; }
    public bool isTranslated =>
        !string.IsNullOrWhiteSpace(Translation);
    public bool isMarked { get; set; }
    public void switchMark() => isMarked = !isMarked;
}

enum screenStates
{
    mainMenu,
    translating,
    search,
    goTo,
    save,
    quitConfirmation,
    import,
    merge
}