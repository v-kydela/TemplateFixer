using Newtonsoft.Json.Linq;
using System.Collections.Generic;

namespace TemplateFixer
{
    internal class DistinctTemplate
    {
        public int Index { get; set; }

        public JObject JObject { get; set; }

        public ICollection<string> Paths { get; } = new HashSet<string>();

        public string GetName() => $"Type {(char)(Index + 65)}";

        public override string ToString() => $"{GetName()} ({Paths.Count} found)";
    }
}
