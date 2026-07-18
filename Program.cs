using System.Diagnostics;
using System.Drawing;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Xml;
using System.Xml.Serialization;

#region vars
List<translationEntry> entries = new List<translationEntry>();
bool running = true;
screenStates state = screenStates.mainMenu;
#endregion

wl("scav_translation_tool v0.1");
wl("--------------------------");
wl("");

while (running)
{
    clear();

    switch (state)
    {
        case screenStates.mainMenu:
            {
                string path = ask(true, "Enter path to either a serialized XML or the JSON file to translate");
                if (path.EndsWith(".xml")) entries = importFile(path);
                else if (path.EndsWith(".json")) parseData(JsonObject.Parse(File.ReadAllText(path)));
                state = screenStates.translating;
            }
            break;

        case screenStates.translating:
            if (!entries.Where(e => !e.isTranslated).Any()) { state = screenStates.save; break; }
            foreach (var entry in entries.Where(e => !e.isTranslated))
            {
                wl($"Path: {entry._path}");
                wl("");
                wl($"Original: {entry.Original}");
                wl("");
                string translation = ask(true, "Enter translation (or leave empty to skip)");
                if (translation == "savenquit")
                {
                    state = screenStates.save;
                    break;
                }
                entry.Translation = translation;
                clear();
            }
            break;

        case screenStates.search:

            break;

        case screenStates.goTo:

            break;

        case screenStates.quitConfirmation:

            break;
        
        case screenStates.save:
            {
                string path = ask(true, "Enter path to save either a serialized XML or a JSON file");
                if (path.EndsWith(".xml")) saveToFile(path, entries);
                else if (path.EndsWith(".json")) exportDataBackToJSON(entries, path);
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
void wrt(string text, int x, int y)
{
    goTo(x, y);
    Console.Write(text);
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
}

void parseData(JsonNode? node, int indent = 0, string path = "")
{
    if (node == null) return;
    if (node is JsonObject obj)
    {
        foreach (var kvp in obj)
        {
            var newPath = string.IsNullOrEmpty(path)
                ? kvp.Key
                : $"{path}.{kvp.Key}";

            parseData(kvp.Value, indent + 2, newPath);
        }
    }
    else if (node is JsonArray arr)
    {
        for (int i = 0; i < arr.Count; i++)
        {
            var newPath = string.IsNullOrEmpty(path)
                ? $"[{i}]"
                : $"{path}.[{i}]";

            parseData(arr[i], indent + 2, newPath);
        }
    }
    else if (node is JsonValue value)
    {
        entries.Add(new translationEntry
        {
            _path = path,
            Original = value.ToString()
        });
    }
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
}

enum screenStates
{
    mainMenu,
    translating,
    search,
    goTo,
    save,
    quitConfirmation
}