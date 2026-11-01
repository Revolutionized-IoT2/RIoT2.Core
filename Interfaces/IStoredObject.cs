namespace RIoT2.Core.Interfaces
{
    /// <summary>
    /// An object the orchestrator keeps in its store, as <c>StoredObjects/&lt;Type&gt;/&lt;Id&gt;.json</c>
    /// (typed persistence, plan M2 step 4).
    /// </summary>
    public interface IStoredObject
    {
        /// <summary>The object's id and file name. When it is empty on save, the store assigns a new GUID.</summary>
        string Id { get; set; }
    }
}
