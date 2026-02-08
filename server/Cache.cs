using System;
using System.Collections;
using System.Collections.Generic;

namespace RcmServer
{
    // Can be fixed, shit code repetition, fast af doe
    public class Cache
    {
        public int ScriptLine { get; set; }

        private volatile HashSet<string> _templateFieldCache = new();
        private volatile HashSet<string> _templateResourceCache = new();

        private string _docText = string.Empty;
        private string[] _lines = Array.Empty<string>();
        private readonly object _syncRoot = new();

        public void UpdateDocument(string newText)
        {
            lock (_syncRoot)
            {
                if (_docText != newText)
                {
                    _docText = newText;
                    _lines = SplitLines(_docText);
                }
            }
        }

        public string GetLine(int lineNumber)
        {
            lock (_syncRoot)
            {
                return (lineNumber >= 0 && lineNumber < _lines.Length)
                    ? _lines[lineNumber]
                    : string.Empty;
            }
        }

        private static string[] SplitLines(string text)
        {
            // Might experience catastrophic breakage
            // Half TODO maybe fix this idk
            return text.Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.None);
        }

        public void UpdateTemplateFieldCache(HashSet<string> templateNames)
        {
            _templateFieldCache = templateNames;
        }

        public void ClearTemplateFieldCache()
        {
            _templateFieldCache = new();
        }

        public string GetField(string key)
        {
            string result;
            _templateFieldCache.TryGetValue(key, out result);
            return result;
        }

        public HashSet<string> GetFields()
        {
            return _templateFieldCache;
        }

        public void UpdateTemplateResourceCache(HashSet<string> templateNames)
        {
            _templateResourceCache = templateNames;
        }

        public void ClearTemplateResourceCache()
        {
            _templateResourceCache = new();
        }

        public string GetResource(string key)
        {
            string result;
            _templateResourceCache.TryGetValue(key, out result);
            return result;
        }

        public HashSet<string> GetResources()
        {
            return _templateResourceCache;
        }
    }
}
