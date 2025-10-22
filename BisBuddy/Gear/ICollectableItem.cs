namespace BisBuddy.Gear
{
    /// <summary>
    /// Represents something that can be collected by the user, such as
    /// a gearpiece, materia, or a prerequisite item
    /// </summary>
    public interface ICollectableItem
    {
        /// <summary>
        /// The status of this collectable item, also considering the statuses of any child 
        /// items
        /// </summary>
        public CollectionStatusType CollectionStatus { get; }

        /// <summary>
        /// Is this specific item collected. Cannot be changed if <see cref="CollectLock"/> is set
        /// <br />
        /// For gearpieces and prerequisites, this means if the actual item id
        /// is obtained and assigned to this collectable item
        /// <br />
        /// For materia, it is when the materia has been attached to its parent item
        /// </summary>
        public bool IsCollected { get; set; }

        /// <summary>
        /// Locks the collect state of the specific item.
        /// <br />
        /// Does not effect any child items (i.e. prerequisites, materia), 
        /// so the <see cref="CollectionStatus"/> can change if those items are changed
        /// </summary>
        public bool CollectLock { get; set; }

        /// <summary>
        /// Set the <see cref="IsCollected" /> state of the item while the item is otherwise locked.
        /// Will lock the collection status if it is not locked on call
        /// </summary>
        /// <param name="toCollect">The value to set <see cref="IsCollected"/> to</param>
        public void SetIsCollectedLocked(bool toCollect);
    }
}
