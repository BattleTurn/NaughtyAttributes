namespace NaughtyAttributes.Editor
{
    /// <summary>
    /// A struct used as a key for identifying lists in dictionaries.
    /// </summary>
    internal struct ListKey
    {
        public int id;
        public string path;
        public ListKey(int id, string path) { this.id = id; this.path = path; }
    }
}