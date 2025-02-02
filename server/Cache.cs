using System.Collections;
using System.Collections.Generic;

namespace RcmServer
{
    // Can be fixed, shit code repetition, fast af doe
    public class Cache
    {
        private volatile HashSet<string> _templateFieldCache = new();
        private volatile HashSet<string> _templateResourceCache = new();

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
