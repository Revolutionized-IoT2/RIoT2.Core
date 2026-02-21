using System.Collections.Generic;

namespace RIoT2.Core.Interfaces.Services
{
    public interface IStoredObjectService
    {
        /// <summary>
        /// Fired when there are changes in objects. ChangeType can be: created, read, updated, or deleted
        /// </summary>
        event StoredObjectEventHandler StoredObjectEvent;

        /// <summary>
        /// Retursn all Type T objects from memory
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns>Objects of type T</returns>
        IEnumerable<T> GetAll<T>();

        /// <summary>
        /// Gets first object of type T from store
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        T Get<T>();

        /// <summary>
        /// Delets all objects of type T from store
        /// </summary>
        /// <typeparam name="T"></typeparam>
        void DeleteAll<T>();

        /// <summary>
        /// Delete object with defined ID
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="id">Object id to delete</param>
        /// <param name="persistent">If true, changes are stored. If set to false object is only deleted from memory.</param>
        void Delete<T>(string id, bool persistent = true);

        /// <summary>
        /// Saves the object
        /// </summary>
        /// <typeparam name="T">Type of object to save</typeparam>
        /// <param name="obj">Object to save</param>
        /// <param name="persistent">If true, object is saved also into persistent store. If set to false object is only stored in memory.</param>
        /// <param name="autoTypeNameHandling">If set to true, object is serialized with type information. Use if object has interface properties.</param>
        /// <returns></returns>
        string Save<T>(T obj, bool persistent = true, bool autoTypeNameHandling = false, bool includeNulls = false);
    }
}
