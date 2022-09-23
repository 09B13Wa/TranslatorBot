﻿#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using System.Text;

namespace TranslatorBot.Modules.Utils;

public sealed class DataAssociation
{
    private Dictionary<string, string> _data;
    private string _path;

    private DataAssociation()
    {
        _path = "";
        _data = new Dictionary<string, string>();
        Count = 0;
    }

    public string Path
    {
        get => _path;
        internal set
        {
            _path = value;
            if (!FileExists()) File.Create(value);
        }
    }

    public Dictionary<string, string> Data
    {
        get
        {
            Dictionary<string, string> dataCopy = new Dictionary<string, string>();
            foreach (KeyValuePair<string, string> keyValuePair in _data)
                dataCopy.Add(keyValuePair.Key, keyValuePair.Value);
            return dataCopy;
        }
    }

    public int Count { get; private set; }

    public string this[string proprety]
    {
        get => _data[proprety];
        set => _data[proprety] = value;
    }

    public Dictionary<string, string>.KeyCollection Keys => new(Data);

    public Dictionary<string, string>.ValueCollection Values => new(Data);

    public static DataAssociation FromData(string data)
    {
        DataAssociation dataAssociation = new();
        dataAssociation.Path = "";
        (Dictionary<string, string> data, int numberOfProperties) parsedData = LoadFromData(data);
        dataAssociation._data = parsedData.data;
        dataAssociation.Count = parsedData.numberOfProperties;
        return dataAssociation;
    }

    public static DataAssociation FromFile(string path)
    {
        DataAssociation dataAssociation = new();
        dataAssociation._path = path;
        (Dictionary<string, string> data, int numberOfProperties) parsedData = LoadOrCreate(path);
        dataAssociation._data = parsedData.data;
        dataAssociation.Count = parsedData.numberOfProperties;
        return dataAssociation;
    }

    public bool AddProprety(string propretyName, string value)
    {
        bool wasAdded = true;
        try
        {
            _data.Add(propretyName, value);
            Count += 1;
            SaveData(Path, _data);
        }
        catch (ArgumentException)
        {
            wasAdded = false;
        }

        return wasAdded;
    }

    public void ClearProprety()
    {
        _data.Clear();
        Count = 0;
    }

    public bool ContainsPropertyName(string propertyName)
    {
        return _data.ContainsKey(propertyName);
    }

    public bool ContainsValueName(string propertyValue)
    {
        return _data.ContainsValue(propertyValue);
    }

    public int EnsureCapacity(int capacity)
    {
        return _data.EnsureCapacity(capacity);
    }

    public Dictionary<string, string>.Enumerator GetProprietiesEnumerator()
    {
        return Data.GetEnumerator();
    }

    public void GetPropretiesObject(SerializationInfo info, StreamingContext context)
    {
        _data.GetObjectData(info, context);
    }

    public void OnPropretyDeserialization(object? sender)
    {
        _data.OnDeserialization(sender);
    }

    public bool RemoveProperty(string propertyName)
    {
        bool wasRemoved = _data.Remove(propertyName);
        if (wasRemoved)
            Count -= 1;
        return wasRemoved;
    }

    public bool RemoveProperty(string propertyName, string propertyValue)
    {
        bool wasRemoved = _data.Remove(propertyName, out propertyValue!);
        if (wasRemoved)
            Count -= 1;
        return wasRemoved;
    }

    public void TrimExcess()
    {
        _data.TrimExcess();
    }

    public void TrimExcess(int capacity)
    {
        _data.TrimExcess();
    }

    public bool TryGetValue(string propertyName, out string propertyValue)
    {
        return _data.TryGetValue(propertyName, out propertyValue!);
    }

    public string GetValueOrDefault(string propertyName, string defaultValue)
    {
        bool wasFound = _data.TryGetValue(propertyName, out string propertyValue);
        return wasFound ? propertyValue : defaultValue;
    }

    public bool SetValue(string propertyName, string value)
    {
        if (!ContainsPropertyName(propertyName)) return false;
        _data[propertyName] = value;
        return true;
    }

    private bool FileExists()
    {
        return FileExists(_path);
    }

    private static bool FileExists(string path)
    {
        return File.Exists(path);
    }

    private static (Dictionary<string, string>, int) LoadOrCreate(string path)
    {
        Dictionary<string, string> loadedData;
        if (FileExists(path))
        {
            using StreamReader reader = new(path);
            string fileContents = reader.ReadToEnd();
            (Dictionary<string, string> data, int numberOfPropreties) parserResponse =
                ParseContentsFromFile(fileContents);
            reader.Close();

            return parserResponse;
        }

        File.Create(path);
        loadedData = new Dictionary<string, string>();
        return (loadedData, 0);
    }

    private static (Dictionary<string, string>, int) LoadFromData(string data)
    {
        (Dictionary<string, string> data, int numberOfPropreties) parserResponse = ParseContentsFromFile(data);
        return parserResponse;
    }

    private static (Dictionary<string, string>, int) ParseContentsFromFile(string contents)
    {
        Dictionary<string, string> data = new();
        int numberOfProperties = 0;

        StringBuilder currentValueLoader = new();
        bool isPropertyName = false;
        bool isPropertyValue = true;
        string currentPropertyName = "";
        bool isEscaped = false;
        bool isInProperty = false;

        foreach (char c in contents)
            isEscaped = ParseCharacters(c, isEscaped, currentValueLoader, data, ref isInProperty,
                ref numberOfProperties, ref currentPropertyName, ref isPropertyName, ref isPropertyValue);

        if (isPropertyName)
        {
            if (isInProperty)
                currentPropertyName = currentValueLoader.ToString();

            bool wasAdded = data.TryAdd(currentPropertyName, "");

            if (wasAdded)
                numberOfProperties += 1;
        }

        return (data, numberOfProperties);
    }

    private static bool ParseCharacters(char c, bool isEscaped, StringBuilder currentValueLoader,
        Dictionary<string, string> data,
        ref bool isInProprety, ref int numberOfPropreties, ref string currentPropretyName, ref bool isPropretyName,
        ref bool isPropretyValue)
    {
        switch (c)
        {
            case '\\' when !isEscaped:
                isEscaped = true;
                break;
            case '"' when !isEscaped && isInProprety:
            {
                (numberOfPropreties, currentPropretyName) = AdjustForPropretyClosing(currentValueLoader,
                    isPropretyName,
                    currentPropretyName, isPropretyValue, data, numberOfPropreties, out isInProprety);
                break;
            }
            case '"' when !isEscaped && !isInProprety:
            {
                (isPropretyName, isPropretyValue) =
                    AdjustForPropretyEntry(isPropretyName, isPropretyValue, out isInProprety);
                break;
            }
            default:
                (isInProprety, isEscaped) = ParseRegularChar(isInProprety, currentValueLoader, c, isEscaped);
                break;
        }

        return isEscaped;
    }

    private static (bool, bool) ParseRegularChar(bool isInProprety, StringBuilder currentValueLoader, char c,
        bool isEscaped)
    {
        if (isInProprety)
        {
            currentValueLoader.Append(c);
            if (isEscaped)
                isEscaped = false;
        }

        return (isInProprety, isEscaped);
    }

    private static (bool, bool) AdjustForPropretyEntry(bool isPropretyName, bool isPropretyValue,
        out bool isInProprety)
    {
        if (isPropretyName)
        {
            isPropretyName = false;
            isPropretyValue = true;
        }
        else if (isPropretyValue)
        {
            isPropretyName = true;
            isPropretyValue = false;
        }

        isInProprety = true;
        return (isPropretyName, isPropretyValue);
    }

    private static (int, string) AdjustForPropretyClosing(StringBuilder currentValueLoader, bool isPropretyName,
        string currentPropretyName, bool isPropretyValue, Dictionary<string, string> data, int numberOfPropreties,
        out bool isInProprety)
    {
        string currentValue = currentValueLoader.ToString();
        if (isPropretyName)
        {
            currentPropretyName = currentValue;
        }
        else if (isPropretyValue)
        {
            string propretyValue = currentValue;
            bool wasAdded = data.TryAdd(currentPropretyName, propretyValue);
            if (wasAdded)
                numberOfPropreties += 1;
        }

        isInProprety = false;
        currentValueLoader.Clear();
        return (numberOfPropreties, currentPropretyName);
    }

    internal static void SaveData(string path, Dictionary<string, string> data)
    {
        using StreamWriter streamWriter = new(path);
        string dataRepresentation = BuildDataRepresentation(data);
        streamWriter.Write(dataRepresentation);
        streamWriter.Close();
    }

    private static string BuildDataRepresentation(Dictionary<string, string> data)
    {
        StringBuilder dataRepresentationBuilder = new();

        foreach (KeyValuePair<string, string> proprety in data)
        {
            string propretyName = proprety.Key;
            string propretyValue = proprety.Value;
            dataRepresentationBuilder.AppendLine($"\"{propretyName}\":\"{propretyValue}\"");
        }

        string dataRepresentation = dataRepresentationBuilder.ToString();
        return dataRepresentation;
    }

    public bool Save()
    {
        if (!FileExists()) return false;
        SaveData(_path, _data);
        return true;
    }
}